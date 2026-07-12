using HardwareMonitor.Core.Storage;
using HardwareMonitor.Core.WindowsEvents;
using Xunit;

namespace HardwareMonitor.Tests.WindowsEvents;

public sealed class WindowsEventCollectorTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "HardwareMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly HistoryDb _db;

    public WindowsEventCollectorTests() => _db = new HistoryDb(_dir);

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 9, 8, 0, 0, TimeSpan.FromHours(3));

    private sealed class FakeSource : IWindowsEventSource
    {
        public List<WindowsLogEvent> Events { get; } = new();
        public long? LastRequestedRecordId { get; private set; }

        public IReadOnlyList<WindowsLogEvent> ReadSince(long lastRecordId, TimeSpan maxAge)
        {
            LastRequestedRecordId = lastRecordId;
            return Events.Where(e => e.RecordId > lastRecordId).ToList();
        }

        public long? ReadNewestRecordId() =>
            Events.Count == 0 ? null : Events.Max(e => e.RecordId);

        public DateTime? LogCreationTimeUtc { get; set; }

        /// <summary>Peräkkäiset luontiaika-arvot (tyhjennys kesken skannauksen).</summary>
        public Queue<DateTime?>? LogCreationSequence { get; set; }

        public DateTime? ReadLogCreationTimeUtc() =>
            LogCreationSequence is { Count: > 0 } q ? q.Dequeue() : LogCreationTimeUtc;
    }

    [Fact]
    public void Scan_KirjoittaaVainLuokitellutTapahtumatKantaan()
    {
        var source = new FakeSource();
        source.Events.Add(new WindowsLogEvent(Now.AddMinutes(-10),
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 100));
        source.Events.Add(new WindowsLogEvent(Now.AddMinutes(-5),
            "Service Control Manager", 7036, WindowsLevel: 4, RecordId: 101)); // ohitetaan
        source.Events.Add(new WindowsLogEvent(Now.AddMinutes(-1),
            "disk", 7, WindowsLevel: 2, RecordId: 102));

        var collector = new WindowsEventCollector(source, _db);
        int written = collector.Scan();

        Assert.Equal(2, written);
        IReadOnlyList<EventRow> rows = _db.ReadRecentEvents(10);
        Assert.Equal(2, rows.Count);
        Assert.Equal("CRITICAL", rows[0].Level);          // uusin ensin = disk 7
        Assert.Equal("Levy", rows[0].Component);
        Assert.Equal("disk", rows[0].Sensor);             // sensor = provider
        Assert.Equal(7, rows[0].Value);                   // value = eventId
        Assert.Contains("Kernel-Power 41", rows[1].Message);
    }

    [Fact]
    public void Scan_KayttaaTapahtumanOmaaAikaleimaa()
    {
        var source = new FakeSource();
        DateTimeOffset eventTime = Now.AddDays(-2);
        source.Events.Add(new WindowsLogEvent(eventTime,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 1));

        new WindowsEventCollector(source, _db).Scan();

        EventRow row = Assert.Single(_db.ReadRecentEvents(1));
        Assert.Equal(eventTime.ToUnixTimeSeconds(), row.Timestamp.ToUnixTimeSeconds());
    }

    [Fact]
    public void Scan_PaivittaaKirjanmerkinMyosOhitetuista()
    {
        var source = new FakeSource();
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 100));
        source.Events.Add(new WindowsLogEvent(Now,
            "Service Control Manager", 7036, WindowsLevel: 4, RecordId: 200)); // ohitetaan

        new WindowsEventCollector(source, _db).Scan();

        Assert.Equal("200", _db.GetMeta("windows_last_record_id"));
    }

    [Fact]
    public void Scan_ToinenAjo_EiKirjoitaDuplikaatteja()
    {
        var source = new FakeSource();
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 100));

        var collector = new WindowsEventCollector(source, _db);
        Assert.Equal(1, collector.Scan());
        Assert.Equal(0, collector.Scan());

        Assert.Single(_db.ReadRecentEvents(10));
    }

    [Fact]
    public void Scan_LukeeKirjanmerkinKannasta()
    {
        _db.SetMeta("windows_last_record_id", "150");
        var source = new FakeSource();

        new WindowsEventCollector(source, _db).Scan();

        Assert.Equal(150, source.LastRequestedRecordId);
    }

    [Fact]
    public void Scan_LokiTyhjennetty_NollaaKirjanmerkin()
    {
        // System-lokin tyhjennyksen jälkeen RecordID:t alkavat taas pienestä —
        // vanha suuri kirjanmerkki ei saa mykistää valvontaa pysyvästi.
        _db.SetMeta("windows_last_record_id", "5000");
        var source = new FakeSource();
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 2));

        int written = new WindowsEventCollector(source, _db).Scan();

        Assert.Equal(1, written);
        Assert.Equal(0, source.LastRequestedRecordId); // luku alkoi alusta
        Assert.Equal("2", _db.GetMeta("windows_last_record_id"));
    }

    [Fact]
    public void Scan_UusinIdSuurempiKuinKirjanmerkki_EiNollausta()
    {
        _db.SetMeta("windows_last_record_id", "150");
        var source = new FakeSource();
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 200));

        new WindowsEventCollector(source, _db).Scan();

        Assert.Equal(150, source.LastRequestedRecordId);
    }

    [Fact]
    public void Scan_LokinLuontiaikaMuuttunut_NollaaKirjanmerkinVaikkaIdRiittaisi()
    {
        // Jos loki tyhjennetään sovelluksen ollessa sammuksissa ja uusi loki
        // ehtii kasvaa yli vanhan kirjanmerkin, RecordID-vertailu ei huomaa
        // tyhjennystä — lokitiedoston luontiaika (sukupolvi) huomaa.
        _db.SetMeta("windows_last_record_id", "150");
        _db.SetMeta("windows_log_created_utc",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks.ToString());
        var source = new FakeSource
        {
            LogCreationTimeUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 200));

        int written = new WindowsEventCollector(source, _db).Scan();

        Assert.Equal(0, source.LastRequestedRecordId); // luku alkoi alusta
        Assert.Equal(1, written);
    }

    [Fact]
    public void Scan_TallentaaLokinLuontiajan()
    {
        var created = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var source = new FakeSource { LogCreationTimeUtc = created };
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 5));

        new WindowsEventCollector(source, _db).Scan();

        Assert.Equal(created.Ticks.ToString(), _db.GetMeta("windows_log_created_utc"));
    }

    [Fact]
    public void Scan_LokiTyhjennettyIlmanUusiaTapahtumia_NollaaBookmarkinJaTallentaaSukupolven()
    {
        // Loki tyhjennetty (sukupolvi muuttunut) mutta uusia luokiteltavia
        // tapahtumia ei vielä ole: bookmarkin ja sukupolven pitää silti
        // siirtyä yhdessä, muuten seuraava skannaus näkee saman sukupolven
        // ja käyttää vanhaa korkeaa bookmarkia (alkupään tapahtumat hukkuisivat).
        _db.SetMeta("windows_last_record_id", "5000");
        _db.SetMeta("windows_log_created_utc",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks.ToString());
        var newGen = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var source = new FakeSource { LogCreationTimeUtc = newGen }; // tyhjä loki, 0 tapahtumaa

        new WindowsEventCollector(source, _db).Scan();

        Assert.Equal("0", _db.GetMeta("windows_last_record_id"));
        Assert.Equal(newGen.Ticks.ToString(), _db.GetMeta("windows_log_created_utc"));
    }

    [Fact]
    public void Scan_SukupolviVaihtuuKeskenSkannauksen_HylkaaTuloksen()
    {
        // Jos loki tyhjennetään ReadSincen aikana, luettu joukko on eri
        // sukupolvea kuin ennen luettu — tulosta ei saa tallentaa vanhalla
        // sukupolvella (seuraava skannaus nollaisi ja lisäisi duplikaatit).
        var g1 = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var g2 = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        _db.SetMeta("windows_log_created_utc", g1.Ticks.ToString());
        var source = new FakeSource
        {
            LogCreationSequence = new Queue<DateTime?>(new DateTime?[] { g1, g2 }),
        };
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 100));

        int written = new WindowsEventCollector(source, _db).Scan();

        Assert.Equal(0, written); // hylätty; ei kirjattu vanhalla sukupolvella
        Assert.Empty(_db.ReadRecentEvents(10));
    }

    [Fact]
    public void Scan_SamaLuontiaika_EiNollausta()
    {
        var created = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        _db.SetMeta("windows_last_record_id", "150");
        _db.SetMeta("windows_log_created_utc", created.Ticks.ToString());
        var source = new FakeSource { LogCreationTimeUtc = created };
        source.Events.Add(new WindowsLogEvent(Now,
            "Microsoft-Windows-Kernel-Power", 41, WindowsLevel: 1, RecordId: 200));

        new WindowsEventCollector(source, _db).Scan();

        Assert.Equal(150, source.LastRequestedRecordId);
    }

    [Fact]
    public void Scan_IlmanTapahtumia_SailyttaaKirjanmerkin()
    {
        _db.SetMeta("windows_last_record_id", "150");

        new WindowsEventCollector(new FakeSource(), _db).Scan();

        Assert.Equal("150", _db.GetMeta("windows_last_record_id"));
    }
}
