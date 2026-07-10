namespace HardwareMonitor.Core.Insights;

/// <summary>Koneen kokoonpano machine-insights.md:n Kokoonpano-osioon.</summary>
public sealed record MachineSpec(
    string? CpuName,
    string? GpuName,
    string? MotherboardName,
    int? RamTotalGb,
    IReadOnlyList<string> DiskNames,
    string OsDescription,
    string UserNotes);
