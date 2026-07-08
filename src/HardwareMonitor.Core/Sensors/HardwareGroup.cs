namespace HardwareMonitor.Core.Sensors;

/// <summary>
/// Yhden laitteen (esim. CPU, GPU, levy) sensorit yhtenä ryhmänä.
/// Laitteilla voi olla alalaitteita (SubHardware), esim. emolevyn alla oleva
/// sensoripiiri tai GPU:n alla oleva muistiohjain.
/// </summary>
public sealed record HardwareGroup(
    string Name,
    string HardwareType,
    IReadOnlyList<SensorReading> Sensors,
    IReadOnlyList<HardwareGroup> SubHardware);
