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
    public void Harvennus_LaskeeBucketKeskiarvon_PaatepisteetArvoineen()
    {
        // Väliin jäävät pisteet ovat bucket-keskiarvoja; ensimmäinen ja
        // viimeinen piste saavat datan päätepisteiden ARVOT aikaleimojen
        // lisäksi — muuten alun tooltip näyttäisi tuntien keskiarvon.
        var rows = new[]
        {
            Row(0, cpuTemp: 40), Row(5, cpuTemp: 60),
            Row(10, cpuTemp: 10), Row(15, cpuTemp: 30),
            Row(20, cpuTemp: 80), Row(25, cpuTemp: 90),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 3, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.Equal(3, cpu.Points.Count);
        Assert.Equal(40, cpu.Points[0].Value);  // rows[0]:n arvo
        Assert.Equal(20, cpu.Points[1].Value);  // bucket-keskiarvo (10+30)/2
        Assert.Equal(90, cpu.Points[^1].Value); // rows[^1]:n arvo
    }

    [Fact]
    public void JoHarvennettuNullKatkos_SailyyKunEiHarvennetaUudelleen()
    {
        // SQL-harvennettu tulos sisältää valmiit null-katkosrivit; kun
        // maxPoints ≥ rivimäärä, builder ei ryhmitä niitä uudelleen vaan
        // säilyttää katkoksen (MainViewModel luottaa tähän).
        var rows = new[]
        {
            Row(0, cpuTemp: 40),
            Row(60),                 // kaikki null = katkos
            Row(120, cpuTemp: 50),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, rows.Length, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.Equal(3, cpu.Points.Count);
        Assert.Null(cpu.Points[1].Value);
    }

    [Fact]
    public void PaatepisteenPuuttuvaArvo_SailyyAukkona()
    {
        // Jos päätepisterivin mittaus puuttuu, pisteen pitää jäädä nulliksi —
        // bucket-keskiarvo olisi keksitty mittaus aukon kohdalle.
        var rows = new[]
        {
            Row(0), Row(5, cpuTemp: 60),
            Row(10, cpuTemp: 10), Row(15, cpuTemp: 30),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 2, NoLabels);

        Assert.Null(Cpu(h).Points[0].Value);
    }

    [Fact]
    public void Sammutusjakso_TuottaaKatkonViivaan()
    {
        // Kone oli sammuksissa rivien välissä — kannassa ei ole null-riviä
        // vaan pelkkä aikaleimaväli, eikä viivaa saa vetää aukon yli.
        var rows = new List<SampleRow>();
        for (int i = 0; i < 10; i++)
        {
            rows.Add(Row(i * 60, cpuTemp: 40));
        }

        for (int i = 0; i < 10; i++)
        {
            rows.Add(Row(7200 + i * 60, cpuTemp: 50));
        }

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.Equal(21, cpu.Points.Count); // 20 riviä + 1 katkospiste
        ChartPoint gap = cpu.Points[10];
        Assert.Null(gap.Value);
        Assert.True(gap.Timestamp > T0.AddSeconds(9 * 60));
        Assert.True(gap.Timestamp < T0.AddSeconds(7200));
    }

    [Fact]
    public void HarvoinPyoriva_SuodattuuRaakarivienMaaralla()
    {
        // Painotus raakarivien lukumäärillä: yksi bucket 99 pysähtynyttä riviä,
        // toinen 1 pyörivä rivi → osuuksien keskiarvo (0 ja 1) olisi 50 %,
        // mutta todellinen osuus 1/100 = 1 % < 5 % → GPU Fan suodattuu.
        var rows = new[]
        {
            Row(0, fans: new[]
            {
                new FanSampleValue("GPU Fan 1", 0, Identifier: "/gpu/fan/1",
                    SpinningRows: 0, KnownRows: 99),
                new FanSampleValue("Fan #1", 600, Identifier: "/mb/fan/1",
                    SpinningRows: 99, KnownRows: 99),
            }),
            Row(300, fans: new[]
            {
                new FanSampleValue("GPU Fan 1", 8, Identifier: "/gpu/fan/1",
                    SpinningRows: 1, KnownRows: 1),
                new FanSampleValue("Fan #1", 600, Identifier: "/mb/fan/1",
                    SpinningRows: 1, KnownRows: 1),
            }),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries fan = Assert.Single(h.Fans);
        Assert.Equal("Fan #1", fan.Name);
    }

    [Fact]
    public void SamannimisetEriTunnisteet_OvatEriSarjat()
    {
        // Emolevyn ja GPU:n "Fan #1" ovat eri tuulettimia — eivät saa sulautua.
        var rows = new[]
        {
            Row(0, fans: new[]
            {
                new FanSampleValue("Fan #1", 600, Identifier: "/mb/fan/1",
                    SpinningRows: 1, KnownRows: 1),
                new FanSampleValue("Fan #1", 1500, Identifier: "/gpu/fan/1",
                    SpinningRows: 1, KnownRows: 1),
            }),
        };
        var labels = new Dictionary<string, string> { ["/gpu/fan/1"] = "GPU-tuuletin" };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, labels);

        Assert.Equal(2, h.Fans.Count);
        Assert.Contains(h.Fans, s => s.Name == "Fan #1");       // emolevy, ei lappua
        Assert.Contains(h.Fans, s => s.Name == "GPU-tuuletin"); // lappu tunnisteella
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
        var rows = new[]
        {
            Row(0, fans: new[] { new FanSampleValue("Fan #2", 1950, Identifier: "/fan/2") }),
        };
        var labels = new Dictionary<string, string> { ["/fan/2"] = "AIO-pumppu" };

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
            Row(0, fans: new[]
            {
                new FanSampleValue("Fan #5", 0, Identifier: "/fan/5"),
                new FanSampleValue("Fan #1", 600, Identifier: "/fan/1"),
            }),
            Row(5, fans: new[]
            {
                new FanSampleValue("Fan #5", null, Identifier: "/fan/5"),
                new FanSampleValue("Fan #1", 620, Identifier: "/fan/1"),
            }),
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
                new FanSampleValue("GPU Fan 1", i < 2 ? 1500 : 0, Identifier: "/gpu/fan/1"),
                new FanSampleValue("Fan #1", 600, Identifier: "/fan/1"),
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
