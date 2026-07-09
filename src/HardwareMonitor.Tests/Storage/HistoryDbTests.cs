using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Storage;

public sealed class HistoryDbTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "HardwareMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly HistoryDb _db;

    public HistoryDbTests() => _db = new HistoryDb(_dir);

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 8, 20, 0, 0, TimeSpan.FromHours(3));

    private static AggregatedSample Sample(DateTimeOffset ts, float? cpuTempMax = 70f) =>
        new(
            Timestamp: ts,
            CpuLoad: new MetricAggregate(10, 20, 30),
            CpuTemp: new MetricAggregate(50, 60, cpuTempMax),
            CpuClockMax: 4700,
            CpuPower: MetricAggregate.Empty,
            GpuLoad: MetricAggregate.Empty,
            GpuTemp: MetricAggregate.Empty,
            GpuHotspot: MetricAggregate.Empty,
            GpuPower: MetricAggregate.Empty,
            VramUsedMb: MetricAggregate.Empty,
            RamLoad: MetricAggregate.Empty,
            RamUsedGb: MetricAggregate.Empty,
            Disks: new[] { new DiskAggregate(0, "NVMe", new MetricAggregate(60, 61, 62), 15) },
            Fans: new[] { new FanAggregate("/fan/2", "Fan #2", new MetricAggregate(1950, 1955, 1960)) });

    [Fact]
    public void InsertSample_KasvattaaRivimaaraa()
    {
        Assert.Equal(0, _db.CountSamples());

        _db.InsertSample(Sample(Now));
        _db.InsertSample(Sample(Now.AddSeconds(5)));

        Assert.Equal(2, _db.CountSamples());
    }

    [Fact]
    public void PurgeOlderThan_PoistaaVanhatMutteiUusia()
    {
        _db.InsertSample(Sample(Now.AddDays(-40)));
        _db.InsertSample(Sample(Now));

        _db.PurgeOlderThan(Now.AddDays(-30));

        Assert.Equal(1, _db.CountSamples());
    }

    [Fact]
    public void InsertEvent_JaReadRecentEvents_PalauttaaUusimmatEnsin()
    {
        _db.InsertEvent(Now, "INFO", "App", null, null, null, "Sovellus käynnistyi");
        _db.InsertEvent(Now.AddMinutes(1), "WARNING", "CPU", "CPU Package", 92, 90, "CPU kuuma");

        IReadOnlyList<EventRow> events = _db.ReadRecentEvents(10);

        Assert.Equal(2, events.Count);
        Assert.Equal("WARNING", events[0].Level);
        Assert.Equal(92, events[0].Value);
        Assert.Equal("Sovellus käynnistyi", events[1].Message);
    }

    [Fact]
    public void GetMeta_PuuttuvaAvain_PalauttaaNull()
    {
        Assert.Null(_db.GetMeta("windows_last_record_id"));
    }

    [Fact]
    public void SetMeta_TallentaaJaKorvaaArvon()
    {
        _db.SetMeta("windows_last_record_id", "123");
        Assert.Equal("123", _db.GetMeta("windows_last_record_id"));

        _db.SetMeta("windows_last_record_id", "456");
        Assert.Equal("456", _db.GetMeta("windows_last_record_id"));
    }

    [Fact]
    public void EventLogService_KirjoittaaOikeallaTasolla()
    {
        var service = new EventLogService(_db);

        service.Info("App", "testi");

        EventRow row = Assert.Single(_db.ReadRecentEvents(1));
        Assert.Equal("INFO", row.Level);
        Assert.Equal("App", row.Component);
    }
}
