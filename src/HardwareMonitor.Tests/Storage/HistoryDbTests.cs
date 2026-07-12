using HardwareMonitor.Core.Storage;
using Microsoft.Data.Sqlite;
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
    public void ReadSampleRows_PalauttaaMyosMinimit()
    {
        // Invariantti 6: jokainen 5 s rivi tallentaa min/avg/max — myös minimit.
        _db.InsertSample(Sample(Now));

        SampleRow row = Assert.Single(_db.ReadSampleRows(Now.AddHours(-24)));

        Assert.Equal(10, row.CpuLoadMin);
        Assert.Equal(50, row.CpuTempMin);
        DiskSampleValue disk = Assert.Single(row.Disks);
        Assert.Equal(60, disk.TempMin);
        FanSampleValue fan = Assert.Single(row.Fans);
        Assert.Equal(1950, fan.RpmMin);
    }

    [Fact]
    public void VanhaKantaIlmanMinSarakkeita_MigratoituuAvattaessa()
    {
        // Simuloidaan ennen min-sarakkeita luotua kantaa: luodaan taulut
        // vanhalla skeemalla ja avataan sitten HistoryDb samaan polkuun.
        string dir = Path.Combine(_dir, "vanha");
        Directory.CreateDirectory(dir);
        string dbPath = Path.Combine(dir, "history.db");
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE samples (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ts INTEGER NOT NULL,
                    cpu_load_avg REAL, cpu_load_max REAL,
                    cpu_temp_avg REAL, cpu_temp_max REAL,
                    cpu_clock_max REAL,
                    cpu_power_avg REAL, cpu_power_max REAL,
                    gpu_load_avg REAL, gpu_load_max REAL,
                    gpu_temp_avg REAL, gpu_temp_max REAL,
                    gpu_hotspot_avg REAL, gpu_hotspot_max REAL,
                    gpu_power_avg REAL, gpu_power_max REAL,
                    vram_used_mb_avg REAL, vram_used_mb_max REAL,
                    ram_load_avg REAL, ram_load_max REAL,
                    ram_used_gb_avg REAL, ram_used_gb_max REAL
                );
                CREATE TABLE disk_samples (
                    sample_id INTEGER NOT NULL REFERENCES samples(id) ON DELETE CASCADE,
                    disk_index INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    temp_avg REAL, temp_max REAL,
                    activity_max REAL
                );
                CREATE TABLE fan_samples (
                    sample_id INTEGER NOT NULL REFERENCES samples(id) ON DELETE CASCADE,
                    identifier TEXT NOT NULL,
                    name TEXT NOT NULL,
                    rpm_avg REAL, rpm_max REAL
                );
                INSERT INTO samples (ts, cpu_temp_avg, cpu_temp_max) VALUES (1, 60, 70);
                """;
            cmd.ExecuteNonQuery();
            SqliteConnection.ClearPool(conn);
        }

        using var db = new HistoryDb(dir);
        db.InsertSample(Sample(Now));

        // Vanha rivi ilman minimejä palautuu nullina, uusi rivi minimeineen.
        IReadOnlyList<SampleRow> rows = db.ReadSampleRows(DateTimeOffset.FromUnixTimeSeconds(0));
        Assert.Equal(2, rows.Count);
        Assert.Null(rows[0].CpuTempMin);
        Assert.Equal(50, rows[1].CpuTempMin);
    }

    [Fact]
    public void ReadSampleRowsDownsampled_YhdistaaRivitBucketeiksi()
    {
        // 30 pv alue toi ennen ~518 000 riviä muistiin — harvennus tehdään
        // SQL:ssä niin, että kutsuja saa suoraan bucket-koosteet.
        _db.InsertSample(Sample(Now, cpuTempMax: 70f));
        _db.InsertSample(Sample(Now.AddSeconds(5), cpuTempMax: 90f));
        _db.InsertSample(Sample(Now.AddSeconds(10), cpuTempMax: 80f));
        _db.InsertSample(Sample(Now.AddSeconds(15), cpuTempMax: 60f));

        IReadOnlyList<SampleRow> rows =
            _db.ReadSampleRowsDownsampled(Now.AddHours(-1), bucketSeconds: 10);

        // [raaka 1. rivi, bucket 1, bucket 2, raaka viimeinen rivi]
        Assert.Equal(4, rows.Count);
        Assert.Equal(90, rows[1].CpuTempMax);  // max(max) bucketin sisällä
        Assert.Equal(60, rows[1].CpuTempAvg);  // avg(avg)
        Assert.Equal(50, rows[1].CpuTempMin);  // min(min)
        Assert.Equal(80, rows[2].CpuTempMax);
        Assert.True(rows[0].Timestamp < rows[1].Timestamp);
        Assert.True(rows[2].Timestamp < rows[3].Timestamp);

        DiskSampleValue disk = Assert.Single(rows[1].Disks);
        Assert.Equal("NVMe", disk.Name);
        Assert.Equal(61, disk.TempAvg);
        Assert.Equal(62, disk.TempMax);

        FanSampleValue fan = Assert.Single(rows[1].Fans);
        Assert.Equal("Fan #2", fan.Name);
        Assert.Equal(1955, fan.RpmAvg);
    }

    [Fact]
    public void ReadSampleRowsDownsampled_LaskeeTuulettimenPyorimisosuuden()
    {
        // Bucket-keskiarvo laimenee (yksi pyörähdys → pieni positiivinen avg),
        // joten pyörimisosuus lasketaan 5 s riveistä erikseen.
        AggregatedSample Fan(DateTimeOffset ts, float rpm) => Sample(ts) with
        {
            Fans = new[] { new FanAggregate("/gpu/fan/1", "GPU Fan 1",
                new MetricAggregate(rpm, rpm, rpm)) },
        };

        _db.InsertSample(Fan(Now, 1000));
        _db.InsertSample(Fan(Now.AddSeconds(5), 0));
        _db.InsertSample(Fan(Now.AddSeconds(10), 0));
        _db.InsertSample(Fan(Now.AddSeconds(15), 0));

        IReadOnlyList<SampleRow> rows =
            _db.ReadSampleRowsDownsampled(Now.AddHours(-1), bucketSeconds: 60);

        // Bucket-koosterivi on raakapäätepisteiden välissä.
        SampleRow bucketRow = Assert.Single(
            rows, r => r.Fans.Any(f => f.KnownRows is not null));
        FanSampleValue fan = Assert.Single(bucketRow.Fans);
        Assert.Equal(250, fan.RpmAvg);
        Assert.Equal(1, fan.SpinningRows);  // vain 1000 RPM -rivi pyöri
        Assert.Equal(4, fan.KnownRows);
        Assert.Equal("/gpu/fan/1", fan.Identifier);
    }

    [Fact]
    public void ReadSampleRowsDownsampled_SamannimisetEriTunnisteet_PysyvatErillaan()
    {
        AggregatedSample TwoFans(DateTimeOffset ts) => Sample(ts) with
        {
            Fans = new[]
            {
                new FanAggregate("/mb/fan/1", "Fan #1", new MetricAggregate(500, 500, 500)),
                new FanAggregate("/gpu/fan/1", "Fan #1", new MetricAggregate(1500, 1500, 1500)),
            },
        };
        _db.InsertSample(TwoFans(Now));
        _db.InsertSample(TwoFans(Now.AddSeconds(5)));

        IReadOnlyList<SampleRow> rows =
            _db.ReadSampleRowsDownsampled(Now.AddHours(-1), bucketSeconds: 60);

        SampleRow row = rows.First(r => r.Fans.Count == 2);
        Assert.Contains(row.Fans, f => f.Identifier == "/mb/fan/1" && f.RpmAvg == 500);
        Assert.Contains(row.Fans, f => f.Identifier == "/gpu/fan/1" && f.RpmAvg == 1500);
    }

    [Fact]
    public void ReadSampleRowsDownsampled_SailyttaaAidotPaatepisterivit()
    {
        // Bucket-keskiarvoistus hukkaisi todelliset päätepistenäytteet —
        // graafin ensimmäisen ja viimeisen pisteen pitää vastata raakarivejä.
        _db.InsertSample(Sample(Now, cpuTempMax: 70f));
        _db.InsertSample(Sample(Now.AddSeconds(5), cpuTempMax: 90f));
        _db.InsertSample(Sample(Now.AddSeconds(10), cpuTempMax: 80f));
        _db.InsertSample(Sample(Now.AddSeconds(15), cpuTempMax: 60f));

        IReadOnlyList<SampleRow> rows =
            _db.ReadSampleRowsDownsampled(Now.AddHours(-1), bucketSeconds: 60);

        Assert.Equal(Now.ToUnixTimeSeconds(), rows[0].Timestamp.ToUnixTimeSeconds());
        Assert.Equal(70, rows[0].CpuTempMax);  // raaka ensimmäinen rivi
        Assert.Equal(
            Now.AddSeconds(15).ToUnixTimeSeconds(),
            rows[^1].Timestamp.ToUnixTimeSeconds());
        Assert.Equal(60, rows[^1].CpuTempMax); // raaka viimeinen rivi
        DiskSampleValue disk = Assert.Single(rows[0].Disks);
        Assert.Equal("NVMe", disk.Name);       // lapsirivit mukana
    }

    [Fact]
    public void ReadSampleRowsDownsampled_PuuttuvaBucketTuottaaNullRivin()
    {
        // Kone sammuksissa bucketin verran → väliin syntyy all-null-rivi,
        // josta graafi katkaisee viivan luotettavasti (aikaleimaheuristiikka
        // ei erota yhtä puuttuvaa buckettia keskiarvoaikaleimojen värinästä).
        _db.InsertSample(Sample(Now));
        _db.InsertSample(Sample(Now.AddSeconds(130)));

        IReadOnlyList<SampleRow> rows =
            _db.ReadSampleRowsDownsampled(Now.AddHours(-1), bucketSeconds: 60);

        int gapIndex = rows.ToList().FindIndex(r =>
            r.CpuTempAvg is null && r.CpuLoadAvg is null && r.Disks.Count == 0);
        Assert.True(gapIndex > 0, "null-riviä ei löytynyt datan välistä");
        Assert.True(gapIndex < rows.Count - 1);
        Assert.True(rows[gapIndex].Timestamp > rows[0].Timestamp);
        Assert.True(rows[gapIndex].Timestamp < rows[^1].Timestamp);
    }

    [Fact]
    public void ReadSampleRowsDownsampled_SamannimisetLevytSailyvatErillaan()
    {
        AggregatedSample sample = Sample(Now) with
        {
            Disks = new[]
            {
                new DiskAggregate(0, "860 EVO", new MetricAggregate(30, 31, 32), 5),
                new DiskAggregate(1, "860 EVO", new MetricAggregate(50, 51, 52), 5),
            },
        };
        _db.InsertSample(sample);
        _db.InsertSample(sample with { Timestamp = Now.AddSeconds(5) });

        IReadOnlyList<SampleRow> rows =
            _db.ReadSampleRowsDownsampled(Now.AddHours(-1), bucketSeconds: 60);

        // rows[1] on bucket-koosterivi (raakapäätepisteet ympärillä).
        SampleRow row = rows[1];
        Assert.Equal(2, row.Disks.Count);
        Assert.Equal(31, row.Disks[0].TempAvg); // disk_index-järjestys säilyy
        Assert.Equal(51, row.Disks[1].TempAvg);
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
    public void InsertEventsWithMeta_KirjoittaaTapahtumatJaKirjanmerkinYhdessa()
    {
        // Windows-lokin skannaus kirjoittaa tapahtumat ja kirjanmerkin samassa
        // transaktiossa — keskeytynyt skannaus ei saa tuottaa duplikaatteja.
        var rows = new[]
        {
            new EventRow(Now, "WARNING", "Laitteisto", "whea", 19, null, "WHEA-virhe"),
            new EventRow(Now.AddMinutes(1), "CRITICAL", "Järjestelmä", "kernel", 41, null, "Kernel-Power 41"),
        };

        _db.InsertEventsWithMeta(rows, new[]
        {
            ("windows_last_record_id", "123"),
            ("windows_log_created_utc", "456"),
        });

        Assert.Equal(2, _db.ReadRecentEvents(10).Count);
        Assert.Equal("123", _db.GetMeta("windows_last_record_id"));
        Assert.Equal("456", _db.GetMeta("windows_log_created_utc"));
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
