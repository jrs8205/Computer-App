using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using HardwareMonitor.Core.Logging;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Sensors;

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

    private string _status = "Käynnistetään sensorit…";
    private int _tickCount;

    public MainViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
    }

    public ObservableCollection<HardwareViewModel> Hardware { get; } = new();

    public DashboardViewModel Dashboard { get; } = new();

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
            Dashboard.Update(metrics);

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

    public void Dispose()
    {
        _timer.Stop();
        _sensorService.Dispose();
    }

    private static readonly PropertyChangedEventArgs StatusChangedArgs = new(nameof(Status));

    public event PropertyChangedEventHandler? PropertyChanged;
}
