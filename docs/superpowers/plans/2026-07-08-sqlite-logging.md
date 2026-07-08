# Vaihe 3: SQLite-lokitus — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sensorihistoria SQLite-kantaan 5 s koosteriveinä (min/avg/max 1 s -lukemista) ja tapahtumaloki samaan kantaan.

**Architecture:** Puhdas `SampleAggregator` kerää 1 s `KeyMetrics`-lukemat ja tuottaa koosteen joka N:nnellä; `HistoryDb` omistaa SQLite-yhteyden (WAL) ja skeeman; `EventLogService` on ohut tapahtumakerros. `MainViewModel` syöttää aggregaattoria joka tickillä ja kirjoittaa koosteet taustalla.

**Tech Stack:** Microsoft.Data.Sqlite 8.0.11 (Coren ensimmäinen NuGet), xUnit, .NET 8.

## Global Constraints

- TargetFramework `net8.0-windows`; file-scoped namespace, `sealed`, suomenkieliset XML-doc-kommentit.
- **UI-muutosten jälkeen `dotnet build HardwareMonitor.sln`** (`dotnet test` ei buildaa Appia); pysäytä HardwareMonitor.exe ennen buildia.
- Kaikki REAL-arvot nullable — puuttuva sensori ei ole virhe.
- Aikaleimat kantaan unix-sekunteina (UTC, `ToUnixTimeSeconds`).
- Commit-viestit suomeksi + `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Spec: `docs/superpowers/specs/2026-07-08-sqlite-logging-design.md`.

---

### Task 1: LoggingSettings + SampleAggregator (TDD)

**Files:**
- Modify: `src/HardwareMonitor.Core/Settings/AppSettings.cs` (LoggingSettings)
- Create: `src/HardwareMonitor.Core/Storage/AggregatedSample.cs`
- Create: `src/HardwareMonitor.Core/Storage/SampleAggregator.cs`
- Test: `src/HardwareMonitor.Tests/Storage/SampleAggregatorTests.cs`

**Interfaces:**
- Produces: `AppSettings.Logging : LoggingSettings { int SensorIntervalSeconds = 5; int KeepHistoryDays = 30 }`;
  `MetricAggregate(float? Min, float? Avg, float? Max)` + `MetricAggregate.Empty`;
  `DiskAggregate(int Index, string Name, MetricAggregate TempC, float? ActivityMaxPercent)`;
  `FanAggregate(string Identifier, string Name, MetricAggregate Rpm)`;
  `AggregatedSample(DateTimeOffset Timestamp, MetricAggregate CpuLoad, MetricAggregate CpuTemp, float? CpuClockMax, MetricAggregate CpuPower, MetricAggregate GpuLoad, MetricAggregate GpuTemp, MetricAggregate GpuHotspot, MetricAggregate GpuPower, MetricAggregate VramUsedMb, MetricAggregate RamLoad, MetricAggregate RamUsedGb, IReadOnlyList<DiskAggregate> Disks, IReadOnlyList<FanAggregate> Fans)`;
  `SampleAggregator(int samplesPerRow).Add(KeyMetrics m, DateTimeOffset now) -> AggregatedSample?`.

- [ ] **Step 1: Epäonnistuvat testit** `src/HardwareMonitor.Tests/Storage/SampleAggregatorTests.cs`:

```csharp
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Storage;

public class SampleAggregatorTests
{
    private static KeyMetrics Metrics(
        float? cpuLoad = null, float? cpuTemp = null, float? cpuClock = null,
        DiskMetrics[]? disks = null, FanMetrics[]? fans = null) =>
        new(
            CpuLoadPercent: cpuLoad,
            CpuPackageTempC: cpuTemp,
            CpuMaxClockMhz: cpuClock,
            CpuPackagePowerW: null,
            GpuLoadPercent: null,
            GpuTempC: null,
            GpuHotspotTempC: null,
            GpuMemoryUsedMb: null,
            GpuMemoryTotalMb: null,
            GpuPowerW: null,
            RamLoadPercent: null,
            RamUsedGb: null,
            RamAvailableGb: null,
            Disks: disks ?? Array.Empty<DiskMetrics>(),
            Fans: fans ?? Array.Empty<FanMetrics>());

