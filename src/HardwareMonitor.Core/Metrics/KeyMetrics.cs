namespace HardwareMonitor.Core.Metrics;

/// <summary>Yhden levyn tärkeimmät arvot (nimi, lämpö, aktiivisuus).</summary>
public sealed record DiskMetrics(string Name, float? TemperatureC, float? ActivityPercent);

/// <summary>Yhden tuulettimen nimi ja kierrosnopeus.</summary>
public sealed record FanMetrics(string Name, float? Rpm);

/// <summary>
/// Tärkeimmät arvot valmiiksi poimittuina (specin Vaihe 2 / 2.5). Kaikki arvot
/// ovat nullable: puuttuva sensori ei ole virhe, vaan UI näyttää "—".
/// Dashboard ja overlay käyttävät molemmat tätä samaa oliota.
/// </summary>
public sealed record KeyMetrics(
    float? CpuLoadPercent,
    float? CpuPackageTempC,
    float? CpuMaxClockMhz,
    float? CpuPackagePowerW,
    float? GpuLoadPercent,
    float? GpuTempC,
    float? GpuHotspotTempC,
    float? GpuMemoryUsedMb,
    float? GpuMemoryTotalMb,
    float? GpuPowerW,
    float? RamLoadPercent,
    float? RamUsedGb,
    float? RamAvailableGb,
    IReadOnlyList<DiskMetrics> Disks,
    IReadOnlyList<FanMetrics> Fans);
