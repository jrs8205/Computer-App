using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Storage;

public class SampleAggregatorTests
{
    private static KeyMetrics Metrics(
        float? cpuLoad = null, float? cpuTemp = null, float? cpuClock = null,
        DiskMetrics[]? disks = null, FanMetrics[]? fans = null) =>
        new(
            CpuLoadPercent: cpuLoad,
            CpuPackageTempC: cpuTemp,
            CpuMaxClockMhz: cpuClock,
            CpuPackagePowerW: null,
            GpuLoadPercent: null,
            GpuTempC: null,
            GpuHotspotTempC: null,
            GpuMemoryUsedMb: null,
            GpuMemoryTotalMb: null,
            GpuPowerW: null,
            RamLoadPercent: null,
            RamUsedGb: null,
            RamAvailableGb: null,
            Disks: disks ?? Array.Empty<DiskMetrics>(),
            Fans: fans ?? Array.Empty<FanMetrics>());

    private static readonly DateTimeOffset T0 = new(2026, 7, 8, 20, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Add_PalauttaaNullKunnesKoosteValmis_JaNollaaLaskurin()
    {
        var agg = new SampleAggregator(samplesPerRow: 3);

        Assert.Null(agg.Add(Metrics(cpuLoad: 10), T0));
        Assert.Null(agg.Add(Metrics(cpuLoad: 20), T0));
        Assert.NotNull(agg.Add(Metrics(cpuLoad: 30), T0));
        Assert.Null(agg.Add(Metrics(cpuLoad: 40), T0)); // uusi jakso alkaa
    }

    [Fact]
    public void Add_LaskeeMinAvgMax_JaOhittaaNullit()
    {
        var agg = new SampleAggregator(samplesPerRow: 3);
        agg.Add(Metrics(cpuLoad: 10, cpuTemp: 50), T0);
        agg.Add(Metrics(cpuLoad: 30, cpuTemp: null), T0); // temp puuttuu välillä
        AggregatedSample s = agg.Add(Metrics(cpuLoad: 50, cpuTemp: 90), T0)!;

        Assert.Equal(10, s.CpuLoad.Min);
        Assert.Equal(30, s.CpuLoad.Avg);
        Assert.Equal(50, s.CpuLoad.Max);
        Assert.Equal(50, s.CpuTemp.Min);
        Assert.Equal(90, s.CpuTemp.Max); // piikki säilyy vaikka välissä null
        Assert.Equal(T0, s.Timestamp);
    }

    [Fact]
    public void Add_KaikkiNull_TuottaaTyhjanAggregaatin()
    {
        var agg = new SampleAggregator(samplesPerRow: 2);
        agg.Add(Metrics(), T0);
        AggregatedSample s = agg.Add(Metrics(), T0)!;

        Assert.Null(s.CpuTemp.Min);
        Assert.Null(s.CpuTemp.Avg);
        Assert.Null(s.CpuTemp.Max);
        Assert.Null(s.CpuClockMax);
    }

    [Fact]
    public void Add_CpuKellostaTallentuuMax()
    {
        var agg = new SampleAggregator(samplesPerRow: 2);
        agg.Add(Metrics(cpuClock: 4600), T0);
        AggregatedSample s = agg.Add(Metrics(cpuClock: 4900), T0)!;

        Assert.Equal(4900, s.CpuClockMax);
    }

    [Fact]
    public void Add_LevytTasmataanIndeksilla_TuulettimetTunnisteella()
    {
        var agg = new SampleAggregator(samplesPerRow: 2);
        agg.Add(Metrics(
            disks: new[] { new DiskMetrics("NVMe", 60f, 5f) },
            fans: new[] { new FanMetrics("Fan #2", 1950f, "/fan/2") }), T0);
        AggregatedSample s = agg.Add(Metrics(
            disks: new[] { new DiskMetrics("NVMe", 70f, 15f) },
            fans: new[] { new FanMetrics("Fan #2", 1960f, "/fan/2") }), T0)!;

        DiskAggregate disk = Assert.Single(s.Disks);
        Assert.Equal("NVMe", disk.Name);
        Assert.Equal(0, disk.Index);
        Assert.Equal(60, disk.TempC.Min);
        Assert.Equal(70, disk.TempC.Max);
        Assert.Equal(15, disk.ActivityMaxPercent);

        FanAggregate fan = Assert.Single(s.Fans);
        Assert.Equal("/fan/2", fan.Identifier);
        Assert.Equal(1950, fan.Rpm.Min);
        Assert.Equal(1960, fan.Rpm.Max);
    }
}
