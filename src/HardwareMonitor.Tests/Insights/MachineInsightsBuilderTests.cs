using HardwareMonitor.Core.Insights;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Insights;

public class MachineInsightsBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 10, 0, 0, TimeSpan.FromHours(3));
    private static readonly ThresholdSettings Limits = new();

    private static SampleStats Stats(
        long count = 1000, double? cpuMax = 82, double? diskMax = 62) => new(
        SampleCount: count,
        CpuTemp: new MetricStat(52, cpuMax),
        CpuLoad: new MetricStat(12, 95),
        GpuTemp: new MetricStat(44, 76),
        GpuHotspot: new MetricStat(52, 91),
        RamLoad: new MetricStat(35, 71),
        Disks: new[] { new DiskStat("970 EVO Plus", 55, diskMax) },
        Fans: new[] { new FanStat("AIO-pumppu", 1948, 1990) });

    private static MachineSpec Spec(string notes = "") => new(
        "Intel Core i9-9900K", "NVIDIA GeForce RTX 2060",
        "ASUS ROG STRIX Z390-F GAMING", 64,
        new[] { "970 EVO Plus" }, "Windows 11 (build 26200)", notes);

    private static string Build(
        SampleStats? stats = null,
        IReadOnlyList<EventRow>? events = null,
        SampleStats? stats7d = null,
        MachineSpec? spec = null) =>
        MachineInsightsBuilder.Build(new MachineInsightsInput(
            Now,
            spec ?? Spec(),
            stats ?? Stats(),
            stats7d ?? stats ?? Stats(),
            events ?? Array.Empty<EventRow>(),
            Limits));

    [Fact]
    public void SisaltaaOtsikonJaNormaalitasot()
    {
        string md = Build();

        Assert.Contains("# Konetuntemus-loki", md);
        Assert.Contains("9.7.2026", md);
        Assert.Contains("52", md);      // CPU keskilämpö
        Assert.Contains("82 °C", md);   // CPU huippu
    }

    [Fact]
    public void ListaaLevytJaTuulettimetNimineen()
    {
        string md = Build();

        Assert.Contains("970 EVO Plus", md);
        Assert.Contains("62 °C", md);
        Assert.Contains("AIO-pumppu", md);
        Assert.Contains("1948", md);
    }

    [Fact]
    public void KuumaLevy_TuottaaOptimointivinkin()
    {
        string md = Build(stats: Stats(diskMax: 83));

        Assert.Contains("970 EVO Plus", md);
        Assert.Contains("varoitusraja", md, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jäähdytys", md, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TapahtumatKootaanMaarineen()
    {
        var events = new EventRow[]
        {
            new(Now.AddDays(-1), "CRITICAL", "Laitteisto", "Microsoft-Windows-WHEA-Logger", 18, null, "whea"),
            new(Now.AddDays(-2), "WARNING", "Laitteisto", "Microsoft-Windows-WHEA-Logger", 19, null, "whea2"),
            new(Now.AddDays(-3), "CRITICAL", "Järjestelmä", "Microsoft-Windows-Kernel-Power", 41, null, "kp41"),
            new(Now.AddDays(-4), "WARNING", "CPU", "CPU Package", 88, 85, "raja"),
            new(Now.AddDays(-5), "INFO", "App", null, null, null, "ohitetaan"),
        };

        string md = Build(events: events);

        Assert.Contains("WHEA-rautavirheitä: 2", md);
        Assert.Contains("Yllättäviä sammutuksia tai kaatumisia: 1", md);
        Assert.Contains("Raja-arvoylityksiä: 1", md);
    }

    [Fact]
    public void KaikkiKunnossa_ToteaaEttaOngelmiaEiOle()
    {
        string md = Build();

        Assert.Contains("Ei merkittäviä ongelmia", md);
    }

    [Fact]
    public void SisaltaaJohdannonTekoalylle()
    {
        string md = Build();

        Assert.Contains("## Johdanto tekoälylle", md);
        Assert.Contains("LibreHardwareMonitor", md);
        Assert.Contains("vianetsinnästä", md);
    }

    [Fact]
    public void JohdantoNakyyMyosIlmanDataa()
    {
        string md = Build(stats: Stats(count: 0));

        Assert.Contains("## Johdanto tekoälylle", md);
    }

    [Fact]
    public void KokoonpanoListaaKomponentit()
    {
        string md = Build();

        Assert.Contains("## Koneen kokoonpano", md);
        Assert.Contains("i9-9900K", md);
        Assert.Contains("RTX 2060", md);
        Assert.Contains("Z390-F", md);
        Assert.Contains("64 GB", md);
        Assert.Contains("Windows 11", md);
    }

    [Fact]
    public void SamannimisetLevytRyhmitellaan()
    {
        MachineSpec spec = Spec() with
        {
            DiskNames = new[] { "860 EVO", "860 EVO", "970 EVO Plus" },
        };

        string md = Build(spec: spec);

        Assert.Contains("2 × 860 EVO", md);
        Assert.Contains("970 EVO Plus", md);
    }

    [Fact]
    public void LisatiedotMukanaVainKunAsetettu()
    {
        Assert.DoesNotContain("lisätiedot", Build(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AIO-vesijäähdytys", Build(spec: Spec(notes: "AIO-vesijäähdytys")));
    }

    [Fact]
    public void PuuttuvaKokoonpanotietoNaytetaanViivana()
    {
        var spec = new MachineSpec(null, null, null, null, Array.Empty<string>(), "", "");

        string md = Build(spec: spec);

        Assert.Contains("- Suoritin: —", md);
        Assert.Contains("- Levyt: —", md);
    }

    [Fact]
    public void IlmanDataa_KertooEttaDataaKertyyVasta()
    {
        string md = Build(stats: Stats(count: 0));

        Assert.Contains("Ei vielä riittävästi dataa", md);
    }
}
