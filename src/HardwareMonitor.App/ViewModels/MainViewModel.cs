using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using HardwareMonitor.Core.Logging;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Sensors;
using HardwareMonitor.Core.Settings;

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

    private string _status = "Käynnistetään sensorit…";
    private int _tickCount;

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        Dashboard.RenameFan = RenameFan;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
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

    /// <summary>Laukeaa kun mikä tahansa overlay-asetus muuttuu (tallennus + ikkunan päivitys).</summary>
    public event Action? OverlaySettingsChanged;

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
            Refresh();
            _timer.Start();
        }
        catch (Exception ex)
        {
            Status = $"Virhe sensorien käynnistyksessä: {ex.Message}";
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

            if (Hardware.Count == 0)
            {
                BuildTree(groups);
            }
            else
            {
                UpdateValues(groups);
            }

            _tickCount++;
            Status =
                $"Päivitetty {DateTime.Now:HH:mm:ss}  —  " +
                $"{_sensorIndex.Count} sensoria, {Hardware.Count} laitetta  (päivitys #{_tickCount})";

            LogSnapshot(groups);
        }
        catch (Exception ex)
        {
            Status = $"Virhe päivityksessä: {ex.Message}";
            _logger.Log($"VIRHE päivityksessä: {ex}");
        }
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
        _sensorService.Dispose();
    }

    private static readonly PropertyChangedEventArgs StatusChangedArgs = new(nameof(Status));

    public event PropertyChangedEventHandler? PropertyChanged;
}
