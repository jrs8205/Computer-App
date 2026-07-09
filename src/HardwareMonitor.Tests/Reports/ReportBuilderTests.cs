using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Reports;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Reports;

public class ReportBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 10, 30, 0, TimeSpan.FromHours(3));
    private static readonly ThresholdSettings Limits = new();

    private static KeyMetrics Metrics(
        float? cpuTemp = 45, float? diskTemp = 61,
        string diskName = "Samsung 970 EVO Plus") => new(
        CpuLoadPercent: 10, CpuPackageTempC: cpuTemp, CpuMaxClockMhz: 4700,
        CpuPackagePowerW: 35, GpuLoadPercent: 5, GpuTempC: 44,
        GpuHotspotTempC: 57, GpuMemoryUsedMb: 900, GpuMemoryTotalMb: 6144,
        GpuPowerW: 20, RamLoadPercent: 15, RamUsedGb: 9, RamAvailableGb: 55,
        Disks: new[] { new DiskMetrics(diskName, diskTemp, 5) },
        Fans: new[] { new FanMetrics("AIO-pumppu", 1950, "/fan/2") });

    private static MetricStates States(ThresholdState cpu = ThresholdState.Normal) =>
        new(cpu, ThresholdState.Normal, ThresholdState.Normal, ThresholdState.Normal,
            new[] { ThresholdState.Normal }, cpu);

    private static RiskAssessment Ok() => new(
        ThresholdState.Normal, "Hyvä", "Matala", 0,
        new[] { "Ei WHEA-virheitä (24 h)" }, null);

    private static SampleStats Stats(double? cpuMax = 58, double? diskMax = 62) => new(
        SampleCount: 500,
        CpuTemp: new MetricStat(36, cpuMax),
        CpuLoad: new MetricStat(8, 67),
        GpuTemp: new MetricStat(44, 48),
        GpuHotspot: new MetricStat(55, 61),
        RamLoad: new MetricStat(15, 21),
        Disks: new[] { new DiskStat("Samsung 970 EVO Plus", 58, diskMax) },
        Fans: new[] { new FanStat("AIO-pumppu", 1950, 1990) });

    private static string Build(
        RiskAssessment? assessment = null, KeyMetrics? metrics = null,
        MetricStates? states = null, SampleStats? dayStats = null,
        SampleStats? monthStats = null, IReadOnlyList<EventRow>? dayEvents = null) =>
        ReportBuilder.Build(
            Now, assessment ?? Ok(), metrics ?? Metrics(), states ?? States(),
            dayStats ?? Stats(), monthStats ?? Stats(),
            dayEvents ?? Array.Empty<EventRow>(), Limits);

    [Fact]
    public void SisaltaaOtsikonJaSelkokielisenJohdannon()
    {
        string report = Build();

        Assert.Contains("# Järjestelmäraportti", report);
        Assert.Contains("9.7.2026", report);
        Assert.Contains("tavallisella kielellä", report);
    }

    [Fact]
    public void YhteenvedossaTilaJaTasojenSelitys()
    {
        string report = Build();

        Assert.Contains("Koneen tila: Hyvä", report);
        Assert.Contains("Riskitaso: Matala", report);
        Assert.Contains("Mitä tasot tarkoittavat", report);
        Assert.Contains("Ei WHEA-virheitä (24 h)", report);
    }

    [Fact]
    public void NykyarvotSelitetaanRajoineen()
    {
        string report = Build();

        Assert.Contains("CPU-lämpötila: 45 °C — kunnossa (varoitusraja 85 °C)", report);
        Assert.Contains("Levy Samsung 970 EVO Plus: 61 °C — kunnossa (varoitusraja 70 °C)", report);
    }

    [Fact]
    public void LevynNimenYlimaaraisetValilyonnitSiivotaan()
    {
        // LibreHardwareMonitor jättää joihinkin levynimiin loppuvälilyönnin.
        string report = Build(metrics: Metrics(diskName: "Samsung SSD 860 EVO 1TB "));

        Assert.Contains("Levy Samsung SSD 860 EVO 1TB: 61 °C", report);
    }

    [Fact]
    public void VaroitustilaHuudetaanArvorivilla()
    {
        string report = Build(
            metrics: Metrics(cpuTemp: 88),
            states: States(cpu: ThresholdState.Warning));

        Assert.Contains("CPU-lämpötila: 88 °C — VAROITUS", report);
    }

    [Fact]
    public void VuorokaudenHuippuSaaVertailulauseen()
    {
        string report = Build(dayStats: Stats(cpuMax: 58, diskMax: 68));

        // 58 °C on kaukana 85 °C:sta; 68 °C on 2 °C:n päässä 70 °C:sta.
        Assert.Contains("enimmillään 58 °C — jäi selvästi alle varoitusrajan (85 °C)", report);
        Assert.Contains("enimmillään 68 °C — kävi lähellä varoitusrajaa (70 °C)", report);
    }

    [Fact]
    public void HuippuYliRajan_KerrotaanSuoraan()
    {
        string report = Build(dayStats: Stats(cpuMax: 91));

        Assert.Contains("enimmillään 91 °C — ylitti varoitusrajan (85 °C)", report);
    }

    [Fact]
    public void NormaalitasotKerrotaanKeskiarvoina()
    {
        string report = Build();

        Assert.Contains("keskimäärin 36 °C", report);
    }

    [Fact]
    public void TapahtumatListataanSuomeksi()
    {
        var events = new EventRow[]
        {
            new(Now.AddHours(-2), "WARNING", "Järjestelmä", "last_state", null, null,
                "Edellinen istunto päättyi yllättäen"),
            new(Now.AddHours(-1), "INFO", "App", null, null, null, "Sovellus käynnistyi"),
        };

        string report = Build(dayEvents: events);

        Assert.Contains("VAROITUS", report);
        Assert.Contains("Edellinen istunto päättyi yllättäen", report);
        Assert.DoesNotContain("Sovellus käynnistyi", report); // INFO-rivit pois
    }

    [Fact]
    public void EiTapahtumia_TodetaanHyvaksiMerkiksi()
    {
        string report = Build();

        Assert.Contains("hyvä merkki", report);
    }

    [Fact]
    public void SanastoSelittaaTekniikkatermit()
    {
        string report = Build();

        Assert.Contains("## Sanasto", report);
        Assert.Contains("WHEA", report);
        Assert.Contains("Kernel-Power 41", report);
        Assert.Contains("hotspot", report);
    }
}
