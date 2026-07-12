namespace HardwareMonitor.Core.Sensors;

/// <summary>
/// Yhden laitteen (esim. CPU, GPU, levy) sensorit yhtenä ryhmänä.
/// Laitteilla voi olla alalaitteita (SubHardware), esim. emolevyn alla oleva
/// sensoripiiri tai GPU:n alla oleva muistiohjain. Identifier on LHM:n pysyvä
/// laitetunniste (esim. "/nvme/0") — näyttönimi voi toistua, tunniste ei.
/// </summary>
public sealed record HardwareGroup(
    string Name,
    string HardwareType,
    IReadOnlyList<SensorReading> Sensors,
    IReadOnlyList<HardwareGroup> SubHardware,
    string Identifier = "");
