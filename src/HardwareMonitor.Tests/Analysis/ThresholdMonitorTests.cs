using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using Xunit;

namespace HardwareMonitor.Tests.Analysis;

public class ThresholdMonitorTests
{
    private static readonly Dictionary<string, string> NoLabels = new();
    private static readonly DateTimeOffset T0 = new(2026, 7, 8, 21, 0, 0, TimeSpan.FromHours(3));

    private static KeyMetrics Metrics(
        float? cpuTemp = null, float? ramLoad = null,
        DiskMetrics[]? disks = null, FanMetrics[]? fans = null) =>
        new(null, cpuTemp, null, null, null, null, null, null, null, null,
            ramLoad, null, null,
            disks ?? Array.Empty<DiskMetrics>(),
            fans ?? Array.Empty<FanMetrics>());

    private static ThresholdMonitor Monitor() => new(new ThresholdSettings());

    [Fact]
    public void LyhytPiikki_VaihtaaTilanHetiMutteiKirjaaTapahtumaa()
    {
        var monitor = Monitor();

        ThresholdResult r1 = monitor.Update(Metrics(cpuTemp: 90), T0, NoLabels);
        Assert.Equal(ThresholdState.Warning, r1.States.CpuTemp);
        Assert.Empty(r1.Events);

        // Piikki ohi 5 s myöhemmin — ei tapahtumia kumpaankaan suuntaan.
        ThresholdResult r2 = monitor.Update(Metrics(cpuTemp: 60), T0.AddSeconds(5), NoLabels);
        Assert.Equal(ThresholdState.Normal, r2.States.CpuTemp);
        Assert.Empty(r2.Events);
    }

    [Fact]
    public void KestavaYlitys_KirjaaWarninginTasanKerran()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();

        for (int s = 0; s <= 60; s++)
        {
            all.AddRange(monitor.Update(Metrics(cpuTemp: 90), T0.AddSeconds(s), NoLabels).Events);
        }