    private static readonly DateTimeOffset T0 = new(2026, 7, 8, 20, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Add_PalauttaaNullKunnesKoosteValmis_JaNollaaLaskurin()
    {
        var agg = new SampleAggregator(samplesPerRow: 3);

        Assert.Null(agg.Add(Metrics(cpuLoad: 10), T0));
        Assert.Null(agg.Add(Metrics(cpuLoad: 20), T0));
        Assert.NotNull(agg.Add(Metrics(cpuLoad: 30), T0));
        Assert.Null(agg.Add(Metrics(cpuLoad: 40), T0)); // uusi jakso alkaa
    }

    [Fact]
    public void Add_LaskeeMinAvgMax_JaOhittaaNullit()
    {
        var agg = new SampleAggregator(samplesPerRow: 3);
        agg.Add(Metrics(cpuLoad: 10, cpuTemp: 50), T0);
        agg.Add(Metrics(cpuLoad: 30, cpuTemp: null), T0); // temp puuttuu välillä
        AggregatedSample s = agg.Add(Metrics(cpuLoad: 50, cpuTemp: 90), T0)!;

        Assert.Equal(10, s.CpuLoad.Min);
        Assert.Equal(30, s.CpuLoad.Avg);
        Assert.Equal(50, s.CpuLoad.Max);
        Assert.Equal(50, s.CpuTemp.Min);
        Assert.Equal(90, s.CpuTemp.Max); // piikki säilyy vaikka välissä null
        Assert.Equal(T0, s.Timestamp);
    }

    [Fact]
    public void Add_KaikkiNull_TuottaaTyhjanAggregaatin()
    {
        var agg = new SampleAggregator(samplesPerRow: 2);
        agg.Add(Metrics(), T0);
        AggregatedSample s = agg.Add(Metrics(), T0)!;

        Assert.Null(s.CpuTemp.Min);
        Assert.Null(s.CpuTemp.Avg);
        Assert.Null(s.CpuTemp.Max);
        Assert.Null(s.CpuClockMax);
    }

    [Fact]
    public void Add_CpuKellostaTallentuuMax()
    {
        var agg = new SampleAggregator(samplesPerRow: 2);
        agg.Add(Metrics(cpuClock: 4600), T0);
        AggregatedSample s = agg.Add(Metrics(cpuClock: 4900), T0)!;

        Assert.Equal(4900, s.CpuClockMax);
    }

    [Fact]
    public void Add_LevytTasmataanIndeksilla_TuulettimetTunnisteella()
    {
        var agg = new SampleAggregator(samplesPerRow: 2);
        agg.Add(Metrics(
            disks: new[] { new DiskMetrics("NVMe", 60f, 5f) },
            fans: new[] { new FanMetrics("Fan #2", 1950f, "/fan/2") }), T0);
        AggregatedSample s = agg.Add(Metrics(
            disks: new[] { new DiskMetrics("NVMe", 70f, 15f) },
            fans: new[] { new FanMetrics("Fan #2", 1960f, "/fan/2") }), T0)!;

        DiskAggregate disk = Assert.Single(s.Disks);
        Assert.Equal("NVMe", disk.Name);
        Assert.Equal(0, disk.Index);
        Assert.Equal(60, disk.TempC.Min);
        Assert.Equal(70, disk.TempC.Max);
        Assert.Equal(15, disk.ActivityMaxPercent);

        FanAggregate fan = Assert.Single(s.Fans);
        Assert.Equal("/fan/2", fan.Identifier);
        Assert.Equal(1950, fan.Rpm.Min);
        Assert.Equal(1960, fan.Rpm.Max);
    }
}
```

- [ ] **Step 2: Aja** `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → FAIL (Storage-nimiavaruutta ei ole).

- [ ] **Step 3: Toteutus.** `AppSettings.cs`iin:

```csharp
/// <summary>Lokituksen asetukset (määrittelyn luku 29).</summary>
public sealed class LoggingSettings
{
    /// <summary>Montako 1 s -lukemaa kootaan yhteen kantariviin.</summary>
    public int SensorIntervalSeconds { get; set; } = 5;

    public int KeepHistoryDays { get; set; } = 30;
}
```
ja `AppSettings`-luokkaan: `public LoggingSettings Logging { get; set; } = new();`

`src/HardwareMonitor.Core/Storage/AggregatedSample.cs`:

```csharp
namespace HardwareMonitor.Core.Storage;

/// <summary>Yhden mittarin kooste lokitusjaksolta. Null = ei yhtään lukemaa.</summary>
public sealed record MetricAggregate(float? Min, float? Avg, float? Max)
{
    public static readonly MetricAggregate Empty = new(null, null, null);
}

/// <summary>Yhden levyn kooste; Index säilyttää järjestyksen samannimisillä levyillä.</summary>
public sealed record DiskAggregate(int Index, string Name, MetricAggregate TempC, float? ActivityMaxPercent);

/// <summary>Yhden tuulettimen kooste; täsmäys pysyvällä tunnisteella.</summary>
public sealed record FanAggregate(string Identifier, string Name, MetricAggregate Rpm);

/// <summary>
/// Yksi kantarivi: lokitusjakson (oletus 5 s) min/keskiarvo/max-kooste
/// 1 s -lukemista. Maksimit säilyttävät sekunnin piikit, vaikka rivejä
/// syntyy vain joka N:s sekunti.
/// </summary>
public sealed record AggregatedSample(
    DateTimeOffset Timestamp,
    MetricAggregate CpuLoad,
    MetricAggregate CpuTemp,
    float? CpuClockMax,
    MetricAggregate CpuPower,
    MetricAggregate GpuLoad,
    MetricAggregate GpuTemp,
    MetricAggregate GpuHotspot,
    MetricAggregate GpuPower,
    MetricAggregate VramUsedMb,
    MetricAggregate RamLoad,
    MetricAggregate RamUsedGb,
    IReadOnlyList<DiskAggregate> Disks,
    IReadOnlyList<FanAggregate> Fans);
```

`src/HardwareMonitor.Core/Storage/SampleAggregator.cs`:

```csharp
using HardwareMonitor.Core.Metrics;

namespace HardwareMonitor.Core.Storage;

/// <summary>
/// Kerää 1 s välein luetut KeyMetrics-lukemat ja tuottaa koosteen
/// (min/avg/max) joka N:nnellä lisäyksellä. Puhdas luokka: ei säikeitä,
/// ei tietokantaa — helposti testattava.
/// </summary>
public sealed class SampleAggregator
{
    private readonly int _samplesPerRow;
    private readonly List<KeyMetrics> _buffer = new();

    public SampleAggregator(int samplesPerRow = 5)
    {
        _samplesPerRow = Math.Max(1, samplesPerRow);
    }

    /// <summary>Lisää lukeman; palauttaa koosteen kun jakso on täynnä, muuten null.</summary>
    public AggregatedSample? Add(KeyMetrics metrics, DateTimeOffset now)
    {
        _buffer.Add(metrics);
        if (_buffer.Count < _samplesPerRow)
        {
            return null;
        }

        AggregatedSample result = Build(now);
        _buffer.Clear();
        return result;
    }

    private AggregatedSample Build(DateTimeOffset now) =>
        new(
            Timestamp: now,
            CpuLoad: Aggregate(m => m.CpuLoadPercent),
            CpuTemp: Aggregate(m => m.CpuPackageTempC),
            CpuClockMax: Aggregate(m => m.CpuMaxClockMhz).Max,
            CpuPower: Aggregate(m => m.CpuPackagePowerW),
            GpuLoad: Aggregate(m => m.GpuLoadPercent),
            GpuTemp: Aggregate(m => m.GpuTempC),
            GpuHotspot: Aggregate(m => m.GpuHotspotTempC),
            GpuPower: Aggregate(m => m.GpuPowerW),
            VramUsedMb: Aggregate(m => m.GpuMemoryUsedMb),
            RamLoad: Aggregate(m => m.RamLoadPercent),
            RamUsedGb: Aggregate(m => m.RamUsedGb),
            Disks: AggregateDisks(),
            Fans: AggregateFans());

    private MetricAggregate Aggregate(Func<KeyMetrics, float?> selector) =>
        ToAggregate(_buffer.Select(selector));

    private static MetricAggregate ToAggregate(IEnumerable<float?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return present.Count == 0
            ? MetricAggregate.Empty
            : new MetricAggregate(present.Min(), (float)present.Average(), present.Max());
    }

    private IReadOnlyList<DiskAggregate> AggregateDisks()
    {
        // Sama levy on samalla indeksillä jokaisessa jakson lukemassa; nimi voi
        // toistua (kaksi samanlaista levyä), joten indeksi erottaa ne.
        int diskCount = _buffer.Count == 0 ? 0 : _buffer.Max(m => m.Disks.Count);
        var result = new List<DiskAggregate>();

        for (int i = 0; i < diskCount; i++)
        {
            var readings = _buffer.Where(m => i < m.Disks.Count).Select(m => m.Disks[i]).ToList();
            if (readings.Count == 0)
            {
                continue;
            }

            var activities = readings.Where(d => d.ActivityPercent.HasValue)
                .Select(d => d.ActivityPercent!.Value).ToList();
            result.Add(new DiskAggregate(
                Index: i,
                Name: readings[0].Name,
                TempC: ToAggregate(readings.Select(d => d.TemperatureC)),
                ActivityMaxPercent: activities.Count == 0 ? null : activities.Max()));
        }

        return result;
    }

    private IReadOnlyList<FanAggregate> AggregateFans() =>
        _buffer.SelectMany(m => m.Fans)
            .GroupBy(f => f.Identifier)
            .Select(g => new FanAggregate(
                Identifier: g.Key,
                Name: g.First().Name,
                Rpm: ToAggregate(g.Select(f => f.Rpm))))
            .ToList();
}
```

Huom: testeissä `FanMetrics("Fan #2", 1950f, "/fan/2")` — järjestys on (Name, Rpm, Identifier).

- [ ] **Step 4: Aja testit** → PASS (19 testiä).
- [ ] **Step 5: Commit** `git add src/HardwareMonitor.Core src/HardwareMonitor.Tests && git commit -m "Lisää SampleAggregator ja lokitusasetukset (TDD)"`

---

### Task 2: HistoryDb + EventLogService (TDD, Microsoft.Data.Sqlite)

**Files:**
- Modify: `src/HardwareMonitor.Core/HardwareMonitor.Core.csproj` (NuGet)
- Create: `src/HardwareMonitor.Core/Storage/HistoryDb.cs`
- Create: `src/HardwareMonitor.Core/Storage/EventLogService.cs`
- Test: `src/HardwareMonitor.Tests/Storage/HistoryDbTests.cs`

**Interfaces:**
- Consumes: `AggregatedSample` ym. (Task 1).
- Produces: `HistoryDb(string? directory) : IDisposable { string DbPath; long InsertSample(AggregatedSample); long CountSamples(); void PurgeOlderThan(DateTimeOffset); void InsertEvent(DateTimeOffset, string level, string component, string? sensor, double? value, double? threshold, string message); IReadOnlyList<EventRow> ReadRecentEvents(int limit) }`;
  `EventRow(DateTimeOffset Timestamp, string Level, string Component, string? Sensor, double? Value, double? Threshold, string Message)`;
  `EventLogService(HistoryDb) { void Info/Warning/Critical/Error(string component, string message, string? sensor = null, double? value = null, double? threshold = null) }`.

- [ ] **Step 1: NuGet** — `HardwareMonitor.Core.csproj`in ItemGroupiin:

```xml
    <!-- SQLite sensorihistorialle ja tapahtumalokille (määrittelyn luku 21). -->
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.11" />
```

- [ ] **Step 2: Epäonnistuvat testit** `src/HardwareMonitor.Tests/Storage/HistoryDbTests.cs`:

```csharp
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
    public void EventLogService_KirjoittaaOikeallaTasolla()
    {
        var service = new EventLogService(_db);

        service.Info("App", "testi");

        EventRow row = Assert.Single(_db.ReadRecentEvents(1));
        Assert.Equal("INFO", row.Level);
        Assert.Equal("App", row.Component);
    }
}
```

- [ ] **Step 3: Aja** → FAIL (HistoryDb puuttuu).

- [ ] **Step 4: Toteutus.** `src/HardwareMonitor.Core/Storage/HistoryDb.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace HardwareMonitor.Core.Storage;

/// <summary>Yksi tapahtumalokin rivi (määrittelyn luku 15).</summary>
public sealed record EventRow(
    DateTimeOffset Timestamp,
    string Level,
    string Component,
    string? Sensor,
    double? Value,
    double? Threshold,
    string Message);

/// <summary>
/// Sensorihistorian ja tapahtumalokin SQLite-kanta
/// (%LOCALAPPDATA%\HardwareMonitor\data\history.db, WAL-tila).
/// Kirjoitukset sarjallistetaan lukolla, koska niitä voi tulla taustasäikeestä.
/// </summary>
public sealed class HistoryDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _lock = new();

    public HistoryDb(string? directory = null)
    {
        directory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor",
            "data");

        Directory.CreateDirectory(directory);
        DbPath = Path.Combine(directory, "history.db");

        _connection = new SqliteConnection($"Data Source={DbPath}");
        _connection.Open();
        Execute("PRAGMA journal_mode=WAL;");
        Execute("PRAGMA foreign_keys=ON;");
        CreateSchema();
    }

    public string DbPath { get; }

    private void CreateSchema() => Execute("""
        CREATE TABLE IF NOT EXISTS samples (
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
        CREATE INDEX IF NOT EXISTS idx_samples_ts ON samples(ts);

        CREATE TABLE IF NOT EXISTS disk_samples (
            sample_id INTEGER NOT NULL REFERENCES samples(id) ON DELETE CASCADE,
            disk_index INTEGER NOT NULL,
            name TEXT NOT NULL,
            temp_avg REAL, temp_max REAL,
            activity_max REAL
        );
        CREATE INDEX IF NOT EXISTS idx_disk_samples ON disk_samples(sample_id);

        CREATE TABLE IF NOT EXISTS fan_samples (
            sample_id INTEGER NOT NULL REFERENCES samples(id) ON DELETE CASCADE,
            identifier TEXT NOT NULL,
            name TEXT NOT NULL,
            rpm_avg REAL, rpm_max REAL
        );
        CREATE INDEX IF NOT EXISTS idx_fan_samples ON fan_samples(sample_id);

        CREATE TABLE IF NOT EXISTS events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            ts INTEGER NOT NULL,
            level TEXT NOT NULL,
            component TEXT NOT NULL,
            sensor TEXT,
            value REAL,
            threshold REAL,
            message TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_events_ts ON events(ts);
        """);

    /// <summary>Kirjoittaa koosteen lapsiriveineen yhtenä transaktiona.</summary>
    public long InsertSample(AggregatedSample s)
    {
        lock (_lock)
        {
            using SqliteTransaction tx = _connection.BeginTransaction();

            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO samples (ts,
                    cpu_load_avg, cpu_load_max, cpu_temp_avg, cpu_temp_max, cpu_clock_max,
                    cpu_power_avg, cpu_power_max, gpu_load_avg, gpu_load_max,
                    gpu_temp_avg, gpu_temp_max, gpu_hotspot_avg, gpu_hotspot_max,
                    gpu_power_avg, gpu_power_max, vram_used_mb_avg, vram_used_mb_max,
                    ram_load_avg, ram_load_max, ram_used_gb_avg, ram_used_gb_max)
                VALUES ($ts,
                    $cpu_load_avg, $cpu_load_max, $cpu_temp_avg, $cpu_temp_max, $cpu_clock_max,
                    $cpu_power_avg, $cpu_power_max, $gpu_load_avg, $gpu_load_max,
                    $gpu_temp_avg, $gpu_temp_max, $gpu_hotspot_avg, $gpu_hotspot_max,
                    $gpu_power_avg, $gpu_power_max, $vram_used_mb_avg, $vram_used_mb_max,
                    $ram_load_avg, $ram_load_max, $ram_used_gb_avg, $ram_used_gb_max);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$ts", s.Timestamp.ToUnixTimeSeconds());
            AddAggregate(cmd, "cpu_load", s.CpuLoad);
            AddAggregate(cmd, "cpu_temp", s.CpuTemp);
            cmd.Parameters.AddWithValue("$cpu_clock_max", (object?)s.CpuClockMax ?? DBNull.Value);
            AddAggregate(cmd, "cpu_power", s.CpuPower);
            AddAggregate(cmd, "gpu_load", s.GpuLoad);
            AddAggregate(cmd, "gpu_temp", s.GpuTemp);
            AddAggregate(cmd, "gpu_hotspot", s.GpuHotspot);
            AddAggregate(cmd, "gpu_power", s.GpuPower);
            AddAggregate(cmd, "vram_used_mb", s.VramUsedMb);
            AddAggregate(cmd, "ram_load", s.RamLoad);
            AddAggregate(cmd, "ram_used_gb", s.RamUsedGb);

            long sampleId = (long)cmd.ExecuteScalar()!;

            foreach (DiskAggregate disk in s.Disks)
            {
                using var diskCmd = _connection.CreateCommand();
                diskCmd.Transaction = tx;
                diskCmd.CommandText = """
                    INSERT INTO disk_samples (sample_id, disk_index, name, temp_avg, temp_max, activity_max)
                    VALUES ($id, $ix, $name, $tavg, $tmax, $act);
                    """;
                diskCmd.Parameters.AddWithValue("$id", sampleId);
                diskCmd.Parameters.AddWithValue("$ix", disk.Index);
                diskCmd.Parameters.AddWithValue("$name", disk.Name);
                diskCmd.Parameters.AddWithValue("$tavg", (object?)disk.TempC.Avg ?? DBNull.Value);
                diskCmd.Parameters.AddWithValue("$tmax", (object?)disk.TempC.Max ?? DBNull.Value);
                diskCmd.Parameters.AddWithValue("$act", (object?)disk.ActivityMaxPercent ?? DBNull.Value);
                diskCmd.ExecuteNonQuery();
            }

            foreach (FanAggregate fan in s.Fans)
            {
                using var fanCmd = _connection.CreateCommand();
                fanCmd.Transaction = tx;
                fanCmd.CommandText = """
                    INSERT INTO fan_samples (sample_id, identifier, name, rpm_avg, rpm_max)
                    VALUES ($id, $ident, $name, $ravg, $rmax);
                    """;
                fanCmd.Parameters.AddWithValue("$id", sampleId);
                fanCmd.Parameters.AddWithValue("$ident", fan.Identifier);
                fanCmd.Parameters.AddWithValue("$name", fan.Name);
                fanCmd.Parameters.AddWithValue("$ravg", (object?)fan.Rpm.Avg ?? DBNull.Value);
                fanCmd.Parameters.AddWithValue("$rmax", (object?)fan.Rpm.Max ?? DBNull.Value);
                fanCmd.ExecuteNonQuery();
            }

            tx.Commit();
            return sampleId;
        }
    }

    private static void AddAggregate(SqliteCommand cmd, string prefix, MetricAggregate a)
    {
        cmd.Parameters.AddWithValue($"${prefix}_avg", (object?)a.Avg ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"${prefix}_max", (object?)a.Max ?? DBNull.Value);
    }

    public long CountSamples()
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM samples;";
            return (long)cmd.ExecuteScalar()!;
        }
    }

    /// <summary>Poistaa rajaa vanhemmat koosteet (cascade lapsiriveihin) ja tapahtumat.</summary>
    public void PurgeOlderThan(DateTimeOffset cutoff)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM samples WHERE ts < $cutoff; DELETE FROM events WHERE ts < $cutoff;";
            cmd.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }
    }

    public void InsertEvent(
        DateTimeOffset ts, string level, string component,
        string? sensor, double? value, double? threshold, string message)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO events (ts, level, component, sensor, value, threshold, message)
                VALUES ($ts, $level, $component, $sensor, $value, $threshold, $message);
                """;
            cmd.Parameters.AddWithValue("$ts", ts.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$level", level);
            cmd.Parameters.AddWithValue("$component", component);
            cmd.Parameters.AddWithValue("$sensor", (object?)sensor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$threshold", (object?)threshold ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$message", message);
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<EventRow> ReadRecentEvents(int limit)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT ts, level, component, sensor, value, threshold, message
                FROM events ORDER BY ts DESC, id DESC LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", limit);

            var result = new List<EventRow>();
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new EventRow(
                    Timestamp: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
                    Level: reader.GetString(1),
                    Component: reader.GetString(2),
                    Sensor: reader.IsDBNull(3) ? null : reader.GetString(3),
                    Value: reader.IsDBNull(4) ? null : reader.GetDouble(4),
                    Threshold: reader.IsDBNull(5) ? null : reader.GetDouble(5),
                    Message: reader.GetString(6)));
            }

            return result;
        }
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
```

`src/HardwareMonitor.Core/Storage/EventLogService.cs`:

```csharp
namespace HardwareMonitor.Core.Storage;

/// <summary>
/// Tapahtumaloki (määrittelyn luku 15): merkittävät tapahtumat tasoilla
/// INFO/WARNING/CRITICAL/ERROR. Vaihe 3 kirjaa sovelluksen elinkaaren;
/// Vaihe 4 lisää raja-arvotapahtumat samaan tauluun.
/// </summary>
public sealed class EventLogService
{
    private readonly HistoryDb _db;

    public EventLogService(HistoryDb db) => _db = db;

    public void Info(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("INFO", component, message, sensor, value, threshold);

    public void Warning(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("WARNING", component, message, sensor, value, threshold);

    public void Critical(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("CRITICAL", component, message, sensor, value, threshold);

    public void Error(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("ERROR", component, message, sensor, value, threshold);

    private void Write(string level, string component, string message,
        string? sensor, double? value, double? threshold) =>
        _db.InsertEvent(DateTimeOffset.Now, level, component, sensor, value, threshold, message);
}
```

- [ ] **Step 5: Aja testit** → PASS (23 testiä).
- [ ] **Step 6: Commit** `git add src/HardwareMonitor.Core src/HardwareMonitor.Tests && git commit -m "Lisää HistoryDb (SQLite WAL) ja EventLogService (TDD)"`

---

### Task 3: MainViewModel-kytkentä + statusrivi + todennus

**Files:**
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs`
- Modify: `docs/ROADMAP.md` (Vaihe 3 valmiiksi; lisäksi kielituki/julkaisu-huomiot Vaihe 8:aan)

**Interfaces:**
- Consumes: `SampleAggregator`, `HistoryDb`, `EventLogService`, `AppSettings.Logging` (Taskit 1–2).

- [ ] **Step 1: MainViewModel** — usingiin `HardwareMonitor.Core.Storage;`, kentät ja kytkennät:

```csharp
    private readonly SampleAggregator _aggregator;
    private HistoryDb? _historyDb;
    private EventLogService? _events;
    private int _rowsLogged;
```

Konstruktoriin (`_settings`-latauksen jälkeen):

```csharp
        _aggregator = new SampleAggregator(_settings.Logging.SensorIntervalSeconds);
```

`Start()`-metodiin onnistuneen `_sensorService.Start()`-kutsun jälkeen:

```csharp
            try
            {
                _historyDb = new HistoryDb();
                _historyDb.PurgeOlderThan(DateTimeOffset.Now.AddDays(-_settings.Logging.KeepHistoryDays));
                _events = new EventLogService(_historyDb);
                _events.Info("App", "Sovellus käynnistyi");
                _logger.Log($"Historia-kanta: {_historyDb.DbPath}");
            }
            catch (Exception ex)
            {
                _historyDb = null;
                _logger.Log($"VIRHE historia-kannan avauksessa: {ex.Message} — jatketaan ilman lokitusta.");
            }
```

`Refresh()`-metodiin `Overlay.Update(...)`-rivin jälkeen:

```csharp
            AggregatedSample? aggregate = _aggregator.Add(metrics, DateTimeOffset.Now);
            if (aggregate is not null && _historyDb is { } db)
            {
                Task.Run(() =>
                {
                    try
                    {
                        db.InsertSample(aggregate);
                        Interlocked.Increment(ref _rowsLogged);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"VIRHE historian kirjoituksessa: {ex.Message}");
                    }
                });
            }
