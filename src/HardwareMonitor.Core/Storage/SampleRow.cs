namespace HardwareMonitor.Core.Storage;

public sealed record DiskSampleValue(
    string Name, double? TempAvg, double? TempMax, double? TempMin = null);

public sealed record FanSampleValue(string Name, double? RpmAvg, double? RpmMin = null);

/// <summary>
/// Yksi 5 s kooste lapsiriveineen CSV-vientiä varten (määrittelyn luku 21).
/// Sarakkeet vastaavat samples-taulua; levyt ja tuulettimet nimillään.
/// Min-arvot ovat null ennen min-sarakkeiden lisäystä kirjatuilla riveillä.
/// </summary>
public sealed record SampleRow(
    DateTimeOffset Timestamp,
    double? CpuLoadAvg, double? CpuLoadMax,
    double? CpuTempAvg, double? CpuTempMax,
    double? CpuClockMax,
    double? CpuPowerAvg, double? CpuPowerMax,
    double? GpuLoadAvg, double? GpuLoadMax,
    double? GpuTempAvg, double? GpuTempMax,
    double? GpuHotspotAvg, double? GpuHotspotMax,
    double? GpuPowerAvg, double? GpuPowerMax,
    double? VramUsedMbAvg, double? VramUsedMbMax,
    double? RamLoadAvg, double? RamLoadMax,
    double? RamUsedGbAvg, double? RamUsedGbMax,
    IReadOnlyList<DiskSampleValue> Disks,
    IReadOnlyList<FanSampleValue> Fans,
    double? CpuLoadMin = null, double? CpuTempMin = null,
    double? CpuPowerMin = null,
    double? GpuLoadMin = null, double? GpuTempMin = null,
    double? GpuHotspotMin = null, double? GpuPowerMin = null,
    double? VramUsedMbMin = null,
    double? RamLoadMin = null, double? RamUsedGbMin = null);
