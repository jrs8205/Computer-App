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
    public void ReadEventsSince_PalauttaaVainRajanJalkeiset()
    {
        _db.InsertEvent(Now.AddHours(-30), "WARNING", "CPU", null, null, null, "vanha");
        _db.InsertEvent(Now.AddHours(-1), "CRITICAL", "Järjestelmä", null, null, null, "uusi");

        IReadOnlyList<EventRow> events = _db.ReadEventsSince(Now.AddHours(-24));

        EventRow row = Assert.Single(events);
        Assert.Equal("uusi", row.Message);
    }

    [Fact]
    public void GetSampleStats_LaskeeKeskiarvotJaMaksimitRajanJalkeen()
    {
        _db.InsertSample(Sample(Now, cpuTempMax: 70f));
        _db.InsertSample(Sample(Now.AddSeconds(5), cpuTempMax: 90f));
        _db.InsertSample(Sample(Now.AddDays(-2), cpuTempMax: 99f)); // rajautuu pois

        SampleStats stats = _db.GetSampleStats(Now.AddHours(-24));

        Assert.Equal(2, stats.SampleCount);
        Assert.Equal(60, stats.CpuTemp.Avg!.Value, 3); // avg(avg)
        Assert.Equal(90, stats.CpuTemp.Max);           // max(max), vain 24 h sisältä

        DiskStat disk = Assert.Single(stats.Disks);
        Assert.Equal("NVMe", disk.Name);
        Assert.Equal(61, disk.TempAvg!.Value, 3);
        Assert.Equal(62, disk.TempMax);

        FanStat fan = Assert.Single(stats.Fans);
        Assert.Equal("Fan #2", fan.Name);
        Assert.Equal(1955, fan.RpmAvg!.Value, 3);
        Assert.Equal(1960, fan.RpmMax);
    }

    [Fact]
    public void GetSampleStats_TyhjaKanta_PalauttaaNollatJaNullit()
    {
        SampleStats stats = _db.GetSampleStats(Now.AddHours(-24));

        Assert.Equal(0, stats.SampleCount);
        Assert.Null(stats.CpuTemp.Max);
        Assert.Empty(stats.Disks);
        Assert.Empty(stats.Fans);
    }

    [Fact]
    public void ReadSampleRows_PalauttaaRivitLapsineenVanhinEnsin()
    {
        _db.InsertSample(Sample(Now.AddSeconds(5), cpuTempMax: 90f));
        _db.InsertSample(Sample(Now, cpuTempMax: 70f));
        _db.InsertSample(Sample(Now.AddDays(-2))); // rajautuu pois

        IReadOnlyList<SampleRow> rows = _db.ReadSampleRows(Now.AddHours(-24));

        Assert.Equal(2, rows.Count);
        Assert.Equal(70, rows[0].CpuTempMax); // vanhin ensin (aikajana)
        Assert.Equal(90, rows[1].CpuTempMax);
        Assert.Equal(20, rows[0].CpuLoadAvg);

        DiskSampleValue disk = Assert.Single(rows[0].Disks);
        Assert.Equal("NVMe", disk.Name);
        Assert.Equal(62, disk.TempMax);

        FanSampleValue fan = Assert.Single(rows[0].Fans);
        Assert.Equal("Fan #2", fan.Name);
        Assert.Equal(1955, fan.RpmAvg);
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