```

Status-rivi (`Refresh()`-metodissa) muotoon:

```csharp
            Status =
                $"Päivitetty {DateTime.Now:HH:mm:ss}  —  " +
                $"{_sensorIndex.Count} sensoria, {Hardware.Count} laitetta  " +
                $"(päivitys #{_tickCount}, lokirivejä: {Volatile.Read(ref _rowsLogged)})";
```

`Dispose()`-metodiin ennen `_sensorService.Dispose()`:

```csharp
        try
        {
            _events?.Info("App", "Sovellus suljettiin siististi");
        }
        catch
        {
            // Sammutus ei saa kaatua lokitukseen.
        }

        _historyDb?.Dispose();
```

- [ ] **Step 2: Buildaa + testit** — `dotnet build HardwareMonitor.sln` → PASS; `dotnet test src/...Tests.csproj` → PASS.

- [ ] **Step 3: Ajonaikainen todennus** — käynnistä adminina, odota ≥15 s, ota
  ruutukaappaus: statusrivillä "lokirivejä: N" (N ≥ 2) ja
  `%LOCALAPPDATA%\HardwareMonitor\data\history.db` on olemassa ja kasvaa.
  Tapahtumat: uusi käynnistys → events-taulussa INFO-rivi (todennus testillä
  tai kannan koolla).

- [ ] **Step 4: ROADMAP** — merkitse Vaihe 3 valmiiksi (SQLite-historia
  koosteriveinä, tapahtumaloki, retention 30 vrk, statusrivin laskuri);
  lisää Vaihe 8:aan: kielituki (fi/en, resx-lokalisointi), LICENSE-tiedosto
  ja julkinen repo, paketointi/asennin (autostart-polun päivitys).

- [ ] **Step 5: Commit** `git add src/HardwareMonitor.App docs/ROADMAP.md && git commit -m "Kytke SQLite-lokitus sovellukseen (Vaihe 3)"`
