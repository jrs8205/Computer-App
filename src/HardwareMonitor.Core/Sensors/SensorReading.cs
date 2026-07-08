namespace HardwareMonitor.Core.Sensors;

/// <summary>
/// Yksittäinen sensorilukema yhdeltä ajanhetkeltä. Muuttumaton tietue (record),
/// jotta lukemia on turvallista siirtää säikeeltä toiselle ja tallentaa historiaan.
/// </summary>
/// <param name="HardwareName">Laitteen nimi, esim. "AMD Ryzen 7 5800X".</param>
/// <param name="HardwareType">Laitetyyppi, esim. "Cpu", "GpuNvidia", "Storage".</param>
/// <param name="SensorName">Sensorin nimi, esim. "Core (Tctl/Tdie)".</param>
/// <param name="SensorType">Sensorityyppi, esim. "Temperature", "Load", "Fan".</param>
/// <param name="Value">Mitattu arvo. Null jos sensori ei juuri nyt anna arvoa.</param>
/// <param name="Unit">Ihmisluettava yksikkö, esim. "°C", "%", "RPM", "W".</param>
/// <param name="Identifier">Yksilöivä tunniste, jolla sama sensori löytyy päivityksestä toiseen.</param>
public sealed record SensorReading(
    string HardwareName,
    string HardwareType,
    string SensorName,
    string SensorType,
    float? Value,
    string Unit,
    string Identifier);
