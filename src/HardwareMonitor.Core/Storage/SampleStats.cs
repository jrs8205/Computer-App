namespace HardwareMonitor.Core.Storage;

/// <summary>Yhden mittarin kooste historiasta: keskiarvo ja huippu.</summary>
public sealed record MetricStat(double? Avg, double? Max);

public sealed record DiskStat(string Name, double? TempAvg, double? TempMax);

public sealed record FanStat(
    string Name, double? RpmAvg, double? RpmMax, string Identifier = "");

/// <summary>
/// Aikavälin koosteet samples-tauluista riskianalyysia ja konetuntemus-lokia
/// varten. Avg on koosterivien keskiarvojen keskiarvo, Max on maksimien maksimi.
/// </summary>
public sealed record SampleStats(
    long SampleCount,
    MetricStat CpuTemp,
    MetricStat CpuLoad,
    MetricStat GpuTemp,
    MetricStat GpuHotspot,
    MetricStat RamLoad,
    IReadOnlyList<DiskStat> Disks,
    IReadOnlyList<FanStat> Fans);
