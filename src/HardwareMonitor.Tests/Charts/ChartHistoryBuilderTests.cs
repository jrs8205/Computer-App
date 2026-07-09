using HardwareMonitor.Core.Charts;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Charts;

public class ChartHistoryBuilderTests
{
    private static readonly Dictionary<string, string> NoLabels = new();
    private static readonly DateTimeOffset T0 = new(2026, 7, 9, 20, 0, 0, TimeSpan.FromHours(3));

    private static SampleRow Row(
        int offsetSeconds, double? cpuTemp = null, double? cpuLoad = null,
        double? ramLoad = null,
        DiskSampleValue[]? disks = null, FanSampleValue[]? fans = null) =>
        new(T0.AddSeconds(offsetSeconds),
            cpuLoad, null,
            cpuTemp, null,
            null,
            null, null,
            null, null,
            null, null,
            null, null,
            null, null,
            null, null,
            ramLoad, null,
            null, null,
            disks ?? Array.Empty<DiskSampleValue>(),
            fans ?? Array.Empty<FanSampleValue>());

    private static ChartSeries Cpu(ChartHistory h) =>
        h.Temperatures.First(s => s.Name == "CPU");

    [Fact]
    public void AlleMaxPoints_PalautuuSellaisenaan()
    {
        var rows = new[] { Row(0, cpuTemp: 40), Row(5, cpuTemp: 50), Row(10, cpuTemp: 60) };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.Equal(3, cpu.Points.Count);
        Assert.Equal(new double?[] { 40, 50, 60 }, cpu.Points.Select(p => p.Value));
        Assert.Equal(T0, cpu.Points[0].Timestamp);
    }

    [Fact]
    public void Harvennus_RajoittaaPistemaaranJaSailyttaaPaatepisteet()
    {
        SampleRow[] rows = Enumerable.Range(0, 100)
            .Select(i => Row(i * 5, cpuTemp: 50)).ToArray();

        ChartHistory h = ChartHistoryBuilder.Build(rows, 10, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.True(cpu.Points.Count <= 10);
        Assert.Equal(T0, cpu.Points[0].Timestamp);
        Assert.Equal(T0.AddSeconds(99 * 5), cpu.Points[^1].Timestamp);
    }

    [Fact]
    public void Harvennus_LaskeeBucketKeskiarvon()
    {
        var rows = new[]
        {
            Row(0, cpuTemp: 40), Row(5, cpuTemp: 60),
            Row(10, cpuTemp: 10), Row(15, cpuTemp: 30),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 2, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.Equal(2, cpu.Points.Count);
        Assert.Equal(50, cpu.Points[0].Value);
        Assert.Equal(20, cpu.Points[1].Value);
    }

    [Fact]
    public void NullAukko_Sailyy()
    {
        var rows = new[] { Row(0, cpuTemp: 40), Row(5), Row(10, cpuTemp: 60) };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        Assert.Null(Cpu(h).Points[1].Value);
    }

    [Fact]
    public void SamannimisetLevyt_ErotellaanJarjestysnumerolla()
    {
        // Käyttäjän koneessa on oikeasti kaksi samannimistä 860 EVO:ta.
        var rows = new[]
        {
            Row(0, disks: new[]
            {
                new DiskSampleValue("Samsung SSD 860 EVO 1TB ", 30, 31),
                new DiskSampleValue("Samsung SSD 860 EVO 1TB ", 35, 36),
            }),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries d1 = h.Temperatures.First(s => s.Name == "Samsung SSD 860 EVO 1TB #1");
        ChartSeries d2 = h.Temperatures.First(s => s.Name == "Samsung SSD 860 EVO 1TB #2");
        Assert.Equal(30, d1.Points[0].Value);
        Assert.Equal(35, d2.Points[0].Value);
    }

    [Fact]
    public void PuuttuvaLevy_TayttyyNullilla()
    {
        var rows = new[]
        {
            Row(0, disks: new[] { new DiskSampleValue("NVMe", 55, 56) }),
            Row(5),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries disk = h.Temperatures.First(s => s.Name == "NVMe");
        Assert.Equal(55, disk.Points[0].Value);
        Assert.Null(disk.Points[1].Value);
    }

    [Fact]
    public void Tuuletin_SaaNimilapun()
    {
        var rows = new[] { Row(0, fans: new[] { new FanSampleValue("Fan #2", 1950) }) };
        var labels = new Dictionary<string, string> { ["Fan #2"] = "AIO-pumppu" };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, labels);

        ChartSeries fan = Assert.Single(h.Fans);
        Assert.Equal("AIO-pumppu", fan.Name);
        Assert.Equal(1950, fan.Points[0].Value);
    }

    [Fact]
    public void NollaTuuletin_Suodattuu()
    {
        var rows = new[]
        {
            Row(0, fans: new[] { new FanSampleValue("Fan #5", 0), new FanSampleValue("Fan #1", 600) }),
            Row(5, fans: new[] { new FanSampleValue("Fan #5", null), new FanSampleValue("Fan #1", 620) }),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries fan = Assert.Single(h.Fans);
        Assert.Equal("Fan #1", fan.Name);
    }

    [Fact]
    public void HarvoinPyorahtavaTuuletin_Suodattuu()
    {
        // GPU-tuulettimet pyörähtävät hetkeksi silloin tällöin — alle 5 %
        // ajasta pyörivä tuuletin ei ansaitse omaa sarjaa (käyttäjän palaute).
        SampleRow[] rows = Enumerable.Range(0, 100).Select(i =>
            Row(i * 5, fans: new[]
            {
                new FanSampleValue("GPU Fan 1", i < 2 ? 1500 : 0),
                new FanSampleValue("Fan #1", 600),
            })).ToArray();

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries fan = Assert.Single(h.Fans);
        Assert.Equal("Fan #1", fan.Name);
    }

    [Fact]
    public void Kuormasarjat_Loytyvat()
    {
        var rows = new[] { Row(0, cpuLoad: 10, ramLoad: 20) };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        Assert.Equal(10, h.Loads.First(s => s.Name == "CPU").Points[0].Value);
        Assert.Equal(20, h.Loads.First(s => s.Name == "RAM").Points[0].Value);
        Assert.Null(h.Loads.First(s => s.Name == "GPU").Points[0].Value);
    }
}
