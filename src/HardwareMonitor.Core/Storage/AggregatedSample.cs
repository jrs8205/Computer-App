namespace HardwareMonitor.Core.Storage;

/// <summary>Yhden mittarin kooste lokitusjaksolta. Null = ei yhtään lukemaa.</summary>
public sealed record MetricAggregate(float? Min, float? Avg, float? Max)
{
    public static readonly MetricAggregate Empty = new(null, null, null);
}

/// <summary>
/// Yhden levyn kooste; Identifier on LHM:n pysyvä laitetunniste (samannimiset
/// levyt pysyvät erillään myös DB:ssä), Index säilyttää järjestyksen.
/// </summary>
public sealed record DiskAggregate(
    int Index, string Name, MetricAggregate TempC, float? ActivityMaxPercent,
    string Identifier = "");

/// <summary>Yhden tuulettimen kooste; täsmäys pysyvällä tunnisteella.</summary>
public sealed record FanAggregate(string Identifier, string Name, MetricAggregate Rpm);

/// <summary>
/// Yksi kantarivi: lokitusjakson (oletus 5 s) min/keskiarvo/max-kooste
/// 1 s -lukemista. Maksimit säilyttävät sekunnin piikit, vaikka rivejä
/// syntyy vain joka N:s sekunti.
/// </summary>
public sealed record AggregatedSample(
    DateTimeOffset Timestamp,
    MetricAggregate CpuLoad,
    MetricAggregate CpuTemp,
    float? CpuClockMax,
    MetricAggregate CpuPower,
    MetricAggregate GpuLoad,
    MetricAggregate GpuTemp,
    MetricAggregate GpuHotspot,
    MetricAggregate GpuPower,
    MetricAggregate VramUsedMb,
    MetricAggregate RamLoad,
    MetricAggregate RamUsedGb,
    IReadOnlyList<DiskAggregate> Disks,
    IReadOnlyList<FanAggregate> Fans);
