using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Analysis;

public class RiskAnalyzerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 9, 0, 0, TimeSpan.FromHours(3));
    private static readonly ThresholdSettings Limits = new();

    private static KeyMetrics Metrics(
        float? cpuTemp = 45, float? gpuTemp = 40, float? hotspot = 50,
        float? ram = 30, float? diskTemp = 55) =>
        new(
            CpuLoadPercent: 10, CpuPackageTempC: cpuTemp, CpuMaxClockMhz: 4700,
            CpuPackagePowerW: 35, GpuLoadPercent: 5, GpuTempC: gpuTemp,
            GpuHotspotTempC: hotspot, GpuMemoryUsedMb: 800, GpuMemoryTotalMb: 6144,
            GpuPowerW: 20, RamLoadPercent: ram, RamUsedGb: 8, RamAvailableGb: 56,
            Disks: new[] { new DiskMetrics("970 EVO Plus", diskTemp, 5) },
            Fans: new[] { new FanMetrics("Fan #2", 1950, "/fan/2") });

    private static MetricStates States(
        ThresholdState cpu = ThresholdState.Normal,
        ThresholdState gpu = ThresholdState.Normal,
        ThresholdState hotspot = ThresholdState.Normal,
        ThresholdState ram = ThresholdState.Normal,
        ThresholdState disk = ThresholdState.Normal,
        ThresholdState? worst = null)
    {
        ThresholdState computed = new[] { cpu, gpu, hotspot, ram, disk }.Max();
        return new MetricStates(cpu, gpu, hotspot, ram,
            new[] { disk }, worst ?? computed);
    }

    private static EventRow Event(
        string level, string component, string? sensor = null,
        double hoursAgo = 1, string message = "testi") =>
        new(Now.AddHours(-hoursAgo), level, component, sensor, null, null, message);

    private static RiskAssessment Assess(
        MetricStates? states = null, KeyMetrics? metrics = null,
        IReadOnlyList<EventRow>? events = null, SampleStats? dayStats = null,
        bool previousSessionCrashed = false) =>
        RiskAnalyzer.Assess(
            states ?? States(), metrics ?? Metrics(), Limits,
            events ?? Array.Empty<EventRow>(), dayStats, previousSessionCrashed);

    [Fact]
    public void KaikkiKunnossa_HyvaJaMatala()
    {
        RiskAssessment a = Assess();

        Assert.Equal(ThresholdState.Normal, a.Level);
        Assert.Equal("Hyvä", a.Status);
        Assert.Equal("Matala", a.RiskLevel);
        Assert.Equal(0, a.Score);
        Assert.Null(a.Recommendation);
        Assert.Contains(a.Observations, o => o.Contains("Ei WHEA-virheitä"));
        Assert.Contains(a.Observations, o => o.Contains("Ei yllättäviä sammutuksia"));
    }

    [Fact]
    public void CpuVaroitusNyt_VaroitusJaSeliteRajoineen()
    {
        RiskAssessment a = Assess(
            states: States(cpu: ThresholdState.Warning),
            metrics: Metrics(cpuTemp: 88));

        Assert.Equal(ThresholdState.Warning, a.Level);
        Assert.Equal("Varoitus", a.Status);
        Assert.Equal("Kohonnut", a.RiskLevel);
        Assert.Contains(a.Observations,
            o => o.Contains("CPU-lämpötila 88 °C") && o.Contains("varoitusrajan (85 °C)"));
    }

    [Fact]
    public void HotspotKriittinenNyt_KriittinenJaJaahdytysSuositus()
    {
        RiskAssessment a = Assess(
            states: States(hotspot: ThresholdState.Critical),
            metrics: Metrics(hotspot: 107));

        Assert.Equal(ThresholdState.Critical, a.Level);
        Assert.Equal("Kriittinen", a.Status);
        Assert.Equal("Korkea", a.RiskLevel);
        Assert.Contains(a.Observations,
            o => o.Contains("GPU hotspot") && o.Contains("kriittisen rajan (105 °C)"));
        Assert.NotNull(a.Recommendation);
        Assert.Contains("jäähdytys", a.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LevyVaroitusNyt_NimiJaRajaNakyvat()
    {
        RiskAssessment a = Assess(
            states: States(disk: ThresholdState.Warning),
            metrics: Metrics(diskTemp: 74));

        Assert.Contains(a.Observations,
            o => o.Contains("970 EVO Plus") && o.Contains("74 °C") && o.Contains("70 °C"));
    }

    [Fact]
    public void VakavaWheaTapahtuma_KriittinenJaRautaSuositus()
    {
        RiskAssessment a = Assess(events: new[] { Event("CRITICAL", "Laitteisto") });

        Assert.Equal(ThresholdState.Critical, a.Level);
        Assert.Equal(15, a.Score);
        Assert.Contains(a.Observations, o => o.Contains("WHEA") && o.Contains("1"));
        Assert.NotNull(a.Recommendation);
    }

    [Fact]
    public void EdellinenIstuntoKaatui_VaroitusIlmanTuplapisteita()
    {
        // Kaatumisesta kirjattu tapahtuma (sensor=last_state) EI saa pisteyttää
        // uudelleen samaa asiaa kuin lippu.
        RiskAssessment a = Assess(
            events: new[] { Event("WARNING", "Järjestelmä", sensor: "last_state") },
            previousSessionCrashed: true);

        Assert.Equal(10, a.Score);
        Assert.Equal("Varoitus", a.Status);
        Assert.Contains(a.Observations, o => o.Contains("Edellinen istunto päättyi yllättäen"));
    }

    [Fact]
    public void KernelPower41Tapahtuma_KriittinenJaHavainto()
    {
        RiskAssessment a = Assess(events: new[] { Event("CRITICAL", "Järjestelmä") });

        Assert.Equal(15, a.Score);
        Assert.Contains(a.Observations,
            o => o.Contains("sammutuksia tai kaatumisia") && o.Contains("1"));
    }

    [Fact]
    public void WindowsLevyvirhe_KohonnutJaVarmuuskopioSuositus()
    {
        RiskAssessment a = Assess(events: new[] { Event("CRITICAL", "Levy", sensor: "stornvme") });

        Assert.Equal(10, a.Score);
        Assert.NotNull(a.Recommendation);
        Assert.Contains("varmuuskopio", a.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RajaArvoTapahtumat_PisteytetaanTasonMukaan()
    {
        RiskAssessment a = Assess(events: new[]
        {
            Event("WARNING", "CPU", sensor: "CPU Package"),   // 2 p
            Event("CRITICAL", "Levy", sensor: "970 EVO Plus"), // 6 p (lämpö, ei Windows-provider)
        });

        Assert.Equal(8, a.Score);
        Assert.Contains(a.Observations, o => o.Contains("Raja-arvoylityksiä") && o.Contains("2"));
    }

    [Fact]
    public void InfoTapahtumat_EivatPisteyta()
    {
        RiskAssessment a = Assess(events: new[] { Event("INFO", "App") });

        Assert.Equal(0, a.Score);
    }

    [Fact]
    public void VuorokaudenHuiput_NakyvatHavainnoissa()
    {
        var stats = new SampleStats(
            SampleCount: 100,
            CpuTemp: new MetricStat(52, 82),
            CpuLoad: new MetricStat(15, 90),
            GpuTemp: new MetricStat(45, 76),
            GpuHotspot: new MetricStat(55, 94),
            RamLoad: new MetricStat(40, 78),
            Disks: Array.Empty<DiskStat>(),
            Fans: Array.Empty<FanStat>());

        RiskAssessment a = Assess(dayStats: stats);

        Assert.Contains(a.Observations, o => o.Contains("CPU") && o.Contains("82 °C"));
        Assert.Contains(a.Observations, o => o.Contains("hotspot") && o.Contains("94 °C"));
        Assert.Contains(a.Observations, o => o.Contains("RAM") && o.Contains("78 %"));
    }
}
