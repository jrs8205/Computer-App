using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using HardwareMonitor.App.Localization;
using HardwareMonitor.App.Services;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Charts;
using HardwareMonitor.Core.Localization;
using HardwareMonitor.Core.Insights;
using HardwareMonitor.Core.Logging;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Notifications;
using HardwareMonitor.Core.Power;
using HardwareMonitor.Core.Reports;
using HardwareMonitor.Core.Sensors;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;
using HardwareMonitor.Core.WindowsEvents;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Päänäkymän logiikka: käynnistää sensorien luvun, päivittää arvot 1 sekunnin
/// välein (suunnitelman luku 14: UI-päivitys 1 s) ja kirjoittaa lukemat debug-lokiin.
///
/// Puu rakennetaan kerran ja arvot päivitetään paikallaan sensorin tunnisteen
/// (Identifier) perusteella, jotta laajennettu tila ja vieritys säilyvät.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SensorService _sensorService = new();
    private readonly DebugLogger _logger = new();
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, SensorViewModel> _sensorIndex = new();
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private readonly SampleAggregator _aggregator;
    private readonly ThresholdMonitor _thresholdMonitor;
    private readonly LastStateService _lastState = new();
    private HistoryDb? _historyDb;
    private EventLogService? _events;
    private WindowsEventCollector? _windowsEvents;

    /// <summary>Viimeisin sensoriluenta konetuntemus-lokin kokoonpano-osiota varten.</summary>
    private IReadOnlyList<HardwareGroup>? _latestGroups;

    /// <summary>Esim. "Windows 11 (build 26200)" — build ≥ 22000 on Windows 11.</summary>
    private static readonly string OsDescriptionText =
        $"Windows {(Environment.OSVersion.Version.Build >= 22000 ? 11 : 10)} " +
        $"(build {Environment.OSVersion.Version.Build})";
    private bool _previousSessionCrashed;
    private IReadOnlyList<EventRow> _recentEvents = Array.Empty<EventRow>();
    private SampleStats? _dayStats;
    private KeyMetrics? _latestMetrics;
    private MetricStates? _latestStates;
    private int _windowsScanRunning;
    private int _analysisRefreshRunning;
    private int _insightsWriteRunning;
    private int _historyRefreshRunning;
    private bool _historyRefreshPending;
    private int _rowsLogged;
    private readonly object _dbWriteLock = new();
    private readonly List<Task> _dbWrites = new();

    private string _status = UiStrings.Status_Starting;
    private int _tickCount;

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        _aggregator = new SampleAggregator(_settings.Logging.SensorIntervalSeconds);
        _thresholdMonitor = new ThresholdMonitor(_settings.Thresholds);
        _autoStart = AutostartService.IsEnabled();
        Dashboard.RenameFan = RenameFan;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        SettingsPage = new SettingsViewModel(
            _settings, () => OnOverlaySettingChanged(nameof(SettingsPage)));
        History.RefreshRequested += RefreshHistoryInBackground;
    }

    private bool _autoStart;

    /// <summary>
    /// Käynnistä Windowsin mukana. Tila luetaan Task Schedulerista (ei
    /// settings.jsonista), jotta checkbox ei erkane todellisuudesta.
    /// </summary>
    public bool AutoStart
    {
        get => _autoStart;
        set
        {
            if (_autoStart == value)
            {
                return;
            }

            // Sarjallistetaan kaikki Task Scheduler -operaatiot: taustapäivitys
            // (RefreshIfEnabled) ja tämä muutos eivät saa ajaa yhtä aikaa.
            lock (_autostartLock)
            {
                if (AutostartService.SetEnabled(value, _logger.Log))
                {
                    _autoStart = value;
                }
                else
                {
                    _logger.Log("VIRHE: automaattikäynnistyksen muutos epäonnistui (vaatii admin-oikeudet).");
                }

                // Todellinen tila kannasta, ettei checkbox erkane (esim.
                // suojaamattomasta polusta SetEnabled kieltäytyy).
                _autoStart = AutostartService.IsEnabled();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoStart)));
        }
    }

    /// <summary>Sarjallistaa Task Scheduler -operaatiot (UI-muutos vs. taustapäivitys).</summary>
    private readonly object _autostartLock = new();

    /// <summary>Jälki debug-lokiin, kun overlay sulkeutui ilman sovelluksen omaa kutsua.</summary>
    public void LogOverlayUnexpectedClose() =>
        _logger.Log("[WARNING] Overlay-ikkuna sulkeutui odottamatta — luodaan uudelleen.");

    /// <summary>
    /// Jälki debug-lokiin, kun overlay luotiin uudelleen herätyksen jälkeen
    /// (läpinäkyvän ikkunan sisältö voi jäädä tyhjäksi näytön unen jälkeen).
    /// </summary>
    public void LogOverlayRecovered(PowerSessionEvent trigger) =>
        _logger.Log($"Overlay luotu uudelleen herätyksen jälkeen (syy: {trigger}).");

    /// <summary>Tallentaa käyttäjän raahaaman overlay-sijainnin.</summary>
    public void SetOverlayCustomPosition(double left, double top)
    {
        _settings.Overlay.UseCustomPosition = true;
        _settings.Overlay.CustomLeft = left;
        _settings.Overlay.CustomTop = top;
        OnOverlaySettingChanged(nameof(OverlayCornerIndex));
    }

    /// <summary>Tallentaa tuulettimen oman nimen; tyhjä nimi palauttaa oletuksen.</summary>
    public void RenameFan(string identifier, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            _settings.FanLabels.Remove(identifier);
        }
        else
        {
            _settings.FanLabels[identifier] = newName;
        }

        OnOverlaySettingChanged(nameof(Dashboard));
    }

    public ObservableCollection<HardwareViewModel> Hardware { get; } = new();

    public DashboardViewModel Dashboard { get; } = new();

    public OverlayViewModel Overlay { get; } = new();

    /// <summary>Asetukset-välilehden kentät (Vaihe 8.2).</summary>
    public SettingsViewModel SettingsPage { get; }

    /// <summary>Historia-välilehden graafit (Vaihe 8.3).</summary>
    public HistoryViewModel History { get; } = new();

    /// <summary>Laukeaa kun mikä tahansa overlay-asetus muuttuu (tallennus + ikkunan päivitys).</summary>
    public event Action? OverlaySettingsChanged;

    /// <summary>Nostetaan kun raja-arvohälytyksestä pitää näyttää tray-ilmoitus.</summary>
    public event Action<TrayNotification>? NotificationRequested;

    public OverlaySettings OverlaySettings => _settings.Overlay;

    public bool OverlayEnabled
    {
        get => _settings.Overlay.Enabled;
        set
        {
            if (_settings.Overlay.Enabled == value)
            {
                return;
            }

            _settings.Overlay.Enabled = value;
            OnOverlaySettingChanged(nameof(OverlayEnabled));
        }
    }

    /// <summary>0=vasen ylä, 1=oikea ylä, 2=vasen ala, 3=oikea ala (ComboBoxin järjestys).</summary>
    public int OverlayCornerIndex
    {
        get => (int)_settings.Overlay.Corner;
        set
        {
            if ((int)_settings.Overlay.Corner == value)
            {
                return;
            }

            _settings.Overlay.Corner = (OverlayCorner)value;
            _settings.Overlay.UseCustomPosition = false; // kulmavalinta palauttaa kulma-asemointiin
            OnOverlaySettingChanged(nameof(OverlayCornerIndex));
        }
    }

    public double OverlayOpacity
    {
        get => _settings.Overlay.Opacity;
        set
        {
            if (Math.Abs(_settings.Overlay.Opacity - value) < 0.01)
            {
                return;
            }

            _settings.Overlay.Opacity = value;
            OnOverlaySettingChanged(nameof(OverlayOpacity));
        }
    }

    public bool OverlayShowCpu
    {
        get => _settings.Overlay.ShowCpu;
        set
        {
            if (_settings.Overlay.ShowCpu == value)
            {
                return;
            }

            _settings.Overlay.ShowCpu = value;
            OnOverlaySettingChanged(nameof(OverlayShowCpu));
        }
    }

    public bool OverlayShowGpu
    {
        get => _settings.Overlay.ShowGpu;
        set
        {
            if (_settings.Overlay.ShowGpu == value)
            {
                return;
            }

            _settings.Overlay.ShowGpu = value;
            OnOverlaySettingChanged(nameof(OverlayShowGpu));
        }
    }

    public bool OverlayShowRam
    {
        get => _settings.Overlay.ShowRam;
        set
        {
            if (_settings.Overlay.ShowRam == value)
            {
                return;
            }

            _settings.Overlay.ShowRam = value;
            OnOverlaySettingChanged(nameof(OverlayShowRam));
        }
    }

    public bool OverlayShowDisks
    {
        get => _settings.Overlay.ShowDisks;
        set
        {
            if (_settings.Overlay.ShowDisks == value)
            {
                return;
            }

            _settings.Overlay.ShowDisks = value;
            OnOverlaySettingChanged(nameof(OverlayShowDisks));
        }
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray == value)
            {
                return;
            }

            _settings.MinimizeToTray = value;
            OnOverlaySettingChanged(nameof(MinimizeToTray));
        }
    }

    public bool AlertNotificationsEnabled
    {
        get => _settings.AlertNotificationsEnabled;
        set
        {
            if (_settings.AlertNotificationsEnabled == value)
            {
                return;
            }

            _settings.AlertNotificationsEnabled = value;
            OnOverlaySettingChanged(nameof(AlertNotificationsEnabled));
        }
    }

    public bool OverlayShowFans
    {
        get => _settings.Overlay.ShowFans;
        set
        {
            if (_settings.Overlay.ShowFans == value)
            {
                return;
            }

            _settings.Overlay.ShowFans = value;
            OnOverlaySettingChanged(nameof(OverlayShowFans));
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            PropertyChanged?.Invoke(this, StatusChangedArgs);
        }
    }

    public string LogPath => _logger.LogPath;

    /// <summary>Kutsutaan pääikkunan Loaded-tapahtumasta.</summary>
    public void Start()
    {
        try
        {
            _sensorService.Start();
            _logger.Log($"SensorService käynnistetty. Loki: {_logger.LogPath}");

            try
            {
                _historyDb = new HistoryDb();
                _historyDb.PurgeOlderThan(DateTimeOffset.Now.AddDays(-_settings.Logging.KeepHistoryDays));
                _events = new EventLogService(_historyDb);
                _events.Info("App", UiStrings.Event_AppStarted);
                _logger.Log($"Historia-kanta: {_historyDb.DbPath}");

                // Vaihe 6: tunnista edellisen istunnon yllättävä päättyminen (luku 17).
                if (_lastState.ReadPrevious() is { CleanShutdown: false } previous)
                {
                    _previousSessionCrashed = true;
                    string message = Strings.Risk_PrevSessionCrashed + DescribeLastState(previous);
                    _events.Warning("Järjestelmä", message, sensor: RiskAnalyzer.LastStateSensor);
                    _logger.Log($"[WARNING] {message}");
                }

                // Vaihe 5: Windowsin System-lokin rautatapahtumat events-tauluun.
                _windowsEvents = new WindowsEventCollector(new SystemEventReader(), _historyDb);
                ScanWindowsEventsInBackground();
                RefreshHistoryInBackground();
                RefreshAnalysisCachesInBackground();
                WriteMachineInsightsInBackground();
            }
            catch (Exception ex)
            {
                _historyDb = null;
                _logger.Log($"VIRHE historia-kannan avauksessa: {ex.Message} — jatketaan ilman lokitusta.");
            }

            Refresh();
            _timer.Start();

            // Pidä ajastettu tehtävä ajan tasalla (exe-polku ja --tray-argumentti)
            // taustalla, ettei schtasks-kutsu viivytä käynnistystä. Sarjallistettu
            // AutoStart-checkboxin kanssa; lopuksi UI:n tila päivitetään todellisen
            // mukaan (vaarallisen tehtävän poisto voi muuttaa tilan).
            Task.Run(() =>
            {
                lock (_autostartLock)
                {
                    AutostartService.RefreshIfEnabled(_logger.Log);
                    bool actual = AutostartService.IsEnabled();
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (_autoStart != actual)
                        {
                            _autoStart = actual;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoStart)));
                        }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Status = string.Format(UiStrings.Status_StartError, ex.Message);
            _logger.Log($"VIRHE käynnistyksessä: {ex}");
        }
    }

    private void Refresh()
    {
        try
        {
            IReadOnlyList<HardwareGroup> groups = _sensorService.Read();
            _latestGroups = groups;

            KeyMetrics metrics = KeyMetricsService.Extract(groups);
            Dashboard.Update(metrics, _settings.FanLabels);
            Overlay.Update(metrics, _settings);

            ThresholdResult thresholds = _thresholdMonitor.Update(metrics, DateTimeOffset.Now, _settings.FanLabels);
            Dashboard.ApplyStates(thresholds.States);
            Overlay.SetWorstState(thresholds.States.Worst);

            // Vaihe 6: selkokielinen kokonaisarvio tilapaneeliin (luku 19).
            RiskAssessment assessment = RiskAnalyzer.Assess(
                thresholds.States, metrics, _settings.Thresholds,
                _recentEvents, _dayStats, _previousSessionCrashed);
            Dashboard.ApplySummary(assessment);

            // Raporttia varten (Vaihe 7).
            _latestMetrics = metrics;
            _latestStates = thresholds.States;

            // Vaihe 8: balloon-ilmoitus ilmaisinalueelle hälytyksistä.
            TrayNotification? notification =
                NotificationBuilder.Build(thresholds.Events, _settings.AlertNotificationsEnabled);
            if (notification is not null)
            {
                NotificationRequested?.Invoke(notification);
            }

            foreach (ThresholdEvent alert in thresholds.Events)
            {
                _logger.Log($"[{alert.Level}] {alert.Message}");
                switch (alert.Level)
                {
                    case "CRITICAL":
                        _events?.Critical(alert.Component, alert.Message, alert.Sensor, alert.Value, alert.Threshold);
                        break;
                    case "WARNING":
                        _events?.Warning(alert.Component, alert.Message, alert.Sensor, alert.Value, alert.Threshold);
                        break;
                    default:
                        _events?.Info(alert.Component, alert.Message, alert.Sensor, alert.Value, alert.Threshold);
                        break;
                }
            }

            if (Hardware.Count == 0)
            {
                BuildTree(groups);
            }
            else
            {
                UpdateValues(groups);
            }

            AggregatedSample? aggregate = _aggregator.Add(metrics, DateTimeOffset.Now);
            if (aggregate is not null && _historyDb is { } db)
            {
                RunDbWrite(() =>
                {
                    try
                    {
                        db.InsertSample(aggregate);
                        Interlocked.Increment(ref _rowsLogged);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"VIRHE historian kirjoituksessa: {ex.Message}");
                    }
                });
            }

            _tickCount++;

            // "Ennen kaatumista" -tila talteen 5 s välein (luku 17).
            if (_tickCount % 5 == 0)
            {
                RunDbWrite(() =>
                {
                    try
                    {
                        _lastState.Write(metrics, DateTimeOffset.Now);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"VIRHE last_state-kirjoituksessa: {ex.Message}");
                    }
                });
            }

            // Windows-lokin uudelleenskannaus 5 minuutin välein.
            if (_tickCount % 300 == 0)
            {
                ScanWindowsEventsInBackground();
            }

            // Analyysin tapahtuma- ja huippukoosteet minuutin välein.
            if (_tickCount % 60 == 30)
            {
                RefreshAnalysisCachesInBackground();
            }

            // Konetuntemus-loki 30 min välein (ensin minuutin kohdalla,
            // jotta istunnon tuore data päätyy tiedostoon nopeasti).
            if (_tickCount % 1800 == 60)
            {
                WriteMachineInsightsInBackground();
            }

            // Historiagraafien data minuutin välein (offset ettei osu muihin töihin).
            if (_tickCount % 60 == 45)
            {
                RefreshHistoryInBackground();
            }

            Status =
                string.Format(UiStrings.Status_Line, DateTime.Now,
                    _sensorIndex.Count, Hardware.Count, _tickCount,
                    Volatile.Read(ref _rowsLogged));

            LogSnapshot(groups);
        }
        catch (Exception ex)
        {
            Status = string.Format(UiStrings.Status_UpdateError, ex.Message);
            _logger.Log($"VIRHE päivityksessä: {ex}");
        }
    }

    /// <summary>
    /// Skannaa Windowsin System-lokin taustasäikeessä (luku voi kestää
    /// sekunteja). Päällekkäiset skannaukset estetään lipulla.
    /// </summary>
    private void ScanWindowsEventsInBackground()
    {
        if (_windowsEvents is not { } collector ||
            Interlocked.CompareExchange(ref _windowsScanRunning, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                int written = collector.Scan();
                if (written > 0)
                {
                    _logger.Log($"Windows-loki: {written} uutta rautatapahtumaa kirjattu.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"VIRHE Windows-lokin luvussa: {ex.Message}");
            }
            finally
            {
                Volatile.Write(ref _windowsScanRunning, 0);
            }
        });
    }

    private static readonly SampleStats EmptyStats = new(
        0, new MetricStat(null, null), new MetricStat(null, null),
        new MetricStat(null, null), new MetricStat(null, null),
        new MetricStat(null, null), Array.Empty<DiskStat>(), Array.Empty<FanStat>());

    /// <summary>Selkokielinen raportti (luku 20), tai null jos dataa ei vielä ole.</summary>
    public string? BuildReport()
    {
        if (_latestMetrics is not { } metrics ||
            _latestStates is not { } states)
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        SampleStats dayStats = _historyDb?.GetSampleStats(now.AddHours(-24)) ?? EmptyStats;
        IReadOnlyList<EventRow> dayEvents =
            _historyDb?.ReadEventsSince(now.AddHours(-24)) ?? Array.Empty<EventRow>();

        // Riski lasketaan samasta tuoreesta datasta kuin raportin listat —
        // tickin välimuistittu arvio voi olla minuutin vanha, jolloin
        // yhteenveto voisi sanoa "Hyvä" vaikka alla listataan juuri
        // kirjattu CRITICAL-tapahtuma.
        RiskAssessment assessment = RiskAnalyzer.Assess(
            states, metrics, _settings.Thresholds,
            dayEvents, dayStats, _previousSessionCrashed);

        return ReportBuilder.Build(
            now, assessment, metrics, states,
            dayStats,
            _historyDb?.GetSampleStats(now.AddDays(-30)) ?? EmptyStats,
            dayEvents,
            _settings.Thresholds);
    }

    /// <summary>Viimeisen 24 h sensorihistoria CSV:nä, tai null jos historiaa ei ole.</summary>
    public string? BuildCsv()
    {
        if (_historyDb is not { } db)
        {
            return null;
        }

        IReadOnlyList<SampleRow> rows = db.ReadSampleRows(DateTimeOffset.Now.AddHours(-24));
        return rows.Count == 0 ? null : CsvExporter.Build(rows, CultureInfo.CurrentCulture);
    }

    /// <summary>Kuvaa kaatumista edeltäneen tilan tapahtumaviestiin (luku 17).</summary>
    private static string DescribeLastState(LastState state)
    {
        var parts = new List<string>();
        if (state.CpuTempC is { } cpu)
        {
            parts.Add($"CPU {cpu:0} °C");
        }

        if (state.GpuHotspotC is { } hotspot)
        {
            parts.Add($"GPU hotspot {hotspot:0} °C");
        }

        if (state.RamLoadPercent is { } ram)
        {
            parts.Add($"RAM {ram:0} %");
        }

        foreach (LastStateDisk disk in state.Disks)
        {
            if (disk.TempC is { } t)
            {
                parts.Add($"{disk.Name} {t:0} °C");
            }
        }

        return parts.Count == 0
            ? ""
            : string.Format(UiStrings.Event_LastState,
                state.Timestamp.ToLocalTime(), string.Join(", ", parts));
    }

    /// <summary>Hakee 24 h tapahtumat ja huiput riskianalyysille taustalla.</summary>
    private void RefreshAnalysisCachesInBackground()
    {
        if (_historyDb is not { } db ||
            Interlocked.CompareExchange(ref _analysisRefreshRunning, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                DateTimeOffset since = DateTimeOffset.Now.AddHours(-24);
                _recentEvents = db.ReadEventsSince(since);
                _dayStats = db.GetSampleStats(since);
            }
            catch (Exception ex)
            {
                _logger.Log($"VIRHE analyysikoosteiden haussa: {ex.Message}");
            }
            finally
            {
                Volatile.Write(ref _analysisRefreshRunning, 0);
            }
        });
    }

    /// <summary>Polku konetuntemus-lokiin (myös Claude lukee tätä istunnoissaan).</summary>
    public static string MachineInsightsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HardwareMonitor",
        "machine-insights.md");

    /// <summary>
    /// Rakentaa konetuntemus-lokin tuoreesta datasta, tai null jos historia-
    /// kantaa ei ole. Käytetään sekä taustakirjoituksessa että yläpalkin
    /// "AI-raportti"-napeissa (kopioi/tallenna).
    /// </summary>
    public string? BuildMachineInsights()
    {
        if (_historyDb is not { } db)
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        return MachineInsightsBuilder.Build(new MachineInsightsInput(
            now,
            MachineSpecReader.Read(
                Volatile.Read(ref _latestGroups) ?? Array.Empty<HardwareGroup>(),
                OsDescriptionText,
                _settings.InsightsNotes),
            db.GetSampleStats(now.AddDays(-30)),
            db.GetSampleStats(now.AddDays(-7)),
            db.ReadEventsSince(now.AddDays(-30)),
            _settings.Thresholds));
    }

    private void WriteMachineInsightsInBackground()
    {
        if (_historyDb is null ||
            Interlocked.CompareExchange(ref _insightsWriteRunning, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                // Käynnistyskirjoitus voi ehtiä ennen ensimmäistä sensoriluentaa —
                // odotetaan hetki, ettei kokoonpano-osio jää tyhjäksi ("—").
                for (int i = 0; i < 30 && Volatile.Read(ref _latestGroups) is null; i++)
                {
                    Thread.Sleep(500);
                }

                if (BuildMachineInsights() is { } markdown)
                {
                    // Atominen korvaus: levytila loppuessa tai kaatuessa aiempi
                    // kelvollinen raportti säilyy (ei tyhjää/osittaista tiedostoa).
                    HardwareMonitor.Core.IO.AtomicFile.WriteAllText(
                        MachineInsightsPath, markdown, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"VIRHE konetuntemus-lokin kirjoituksessa: {ex.Message}");
            }
            finally
            {
                Volatile.Write(ref _insightsWriteRunning, 0);
            }
        });
    }

    private void BuildTree(IReadOnlyList<HardwareGroup> groups)
    {
        Hardware.Clear();
        _sensorIndex.Clear();

        foreach (HardwareGroup group in groups)
        {
            Hardware.Add(BuildHardware(group));
        }
    }

    private HardwareViewModel BuildHardware(HardwareGroup group)
    {
        var vm = new HardwareViewModel(group.Name, group.HardwareType);

        // Alalaitteet ensin, sitten sensorit — luettavampi järjestys puussa.
        foreach (HardwareGroup sub in group.SubHardware)
        {
            vm.Items.Add(BuildHardware(sub));
        }

        foreach (SensorReading reading in group.Sensors)
        {
            var sensorVm = new SensorViewModel(reading);
            vm.Items.Add(sensorVm);
            _sensorIndex[reading.Identifier] = sensorVm;
        }

        return vm;
    }

    private void UpdateValues(IReadOnlyList<HardwareGroup> groups)
    {
        bool newSensorAppeared = false;
        int sensorsSeen = 0;

        foreach (HardwareGroup group in groups)
        {
            newSensorAppeared |= UpdateGroup(group, ref sensorsSeen);
        }

        // Puu rakennetaan uudelleen kun laitteita tulee lisää (esim. USB-levy)
        // TAI kun indeksoitu sensori katoaa (irrotus, sleep/resume) — muuten
        // kadonneen laitteen rivit jäisivät näkyviin viimeisine arvoineen.
        if (newSensorAppeared || sensorsSeen != _sensorIndex.Count)
        {
            BuildTree(groups);
        }
    }

    private bool UpdateGroup(HardwareGroup group, ref int sensorsSeen)
    {
        bool newSensorAppeared = false;

        foreach (SensorReading reading in group.Sensors)
        {
            if (_sensorIndex.TryGetValue(reading.Identifier, out SensorViewModel? sensorVm))
            {
                sensorVm.Update(reading);
                sensorsSeen++;
            }
            else
            {
                newSensorAppeared = true;
            }
        }

        foreach (HardwareGroup sub in group.SubHardware)
        {
            newSensorAppeared |= UpdateGroup(sub, ref sensorsSeen);
        }

        return newSensorAppeared;
    }

    private void LogSnapshot(IReadOnlyList<HardwareGroup> groups)
    {
        // Koko sensorilista lokiin minuutin välein yhtenä eräkirjoituksena —
        // rivi kerrallaan kirjoitettuna satojen rivien snapshot venytti
        // 1 s mittausväliä ja kasvatti lokin 149 MB:iin neljässä päivässä.
        if (_tickCount % 60 != 1)
        {
            return;
        }

        var lines = new List<string> { $"--- Sensoritilanne (päivitys #{_tickCount}) ---" };
        foreach (HardwareGroup group in groups)
        {
            CollectLogLines(group, indent: 0, lines);
        }

        _logger.LogBatch(lines);
    }

    private static void CollectLogLines(HardwareGroup group, int indent, List<string> lines)
    {
        string pad = new(' ', indent * 2);
        lines.Add($"{pad}[{group.HardwareType}] {group.Name}");

        foreach (SensorReading reading in group.Sensors)
        {
            string value = reading.Value.HasValue
                ? $"{reading.Value.Value:0.###} {reading.Unit}".Trim()
                : "—";
            lines.Add($"{pad}  {reading.SensorType,-12} {reading.SensorName}: {value}");
        }

        foreach (HardwareGroup sub in group.SubHardware)
        {
            CollectLogLines(sub, indent + 1, lines);
        }
    }

    /// <summary>
    /// Käynnistää DB-kirjoituksen taustalla ja seuraa tehtävää, jotta sammutus
    /// voi odottaa jonossa olevat kirjoitukset valmiiksi ennen kannan
    /// sulkemista (viimeinen kirjoitus ei saa hukkua).
    /// </summary>
    private void RunDbWrite(Action write)
    {
        var task = Task.Run(write);
        lock (_dbWriteLock)
        {
            _dbWrites.RemoveAll(t => t.IsCompleted);
            _dbWrites.Add(task);
        }
    }

    /// <summary>Graafien enimmäispistemäärä; SQL-harvennus tähtää samaan rajaan.</summary>
    private const int ChartMaxPoints = 500;

    /// <summary>
    /// Hakee historiagraafien datan taustalla; päällekkäisyys estetty. Jos
    /// haku pyydetään käynnissä olevan aikana, merkitään pending ja ajetaan
    /// uudelleen lopuksi — tämä kattaa myös ABA-vaihdon (24 h → 7 pv → 24 h),
    /// jossa pelkkä tuntimäärän vertailu jättäisi tuloksen vanhentuneeksi.
    /// </summary>
    private void RefreshHistoryInBackground()
    {
        if (_historyDb is not { } db)
        {
            return;
        }

        if (Interlocked.Exchange(ref _historyRefreshRunning, 1) == 1)
        {
            _historyRefreshPending = true;
            return;
        }

        int hours = History.RangeHours;

        // Nimilaput kaapataan UI-säikeellä muuttumattomana snapshotina — niitä
        // ei saa enumeroida taustalla samalla kun UI muokkaa sanakirjaa.
        Dictionary<string, string> fanLabels = BuildFanLabelMap();

        Task.Run(() =>
        {
            try
            {
                // Harvennus SQL:ssä bucket-koosteiksi: 30 pv alue toisi muuten
                // ~518 000 riviä lapsineen muistiin ja pitäisi kantalukkoa
                // koko materialisoinnin ajan (UI:n tapahtumakirjaukset jonossa).
                int bucketSeconds = Math.Max(
                    _settings.Logging.SensorIntervalSeconds,
                    (int)Math.Ceiling(hours * 3600 / (double)ChartMaxPoints));
                IReadOnlyList<SampleRow> rows = db.ReadSampleRowsDownsampled(
                    DateTimeOffset.Now.AddHours(-hours), bucketSeconds);

                // SQL on jo harventanut rivit (+ raakapäätepisteet ja
                // null-katkosrivit), joten builderin ei pidä harventaa niitä
                // uudelleen — muuten se ryhmittäisi rivit pareittain ja voisi
                // pudottaa null-katkokset ja vetää viivan aukon yli. maxPoints
                // ≥ rivimäärä pitää builderin harvennuksen pois päältä.
                int maxPoints = Math.Max(ChartMaxPoints, rows.Count);
                ChartHistory history =
                    ChartHistoryBuilder.Build(rows, maxPoints, fanLabels);
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    // Vanhentuneen aikavälin tulosta ei sovelleta: valitsin
                    // näyttäisi uutta aluetta mutta graafi palaisi vanhaan.
                    if (History.RangeHours == hours)
                    {
                        History.Apply(history, hours);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"VIRHE historiagraafien haussa: {ex.Message}");
            }
            finally
            {
                bool pending = _historyRefreshPending;
                _historyRefreshPending = false;
                Interlocked.Exchange(ref _historyRefreshRunning, 0);

                // Jos aikaväli vaihtui haun aikana (myös ABA), haetaan uudelleen.
                if (pending || History.RangeHours != hours)
                {
                    RefreshHistoryInBackground();
                }
            }
        });
    }

    /// <summary>Tuulettimen tunniste → käyttäjän nimilappu (graafien selitteisiin).</summary>
    private Dictionary<string, string> BuildFanLabelMap()
    {
        // Nimilaput ovat jo tunnisteavaimisia (AppSettings.FanLabels), joten
        // kopioidaan vain ei-tyhjät. Graafibuilder avaintaa tuulettimet
        // tunnisteella, joten kartta on suoraan yhteensopiva.
        var map = new Dictionary<string, string>();
        foreach ((string identifier, string label) in _settings.FanLabels)
        {
            if (label.Length > 0)
            {
                map[identifier] = label;
            }
        }

        return map;
    }

    private void OnOverlaySettingChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            _logger.Log($"VIRHE asetusten tallennuksessa: {ex.Message}");
        }

        OverlaySettingsChanged?.Invoke();
    }

    public void Dispose()
    {
        _timer.Stop();

        try
        {
            _events?.Info("App", UiStrings.Event_AppClosed);
        }
        catch
        {
            // Sammutus ei saa kaatua lokitukseen.
        }

        try
        {
            // Erillään tapahtumakirjoituksesta: siisti sulkeminen merkitään
            // vaikka SQLite-kirjoitus epäonnistuisi — muuten seuraava
            // käynnistys raportoisi normaalin sulkemisen kaatumisena.
            _lastState.MarkCleanShutdown();
        }
        catch
        {
        }

        // Odota jonossa olevat DB-kirjoitukset (sample, last_state) valmiiksi
        // ENNEN kannan sulkemista — viimeinen kirjoitus ei saa hukkua siihen,
        // että yhteys suljetaan sen odottaessa lukkoa.
        Task[] pending;
        lock (_dbWriteLock)
        {
            pending = _dbWrites.ToArray();
        }

        try
        {
            Task.WaitAll(pending, TimeSpan.FromSeconds(3));
        }
        catch
        {
        }

        _historyDb?.Dispose();
        _sensorService.Dispose();
        _logger.Dispose(); // tyhjentää taustalokijonon
    }

    private static readonly PropertyChangedEventArgs StatusChangedArgs = new(nameof(Status));

    public event PropertyChangedEventHandler? PropertyChanged;
}
