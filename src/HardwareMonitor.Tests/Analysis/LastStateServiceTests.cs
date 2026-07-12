using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using Xunit;

namespace HardwareMonitor.Tests.Analysis;

public sealed class LastStateServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "HardwareMonitorTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 9, 9, 30, 0, TimeSpan.FromHours(3));

    private static KeyMetrics Metrics() => new(
        CpuLoadPercent: 10, CpuPackageTempC: 97, CpuMaxClockMhz: 4700,
        CpuPackagePowerW: 120, GpuLoadPercent: 99, GpuTempC: 83,
        GpuHotspotTempC: 106, GpuMemoryUsedMb: 5000, GpuMemoryTotalMb: 6144,
        GpuPowerW: 170, RamLoadPercent: 94, RamUsedGb: 60, RamAvailableGb: 4,
        Disks: new[] { new DiskMetrics("970 EVO Plus", 78, 90) },
        Fans: new[] { new FanMetrics("AIO-pumppu", 1950, "/fan/2") });

    [Fact]
    public void ReadPrevious_EiTiedostoa_PalauttaaNull()
    {
        var service = new LastStateService(_dir);

        Assert.Null(service.ReadPrevious());
    }

    [Fact]
    public void Write_TallentaaArvotLikaisellaLipulla()
    {
        var service = new LastStateService(_dir);
        service.Write(Metrics(), Now);

        LastState? state = new LastStateService(_dir).ReadPrevious();

        Assert.NotNull(state);
        Assert.False(state.CleanShutdown);
        Assert.Equal(Now.ToUnixTimeSeconds(), state.Timestamp.ToUnixTimeSeconds());
        Assert.Equal(97, state.CpuTempC);
        Assert.Equal(106, state.GpuHotspotC);
        Assert.Equal(94, state.RamLoadPercent);
        LastStateDisk disk = Assert.Single(state.Disks);
        Assert.Equal("970 EVO Plus", disk.Name);
        Assert.Equal(78, disk.TempC);
    }

    [Fact]
    public void MarkCleanShutdown_AsettaaPuhtaanLipun()
    {
        var service = new LastStateService(_dir);
        service.Write(Metrics(), Now);
        service.MarkCleanShutdown();

        LastState? state = new LastStateService(_dir).ReadPrevious();

        Assert.NotNull(state);
        Assert.True(state.CleanShutdown);
        Assert.Equal(97, state.CpuTempC); // arvot säilyvät lipun päivittyessä
    }

    [Fact]
    public void Write_SiistinSulkemisenJalkeen_EiLikaaLippua()
    {
        // Taustasäikeen viivästynyt kirjoitus ei saa kumota MarkCleanShutdownia.
        var service = new LastStateService(_dir);
        service.Write(Metrics(), Now);
        service.MarkCleanShutdown();

        service.Write(Metrics(), Now.AddSeconds(5));

        LastState? state = new LastStateService(_dir).ReadPrevious();
        Assert.NotNull(state);
        Assert.True(state.CleanShutdown);
    }

    [Fact]
    public void Write_MarkCleanShutdownKeskenKirjoituksen_EiLikaaLippua()
    {
        // Sama lomitus kuin taustakirjoituksen ja sulkemisen kilpailussa:
        // Write on ehtinyt tilanrakennukseen, kun MarkCleanShutdown ajaa läpi.
        // Levylistan enumerointi laukaisee Markin deterministisesti keskelle Writeä.
        var service = new LastStateService(_dir);
        service.Write(Metrics(), Now);

        var trap = new MarkTriggeringDiskList(service);
        service.Write(Metrics() with { Disks = trap }, Now.AddSeconds(5));

        LastState? state = new LastStateService(_dir).ReadPrevious();
        Assert.NotNull(state);
        Assert.True(state.CleanShutdown);
    }

    /// <summary>Kutsuu MarkCleanShutdownia kun listaa aletaan lukea.</summary>
    private sealed class MarkTriggeringDiskList : IReadOnlyList<DiskMetrics>
    {
        private readonly LastStateService _service;
        private bool _triggered;

        public MarkTriggeringDiskList(LastStateService service) => _service = service;

        public DiskMetrics this[int index] => Trigger()[index];

        public int Count => Trigger().Count;

        public IEnumerator<DiskMetrics> GetEnumerator() => Trigger().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();

        private IReadOnlyList<DiskMetrics> Trigger()
        {
            if (!_triggered)
            {
                _triggered = true;
                _service.MarkCleanShutdown();
            }

            return new[] { new DiskMetrics("970 EVO Plus", 78, 90) };
        }
    }

    [Fact]
    public void Write_VanhempiAikaleimaMyohemminSaapuen_EiKorvaaUudempaa()
    {
        // Rinnakkaiset 5 s kirjoitukset (Task.Run) voivat valmistua eri
        // järjestyksessä kuin käynnistyivät — vanhempi tila ei saa jäädä
        // viimeiseksi. Palvelu hylkää nykyistä vanhemman aikaleiman.
        var service = new LastStateService(_dir);
        service.Write(Metrics() with { }, Now.AddSeconds(10));
        service.Write(Metrics(), Now); // vanhempi saapuu myöhemmin

        LastState? state = new LastStateService(_dir).ReadPrevious();
        Assert.NotNull(state);
        Assert.Equal(Now.AddSeconds(10).ToUnixTimeSeconds(), state.Timestamp.ToUnixTimeSeconds());
    }

    [Fact]
    public void ReadPrevious_RikkinainenTiedosto_PalauttaaNull()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "last_state.json"), "{ rikki");

        Assert.Null(new LastStateService(_dir).ReadPrevious());
    }
}
