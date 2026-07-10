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
    private bool _previousSessionCrashed;
    private IReadOnlyList<EventRow> _recentEvents = Array.Empty<EventRow>();
    private SampleStats? _dayStats;
    private KeyMetrics? _latestMetrics;
    private MetricStates? _latestStates;
    private RiskAssessment? _latestAssessment;
    private int _windowsScanRunning;
    private int _analysisRefreshRunning;
    private int _insightsWriteRunning;
    private int _historyRefreshRunning;
    private int _rowsLogged;

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

            if (AutostartService.SetEnabled(value))
            {
                _autoStart = value;
            }
            else
            {
                _logger.Log("VIRHE: automaattikäynnistyksen muutos epäonnistui (vaatii admin-oikeudet).");
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoStart)));
        }
    }

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
            // taustalla, ettei schtasks-kutsu viivytä käynnistystä.
            Task.Run(AutostartService.RefreshIfEnabled);
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
            _latestAssessment = assessment;

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
                Task.Run(() =>
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
                Task.Run(() =>
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
            _latestStates is not { } states ||
            _latestAssessment is not { } assessment)
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        return ReportBuilder.Build(
            now, assessment, metrics, states,
            _historyDb?.GetSampleStats(now.AddHours(-24)) ?? EmptyStats,
            _historyDb?.GetSampleStats(now.AddDays(-30)) ?? EmptyStats,
            _historyDb?.ReadEventsSince(now.AddHours(-24)) ?? Array.Empty<EventRow>(),
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

    private void WriteMachineInsightsInBackground()
    {
        if (_historyDb is not { } db ||
            Interlocked.CompareExchange(ref _insightsWriteRunning, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                DateTimeOffset now = DateTimeOffset.Now;
                string markdown = MachineInsightsBuilder.Build(new MachineInsightsInput(
                    now,
                    MachineSpecReader.Read(Array.Empty<HardwareGroup>(), "", ""),
                    db.GetSampleStats(now.AddDays(-30)),
                    db.GetSampleStats(now.AddDays(-7)),
                    db.ReadEventsSince(now.AddDays(-30)),
                    _settings.Thresholds));
                File.WriteAllText(MachineInsightsPath, markdown);
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

        foreach (HardwareGroup group in groups)
        {
            newSensorAppeared |= UpdateGroup(group);
        }

        // Jos laitteita tuli lisää ajon aikana (esim. USB-levy), rakennetaan puu uudelleen.
        if (newSensorAppeared)
        {
            BuildTree(groups);
        }
    }

    private bool UpdateGroup(HardwareGroup group)
    {
        bool newSensorAppeared = false;

        foreach (SensorReading reading in group.Sensors)
        {
            if (_sensorIndex.TryGetValue(reading.Identifier, out SensorViewModel? sensorVm))
            {
                sensorVm.Update(reading);
            }
            else
            {
                newSensorAppeared = true;
            }
        }

        foreach (HardwareGroup sub in group.SubHardware)
        {
            newSensorAppeared |= UpdateGroup(sub);
        }

        return newSensorAppeared;
    }

    private void LogSnapshot(IReadOnlyList<HardwareGroup> groups)
    {
        // Kirjataan koko sensorilista debug-lokiin harvakseltaan (n. 10 s välein),
        // jottei tiedosto kasva liian nopeasti. Tämä on vain PoC-tason lokitus;
        // varsinainen SQLite-sensoriloki tulee myöhemmässä vaiheessa.
        if (_tickCount % 10 != 1)
        {
            return;
        }

        _logger.Log($"--- Sensoritilanne (päivitys #{_tickCount}) ---");
        foreach (HardwareGroup group in groups)
        {
            LogGroup(group, indent: 0);
        }
    }

    private void LogGroup(HardwareGroup group, int indent)
    {
        string pad = new(' ', indent * 2);
        _logger.Log($"{pad}[{group.HardwareType}] {group.Name}");

        foreach (SensorReading reading in group.Sensors)
        {
            string value = reading.Value.HasValue
                ? $"{reading.Value.Value:0.###} {reading.Unit}".Trim()
                : "—";
            _logger.Log($"{pad}  {reading.SensorType,-12} {reading.SensorName}: {value}");
        }

        foreach (HardwareGroup sub in group.SubHardware)
        {
            LogGroup(sub, indent + 1);
        }
    }

    /// <summary>Hakee historiagraafien datan taustalla; päällekkäisyys estetty.</summary>
    private void RefreshHistoryInBackground()
    {
        if (_historyDb is not { } db
            || Interlocked.Exchange(ref _historyRefreshRunning, 1) == 1)
        {
            return;
        }

        int hours = History.RangeHours;
        Task.Run(() =>
        {
            try
            {
                IReadOnlyList<SampleRow> rows =
                    db.ReadSampleRows(DateTimeOffset.Now.AddHours(-hours));
                ChartHistory history =
                    ChartHistoryBuilder.Build(rows, 500, BuildFanLabelMap());
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                    () => History.Apply(history, hours));
            }
            catch (Exception ex)
            {
                _logger.Log($"VIRHE historiagraafien haussa: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _historyRefreshRunning, 0);
            }
        });
    }

    /// <summary>Tuulettimen raakanimi → käyttäjän nimilappu (graafien selitteisiin).</summary>
    private Dictionary<string, string> BuildFanLabelMap()
    {
        var map = new Dictionary<string, string>();
        if (_latestMetrics is { } m)
        {
            foreach (FanMetrics fan in m.Fans)
            {
                if (_settings.FanLabels.TryGetValue(fan.Identifier, out string? label)
                    && label.Length > 0)
                {
                    map[fan.Name] = label;
                }
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
            _lastState.MarkCleanShutdown();
        }
        catch
        {
            // Sammutus ei saa kaatua lokitukseen.
        }

        _historyDb?.Dispose();
        _sensorService.Dispose();
    }

    private static readonly PropertyChangedEventArgs StatusChangedArgs = new(nameof(Status));

    public event PropertyChangedEventHandler? PropertyChanged;
}