        ThresholdEvent e = Assert.Single(all);
        Assert.Equal("WARNING", e.Level);
        Assert.Equal("CPU", e.Component);
        Assert.Equal(90, e.Value);
        Assert.Equal(85, e.Threshold);
    }

    [Fact]
    public void Eskalaatio_KriittinenKirjautuuWarninginJalkeen()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();

        for (int s = 0; s <= 40; s++) // WARNING nousee 30 s kohdalla
        {
            all.AddRange(monitor.Update(Metrics(cpuTemp: 90), T0.AddSeconds(s), NoLabels).Events);
        }

        for (int s = 41; s <= 60; s++) // arvo kriittiseksi -> CRITICAL 10 s ylityksen jälkeen
        {
            all.AddRange(monitor.Update(Metrics(cpuTemp: 97), T0.AddSeconds(s), NoLabels).Events);
        }

        Assert.Equal(2, all.Count);
        Assert.Equal("WARNING", all[0].Level);
        Assert.Equal("CRITICAL", all[1].Level);
    }

    [Fact]
    public void Palautuminen_KirjaaInfonKestollaJaHuipulla()
    {
        var monitor = Monitor();
        for (int s = 0; s <= 45; s++)
        {
            monitor.Update(Metrics(cpuTemp: s == 20 ? 93 : 90), T0.AddSeconds(s), NoLabels);
        }

        ThresholdResult r = monitor.Update(Metrics(cpuTemp: 60), T0.AddSeconds(46), NoLabels);

        ThresholdEvent e = Assert.Single(r.Events);
        Assert.Equal("INFO", e.Level);
        Assert.Equal(93, e.Value); // huippu
        Assert.Contains("46 s", e.Message); // kesto
        Assert.Equal(ThresholdState.Normal, r.States.CpuTemp);
    }

    [Fact]
    public void Cooldown_EstaaToisenJaksonTapahtuman_SalliiMyohemmin()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();

        void Excursion(DateTimeOffset start)
        {
            for (int s = 0; s <= 35; s++)
            {
                all.AddRange(monitor.Update(Metrics(cpuTemp: 90), start.AddSeconds(s), NoLabels).Events);
            }

            all.AddRange(monitor.Update(Metrics(cpuTemp: 60), start.AddSeconds(36), NoLabels).Events);
        }

        Excursion(T0);                // WARNING + palautumis-INFO
        Excursion(T0.AddMinutes(2));  // cooldownin sisällä -> ei nostoa eikä siksi palautumistakaan
        Excursion(T0.AddMinutes(10)); // cooldown ohi -> WARNING + INFO

        Assert.Equal(2, all.Count(e => e.Level == "WARNING"));
        Assert.Equal(2, all.Count(e => e.Level == "INFO"));
    }

    [Fact]
    public void Tuuletin_PyorinytJaPysahtynytKuumana_Kriittinen()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();
        var spinning = new[] { new FanMetrics("CPU Fan", 900f, "/fan/1") };
        var stopped = new[] { new FanMetrics("CPU Fan", 0f, "/fan/1") };

        monitor.Update(Metrics(cpuTemp: 60, fans: spinning), T0, NoLabels); // rekisteröityy pyörineeksi

        ThresholdState worst = ThresholdState.Normal;
        for (int s = 1; s <= 12; s++)
        {
            ThresholdResult r = monitor.Update(Metrics(cpuTemp: 85, fans: stopped), T0.AddSeconds(s), NoLabels);
            all.AddRange(r.Events);
            worst = r.States.Worst;
        }

        Assert.Equal(ThresholdState.Critical, worst);
        ThresholdEvent e = Assert.Single(all, e => e.Component == "Tuuletin");
        Assert.Equal("CRITICAL", e.Level);
    }

    [Fact]
    public void GpuTuuletin_PysahtyyPuolipassiiviseksi_EiHalyta()
    {
        // GPU-tuulettimet pysähtyvät normaalisti idlessä (semi-passive) —
        // CPU-jäähdytyssääntö ei koske niitä, vaikka CPU olisi kuuma.
        var monitor = Monitor();
        var spinning = new[] { new FanMetrics("GPU Fan 1", 1200f, "/gpu-nvidia/0/fan/1") };
        var stopped = new[] { new FanMetrics("GPU Fan 1", 0f, "/gpu-nvidia/0/fan/1") };

        monitor.Update(Metrics(cpuTemp: 60, fans: spinning), T0, NoLabels);

        for (int s = 1; s <= 20; s++)
        {
            // CPU 82 °C: yli FanStopCpuTemp-rajan (80) mutta alle varoitusrajan (85).
            ThresholdResult r = monitor.Update(
                Metrics(cpuTemp: 82, fans: stopped), T0.AddSeconds(s), NoLabels);
            Assert.DoesNotContain(r.Events, e => e.Component == "Tuuletin");
            Assert.Equal(ThresholdState.Normal, r.States.Worst);
        }
    }

    [Fact]
    public void Tuuletin_JokaEiKoskaanPyorinyt_EiHalyta()
    {
        var monitor = Monitor();
        var never = new[] { new FanMetrics("Fan #7", 0f, "/fan/7") };

        for (int s = 0; s <= 20; s++)
        {
            ThresholdResult r = monitor.Update(Metrics(cpuTemp: 90, fans: never), T0.AddSeconds(s), NoLabels);
            Assert.DoesNotContain(r.Events, e => e.Component == "Tuuletin");
        }
    }

    [Fact]
    public void LevykohtainenTila_JaWorstKooste()
    {
        var monitor = Monitor();
        var disks = new[]
        {
            new DiskMetrics("SATA", 30f, null),
            new DiskMetrics("NVMe", 85f, null), // yli kriittisen (82)
        };

        ThresholdResult r = monitor.Update(Metrics(disks: disks), T0, NoLabels);

        Assert.Equal(ThresholdState.Normal, r.States.Disks[0]);
        Assert.Equal(ThresholdState.Critical, r.States.Disks[1]);
        Assert.Equal(ThresholdState.Critical, r.States.Worst);
    }
}
