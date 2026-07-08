using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor.Core.Sensors;

/// <summary>
/// Vastaa koneen sensorien lukemisesta LibreHardwareMonitorLib-kirjaston avulla.
/// Tämä on ohjelman keskeisin palvelu (SensorService suunnitelman luvusta 23).
///
/// Käyttö:
///   using var service = new SensorService();
///   service.Start();
///   IReadOnlyList&lt;HardwareGroup&gt; snapshot = service.Read();
///
/// Huom: monet matalan tason sensorit (CPU-lämpö, tuulettimet, jännitteet)
/// näkyvät vain, jos ohjelma ajetaan järjestelmänvalvojan oikeuksilla.
/// </summary>
public sealed class SensorService : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private bool _opened;

    public SensorService()
    {
        // Otetaan kaikki laiteryhmät käyttöön. MVP:ssä keskitytään lukemiseen,
        // ei ohjaukseen, joten emme muuta koneen asetuksia.
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true,
            IsBatteryEnabled = true,
        };
    }

    /// <summary>Avaa yhteyden laitteistoon. Turvallinen kutsua monta kertaa.</summary>
    public void Start()
    {
        if (_opened)
        {
            return;
        }

        _computer.Open();
        _opened = true;
    }

    /// <summary>
    /// Lukee tuoreen tilannekuvan kaikista sensoreista. Päivittää ensin laitteet
    /// ja palauttaa sitten lukemat laiteryhmittäin.
    /// </summary>
    public IReadOnlyList<HardwareGroup> Read()
    {
        if (!_opened)
        {
            Start();
        }

        _computer.Accept(_visitor);

        var groups = new List<HardwareGroup>();
        foreach (IHardware hardware in _computer.Hardware)
        {
            groups.Add(ReadHardware(hardware));
        }

        return groups;
    }

    private static HardwareGroup ReadHardware(IHardware hardware)
    {
        var readings = new List<SensorReading>();
        foreach (ISensor sensor in hardware.Sensors)
        {
            readings.Add(ToReading(hardware, sensor));
        }

        var subGroups = new List<HardwareGroup>();
        foreach (IHardware sub in hardware.SubHardware)
        {
            subGroups.Add(ReadHardware(sub));
        }

        return new HardwareGroup(
            hardware.Name,
            hardware.HardwareType.ToString(),
            readings,
            subGroups);
    }

    private static SensorReading ToReading(IHardware hardware, ISensor sensor) =>
        new(
            HardwareName: hardware.Name,
            HardwareType: hardware.HardwareType.ToString(),
            SensorName: sensor.Name,
            SensorType: sensor.SensorType.ToString(),
            Value: sensor.Value,
            Unit: UnitFor(sensor.SensorType),
            Identifier: sensor.Identifier.ToString());

    /// <summary>Ihmisluettava yksikkö sensorityypin perusteella.</summary>
    public static string UnitFor(SensorType type) => type switch
    {
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Power => "W",
        SensorType.Clock => "MHz",
        SensorType.Temperature => "°C",
        SensorType.Load => "%",
        SensorType.Frequency => "Hz",
        SensorType.Fan => "RPM",
        SensorType.Flow => "L/h",
        SensorType.Control => "%",
        SensorType.Level => "%",
        SensorType.Factor => "",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Throughput => "B/s",
        SensorType.TimeSpan => "s",
        SensorType.Energy => "mWh",
        SensorType.Noise => "dBA",
        _ => string.Empty,
    };

    public void Dispose()
    {
        if (_opened)
        {
            _computer.Close();
            _opened = false;
        }
    }
}
