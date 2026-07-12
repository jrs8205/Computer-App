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
    private bool _disposed;

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
        MigrateSchema();
    }

    public string DbPath { get; }

    private void CreateSchema() => Execute("""
        CREATE TABLE IF NOT EXISTS samples (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            ts INTEGER NOT NULL,
            cpu_load_avg REAL, cpu_load_max REAL, cpu_load_min REAL,
            cpu_temp_avg REAL, cpu_temp_max REAL, cpu_temp_min REAL,
            cpu_clock_max REAL,
            cpu_power_avg REAL, cpu_power_max REAL, cpu_power_min REAL,
            gpu_load_avg REAL, gpu_load_max REAL, gpu_load_min REAL,
            gpu_temp_avg REAL, gpu_temp_max REAL, gpu_temp_min REAL,
            gpu_hotspot_avg REAL, gpu_hotspot_max REAL, gpu_hotspot_min REAL,
            gpu_power_avg REAL, gpu_power_max REAL, gpu_power_min REAL,
            vram_used_mb_avg REAL, vram_used_mb_max REAL, vram_used_mb_min REAL,
            ram_load_avg REAL, ram_load_max REAL, ram_load_min REAL,
            ram_used_gb_avg REAL, ram_used_gb_max REAL, ram_used_gb_min REAL
        );
        CREATE INDEX IF NOT EXISTS idx_samples_ts ON samples(ts);

        CREATE TABLE IF NOT EXISTS disk_samples (
            sample_id INTEGER NOT NULL REFERENCES samples(id) ON DELETE CASCADE,
            disk_index INTEGER NOT NULL,
            name TEXT NOT NULL,
            temp_avg REAL, temp_max REAL, temp_min REAL,
            activity_max REAL,
            identifier TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS idx_disk_samples ON disk_samples(sample_id);

        CREATE TABLE IF NOT EXISTS fan_samples (
            sample_id INTEGER NOT NULL REFERENCES samples(id) ON DELETE CASCADE,
            identifier TEXT NOT NULL,
            name TEXT NOT NULL,
            rpm_avg REAL, rpm_max REAL, rpm_min REAL
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

        CREATE TABLE IF NOT EXISTS meta (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """);

    /// <summary>
    /// Lisää min-sarakkeet ennen niiden käyttöönottoa luotuihin kantoihin.
    /// CREATE TABLE IF NOT EXISTS ei muuta olemassa olevaa taulua, joten
    /// puuttuvat sarakkeet lisätään ALTER TABLElla (vanhat rivit jäävät null).
    /// </summary>
    private void MigrateSchema()
    {
        (string Table, string Column)[] minColumns =
        {
            ("samples", "cpu_load_min"), ("samples", "cpu_temp_min"),
            ("samples", "cpu_power_min"), ("samples", "gpu_load_min"),
            ("samples", "gpu_temp_min"), ("samples", "gpu_hotspot_min"),
            ("samples", "gpu_power_min"), ("samples", "vram_used_mb_min"),
            ("samples", "ram_load_min"), ("samples", "ram_used_gb_min"),
            ("disk_samples", "temp_min"),
            ("fan_samples", "rpm_min"),
        };

        foreach (IGrouping<string, (string Table, string Column)> group in
                 minColumns.GroupBy(c => c.Table))
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({group.Key});";
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    existing.Add(reader.GetString(1));
                }
            }

            foreach ((string table, string column) in group)
            {
                if (!existing.Contains(column))
                {
                    Execute($"ALTER TABLE {table} ADD COLUMN {column} REAL;");
                }
            }
        }

        // Levyn tunniste (TEXT) — vanhat rivit saavat tyhjän tunnisteen ja
        // täsmätään legacy-avaimella (name + disk_index) lukupolussa.
        if (!ColumnExists("disk_samples", "identifier"))
        {
            Execute("ALTER TABLE disk_samples ADD COLUMN identifier TEXT NOT NULL DEFAULT '';");
        }
    }

    private bool ColumnExists(string table, string column)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
                    cpu_load_avg, cpu_load_max, cpu_load_min,
                    cpu_temp_avg, cpu_temp_max, cpu_temp_min, cpu_clock_max,
                    cpu_power_avg, cpu_power_max, cpu_power_min,
                    gpu_load_avg, gpu_load_max, gpu_load_min,
                    gpu_temp_avg, gpu_temp_max, gpu_temp_min,
                    gpu_hotspot_avg, gpu_hotspot_max, gpu_hotspot_min,
                    gpu_power_avg, gpu_power_max, gpu_power_min,
                    vram_used_mb_avg, vram_used_mb_max, vram_used_mb_min,
                    ram_load_avg, ram_load_max, ram_load_min,
                    ram_used_gb_avg, ram_used_gb_max, ram_used_gb_min)
                VALUES ($ts,
                    $cpu_load_avg, $cpu_load_max, $cpu_load_min,
                    $cpu_temp_avg, $cpu_temp_max, $cpu_temp_min, $cpu_clock_max,
                    $cpu_power_avg, $cpu_power_max, $cpu_power_min,
                    $gpu_load_avg, $gpu_load_max, $gpu_load_min,
                    $gpu_temp_avg, $gpu_temp_max, $gpu_temp_min,
                    $gpu_hotspot_avg, $gpu_hotspot_max, $gpu_hotspot_min,
                    $gpu_power_avg, $gpu_power_max, $gpu_power_min,
                    $vram_used_mb_avg, $vram_used_mb_max, $vram_used_mb_min,
                    $ram_load_avg, $ram_load_max, $ram_load_min,
                    $ram_used_gb_avg, $ram_used_gb_max, $ram_used_gb_min);
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
                    INSERT INTO disk_samples (sample_id, disk_index, name, temp_avg, temp_max, temp_min, activity_max, identifier)
                    VALUES ($id, $ix, $name, $tavg, $tmax, $tmin, $act, $ident);
                    """;
                diskCmd.Parameters.AddWithValue("$id", sampleId);
                diskCmd.Parameters.AddWithValue("$ix", disk.Index);
                diskCmd.Parameters.AddWithValue("$name", disk.Name);
                diskCmd.Parameters.AddWithValue("$tavg", (object?)disk.TempC.Avg ?? DBNull.Value);
                diskCmd.Parameters.AddWithValue("$tmax", (object?)disk.TempC.Max ?? DBNull.Value);
                diskCmd.Parameters.AddWithValue("$tmin", (object?)disk.TempC.Min ?? DBNull.Value);
                diskCmd.Parameters.AddWithValue("$ident", disk.Identifier);
                diskCmd.Parameters.AddWithValue("$act", (object?)disk.ActivityMaxPercent ?? DBNull.Value);
                diskCmd.ExecuteNonQuery();
            }

            foreach (FanAggregate fan in s.Fans)
            {
                using var fanCmd = _connection.CreateCommand();
                fanCmd.Transaction = tx;
                fanCmd.CommandText = """
                    INSERT INTO fan_samples (sample_id, identifier, name, rpm_avg, rpm_max, rpm_min)
                    VALUES ($id, $ident, $name, $ravg, $rmax, $rmin);
                    """;
                fanCmd.Parameters.AddWithValue("$id", sampleId);
                fanCmd.Parameters.AddWithValue("$ident", fan.Identifier);
                fanCmd.Parameters.AddWithValue("$name", fan.Name);
                fanCmd.Parameters.AddWithValue("$ravg", (object?)fan.Rpm.Avg ?? DBNull.Value);
                fanCmd.Parameters.AddWithValue("$rmax", (object?)fan.Rpm.Max ?? DBNull.Value);
                fanCmd.Parameters.AddWithValue("$rmin", (object?)fan.Rpm.Min ?? DBNull.Value);
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
        cmd.Parameters.AddWithValue($"${prefix}_min", (object?)a.Min ?? DBNull.Value);
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
        // Puolustava tarkistus: tulevaisuuteen osoittava cutoff (esim.
        // virheellisestä negatiivisesta retentiosta) poistaisi KAIKEN — ei
        // koskaan pyyhitä nykyhetkeä uudempaa rajaa käyttäen.
        if (cutoff > DateTimeOffset.Now)
        {
            return;
        }

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

    /// <summary>
    /// Kirjoittaa tapahtumat ja yhden tai useamman meta-arvon (esim.
    /// Windows-lokin kirjanmerkin JA sukupolven) yhtenä transaktiona — joko
    /// kaikki tai ei mitään, joten keskeytynyt skannaus ei jätä bookmarkkia
    /// ja sukupolvea eri tahtiin.
    /// </summary>
    public void InsertEventsWithMeta(
        IReadOnlyList<EventRow> events, IReadOnlyList<(string Key, string Value)> meta)
    {
        lock (_lock)
        {
            using SqliteTransaction tx = _connection.BeginTransaction();

            foreach (EventRow e in events)
            {
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO events (ts, level, component, sensor, value, threshold, message)
                    VALUES ($ts, $level, $component, $sensor, $value, $threshold, $message);
                    """;
                cmd.Parameters.AddWithValue("$ts", e.Timestamp.ToUnixTimeSeconds());
                cmd.Parameters.AddWithValue("$level", e.Level);
                cmd.Parameters.AddWithValue("$component", e.Component);
                cmd.Parameters.AddWithValue("$sensor", (object?)e.Sensor ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$value", (object?)e.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$threshold", (object?)e.Threshold ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$message", e.Message);
                cmd.ExecuteNonQuery();
            }

            foreach ((string key, string value) in meta)
            {
                using var metaCmd = _connection.CreateCommand();
                metaCmd.Transaction = tx;
                metaCmd.CommandText = """
                    INSERT INTO meta (key, value) VALUES ($key, $value)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                    """;
                metaCmd.Parameters.AddWithValue("$key", key);
                metaCmd.Parameters.AddWithValue("$value", value);
                metaCmd.ExecuteNonQuery();
            }

            tx.Commit();
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
                result.Add(ReadEventRow(reader));
            }

            return result;
        }
    }

    private static EventRow ReadEventRow(SqliteDataReader reader) => new(
        Timestamp: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
        Level: reader.GetString(1),
        Component: reader.GetString(2),
        Sensor: reader.IsDBNull(3) ? null : reader.GetString(3),
        Value: reader.IsDBNull(4) ? null : reader.GetDouble(4),
        Threshold: reader.IsDBNull(5) ? null : reader.GetDouble(5),
        Message: reader.GetString(6));

    /// <summary>Tapahtumat annetusta hetkestä eteenpäin, uusimmat ensin.</summary>
    public IReadOnlyList<EventRow> ReadEventsSince(DateTimeOffset since)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT ts, level, component, sensor, value, threshold, message
                FROM events WHERE ts >= $since ORDER BY ts DESC, id DESC;
                """;
            cmd.Parameters.AddWithValue("$since", since.ToUnixTimeSeconds());

            var result = new List<EventRow>();
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(ReadEventRow(reader));
            }

            return result;
        }
    }

    /// <summary>Aikavälin avg/max-koosteet riskianalyysille ja konetuntemus-lokille.</summary>
    public SampleStats GetSampleStats(DateTimeOffset since)
    {
        lock (_lock)
        {
            long cutoff = since.ToUnixTimeSeconds();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*),
                    AVG(cpu_temp_avg), MAX(cpu_temp_max),
                    AVG(cpu_load_avg), MAX(cpu_load_max),
                    AVG(gpu_temp_avg), MAX(gpu_temp_max),
                    AVG(gpu_hotspot_avg), MAX(gpu_hotspot_max),
                    AVG(ram_load_avg), MAX(ram_load_max)
                FROM samples WHERE ts >= $since;
                """;
            cmd.Parameters.AddWithValue("$since", cutoff);

            long count;
            MetricStat cpuTemp, cpuLoad, gpuTemp, gpuHotspot, ramLoad;
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                reader.Read();
                count = reader.GetInt64(0);
                cpuTemp = ReadStat(reader, 1);
                cpuLoad = ReadStat(reader, 3);
                gpuTemp = ReadStat(reader, 5);
                gpuHotspot = ReadStat(reader, 7);
                ramLoad = ReadStat(reader, 9);
            }

            var disks = new List<DiskStat>();
            using (var diskCmd = _connection.CreateCommand())
            {
                diskCmd.CommandText = """
                    SELECT d.name, AVG(d.temp_avg), MAX(d.temp_max)
                    FROM disk_samples d JOIN samples s ON s.id = d.sample_id
                    WHERE s.ts >= $since GROUP BY d.name ORDER BY d.name;
                    """;
                diskCmd.Parameters.AddWithValue("$since", cutoff);
                using SqliteDataReader reader = diskCmd.ExecuteReader();
                while (reader.Read())
                {
                    MetricStat stat = ReadStat(reader, 1);
                    disks.Add(new DiskStat(reader.GetString(0), stat.Avg, stat.Max));
                }
            }

            var fans = new List<FanStat>();
            using (var fanCmd = _connection.CreateCommand())
            {
                // Ryhmittely tunnisteella (ei nimellä): samannimiset eri
                // tuulettimet eivät saa sulautua yhdeksi. Nimi valitaan
                // ryhmän edustajaksi (MIN).
                fanCmd.CommandText = """
                    SELECT f.identifier, MIN(f.name), AVG(f.rpm_avg), MAX(f.rpm_max)
                    FROM fan_samples f JOIN samples s ON s.id = f.sample_id
                    WHERE s.ts >= $since GROUP BY f.identifier ORDER BY MIN(f.name);
                    """;
                fanCmd.Parameters.AddWithValue("$since", cutoff);
                using SqliteDataReader reader = fanCmd.ExecuteReader();
                while (reader.Read())
                {
                    MetricStat stat = ReadStat(reader, 2);
                    fans.Add(new FanStat(reader.GetString(1), stat.Avg, stat.Max,
                        Identifier: reader.GetString(0)));
                }
            }

            return new SampleStats(count, cpuTemp, cpuLoad, gpuTemp, gpuHotspot, ramLoad, disks, fans);
        }
    }

    private static MetricStat ReadStat(SqliteDataReader reader, int avgOrdinal) => new(
        Avg: reader.IsDBNull(avgOrdinal) ? null : reader.GetDouble(avgOrdinal),
        Max: reader.IsDBNull(avgOrdinal + 1) ? null : reader.GetDouble(avgOrdinal + 1));

    /// <summary>Koosterivit lapsiriveineen CSV-vientiä varten, vanhin ensin.</summary>
    public IReadOnlyList<SampleRow> ReadSampleRows(DateTimeOffset since)
    {
        lock (_lock)
        {
            long cutoff = since.ToUnixTimeSeconds();

            var disksBySample = new Dictionary<long, List<DiskSampleValue>>();
            using (var diskCmd = _connection.CreateCommand())
            {
                diskCmd.CommandText = """
                    SELECT d.sample_id, d.name, d.temp_avg, d.temp_max, d.temp_min, d.identifier
                    FROM disk_samples d JOIN samples s ON s.id = d.sample_id
                    WHERE s.ts >= $since ORDER BY d.disk_index;
                    """;
                diskCmd.Parameters.AddWithValue("$since", cutoff);
                using SqliteDataReader r = diskCmd.ExecuteReader();
                while (r.Read())
                {
                    long sampleId = r.GetInt64(0);
                    if (!disksBySample.TryGetValue(sampleId, out List<DiskSampleValue>? list))
                    {
                        disksBySample[sampleId] = list = new List<DiskSampleValue>();
                    }

                    list.Add(new DiskSampleValue(
                        r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetDouble(2),
                        r.IsDBNull(3) ? null : r.GetDouble(3),
                        r.IsDBNull(4) ? null : r.GetDouble(4),
                        Identifier: r.GetString(5)));
                }
            }

            var fansBySample = new Dictionary<long, List<FanSampleValue>>();
            using (var fanCmd = _connection.CreateCommand())
            {
                fanCmd.CommandText = """
                    SELECT f.sample_id, f.name, f.rpm_avg, f.rpm_min, f.identifier
                    FROM fan_samples f JOIN samples s ON s.id = f.sample_id
                    WHERE s.ts >= $since;
                    """;
                fanCmd.Parameters.AddWithValue("$since", cutoff);
                using SqliteDataReader r = fanCmd.ExecuteReader();
                while (r.Read())
                {
                    long sampleId = r.GetInt64(0);
                    if (!fansBySample.TryGetValue(sampleId, out List<FanSampleValue>? list))
                    {
                        fansBySample[sampleId] = list = new List<FanSampleValue>();
                    }

                    list.Add(new FanSampleValue(
                        r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetDouble(2),
                        r.IsDBNull(3) ? null : r.GetDouble(3),
                        Identifier: r.GetString(4)));
                }
            }

            var rows = new List<SampleRow>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, ts,
                    cpu_load_avg, cpu_load_max, cpu_temp_avg, cpu_temp_max, cpu_clock_max,
                    cpu_power_avg, cpu_power_max, gpu_load_avg, gpu_load_max,
                    gpu_temp_avg, gpu_temp_max, gpu_hotspot_avg, gpu_hotspot_max,
                    gpu_power_avg, gpu_power_max, vram_used_mb_avg, vram_used_mb_max,
                    ram_load_avg, ram_load_max, ram_used_gb_avg, ram_used_gb_max,
                    cpu_load_min, cpu_temp_min, cpu_power_min,
                    gpu_load_min, gpu_temp_min, gpu_hotspot_min, gpu_power_min,
                    vram_used_mb_min, ram_load_min, ram_used_gb_min
                FROM samples WHERE ts >= $since ORDER BY ts, id;
                """;
            cmd.Parameters.AddWithValue("$since", cutoff);
            using SqliteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                rows.Add(new SampleRow(
                    Timestamp: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                    CpuLoadAvg: Opt(reader, 2), CpuLoadMax: Opt(reader, 3),
                    CpuTempAvg: Opt(reader, 4), CpuTempMax: Opt(reader, 5),
                    CpuClockMax: Opt(reader, 6),
                    CpuPowerAvg: Opt(reader, 7), CpuPowerMax: Opt(reader, 8),
                    GpuLoadAvg: Opt(reader, 9), GpuLoadMax: Opt(reader, 10),
                    GpuTempAvg: Opt(reader, 11), GpuTempMax: Opt(reader, 12),
                    GpuHotspotAvg: Opt(reader, 13), GpuHotspotMax: Opt(reader, 14),
                    GpuPowerAvg: Opt(reader, 15), GpuPowerMax: Opt(reader, 16),
                    VramUsedMbAvg: Opt(reader, 17), VramUsedMbMax: Opt(reader, 18),
                    RamLoadAvg: Opt(reader, 19), RamLoadMax: Opt(reader, 20),
                    RamUsedGbAvg: Opt(reader, 21), RamUsedGbMax: Opt(reader, 22),
                    Disks: disksBySample.TryGetValue(id, out List<DiskSampleValue>? d)
                        ? d : Array.Empty<DiskSampleValue>(),
                    Fans: fansBySample.TryGetValue(id, out List<FanSampleValue>? f)
                        ? f : Array.Empty<FanSampleValue>(),
                    CpuLoadMin: Opt(reader, 23), CpuTempMin: Opt(reader, 24),
                    CpuPowerMin: Opt(reader, 25),
                    GpuLoadMin: Opt(reader, 26), GpuTempMin: Opt(reader, 27),
                    GpuHotspotMin: Opt(reader, 28), GpuPowerMin: Opt(reader, 29),
                    VramUsedMbMin: Opt(reader, 30),
                    RamLoadMin: Opt(reader, 31), RamUsedGbMin: Opt(reader, 32)));
            }

            return rows;
        }
    }

    /// <summary>
    /// Koosterivit harvennettuna SQL:ssä aikabucketteihin (graafeja varten).
    /// Ilman tätä 30 pv alue materialisoisi ~518 000 riviä lapsineen muistiin
    /// ja pitäisi kantalukkoa koko ajan. Avg on avg(avg), max max(max) ja
    /// min min(min) bucketin sisällä; samannimiset levyt pysyvät erillään
    /// disk_index-avaimella ja rivin levyjärjestys säilyy.
    /// </summary>
    public IReadOnlyList<SampleRow> ReadSampleRowsDownsampled(
        DateTimeOffset since, int bucketSeconds)
    {
        bucketSeconds = Math.Max(1, bucketSeconds);

        lock (_lock)
        {
            long cutoff = since.ToUnixTimeSeconds();

            var disksByBucket = new Dictionary<long, List<DiskSampleValue>>();
            using (var diskCmd = _connection.CreateCommand())
            {
                // Ryhmittely tunnisteella (+ legacy disk_index vanhoille tyhjä-
                // tunnisteisille riveille), jotta samannimiset levyt eivät
                // sulaudu vaikka järjestys vaihtuisi.
                diskCmd.CommandText = """
                    SELECT s.ts / $bucket, MIN(d.name),
                        AVG(d.temp_avg), MAX(d.temp_max), MIN(d.temp_min), d.identifier
                    FROM disk_samples d JOIN samples s ON s.id = d.sample_id
                    WHERE s.ts >= $since
                    GROUP BY s.ts / $bucket, d.identifier,
                        CASE WHEN d.identifier = '' THEN d.disk_index ELSE -1 END
                    ORDER BY s.ts / $bucket, d.disk_index;
                    """;
                diskCmd.Parameters.AddWithValue("$since", cutoff);
                diskCmd.Parameters.AddWithValue("$bucket", bucketSeconds);
                using SqliteDataReader r = diskCmd.ExecuteReader();
                while (r.Read())
                {
                    long bucket = r.GetInt64(0);
                    if (!disksByBucket.TryGetValue(bucket, out List<DiskSampleValue>? list))
                    {
                        disksByBucket[bucket] = list = new List<DiskSampleValue>();
                    }

                    list.Add(new DiskSampleValue(
                        r.GetString(1), Opt(r, 2), Opt(r, 3), Opt(r, 4),
                        Identifier: r.GetString(5)));
                }
            }

            var fansByBucket = new Dictionary<long, List<FanSampleValue>>();
            using (var fanCmd = _connection.CreateCommand())
            {
                // Ryhmittely tunnisteella (samannimiset eri tuulettimet eivät
                // sulaudu). Pyörivien ja tunnettujen 5 s rivien lukumäärät
                // kuljetetaan mukana, jotta graafin 5 % -näkyvyysraja voidaan
                // laskea raakariveillä painotettuna (bucket-osuuksien keskiarvo
                // yliedustaisi pieniä bucketteja).
                fanCmd.CommandText = """
                    SELECT s.ts / $bucket, f.identifier, MIN(f.name),
                        AVG(f.rpm_avg), MIN(f.rpm_min),
                        SUM(CASE WHEN f.rpm_avg > 0 THEN 1 ELSE 0 END),
                        COUNT(f.rpm_avg)
                    FROM fan_samples f JOIN samples s ON s.id = f.sample_id
                    WHERE s.ts >= $since
                    GROUP BY s.ts / $bucket, f.identifier;
                    """;
                fanCmd.Parameters.AddWithValue("$since", cutoff);
                fanCmd.Parameters.AddWithValue("$bucket", bucketSeconds);
                using SqliteDataReader r = fanCmd.ExecuteReader();
                while (r.Read())
                {
                    long bucket = r.GetInt64(0);
                    if (!fansByBucket.TryGetValue(bucket, out List<FanSampleValue>? list))
                    {
                        fansByBucket[bucket] = list = new List<FanSampleValue>();
                    }

                    list.Add(new FanSampleValue(
                        r.GetString(2), Opt(r, 3), Opt(r, 4),
                        Identifier: r.GetString(1),
                        SpinningRows: (int)r.GetInt64(5),
                        KnownRows: (int)r.GetInt64(6)));
                }
            }

            var pairs = new List<(long Bucket, SampleRow Row)>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT ts / $bucket, CAST(AVG(ts) AS INTEGER),
                        AVG(cpu_load_avg), MAX(cpu_load_max),
                        AVG(cpu_temp_avg), MAX(cpu_temp_max), MAX(cpu_clock_max),
                        AVG(cpu_power_avg), MAX(cpu_power_max),
                        AVG(gpu_load_avg), MAX(gpu_load_max),
                        AVG(gpu_temp_avg), MAX(gpu_temp_max),
                        AVG(gpu_hotspot_avg), MAX(gpu_hotspot_max),
                        AVG(gpu_power_avg), MAX(gpu_power_max),
                        AVG(vram_used_mb_avg), MAX(vram_used_mb_max),
                        AVG(ram_load_avg), MAX(ram_load_max),
                        AVG(ram_used_gb_avg), MAX(ram_used_gb_max),
                        MIN(cpu_load_min), MIN(cpu_temp_min), MIN(cpu_power_min),
                        MIN(gpu_load_min), MIN(gpu_temp_min),
                        MIN(gpu_hotspot_min), MIN(gpu_power_min),
                        MIN(vram_used_mb_min), MIN(ram_load_min), MIN(ram_used_gb_min)
                    FROM samples WHERE ts >= $since
                    GROUP BY ts / $bucket ORDER BY ts / $bucket;
                    """;
                cmd.Parameters.AddWithValue("$since", cutoff);
                cmd.Parameters.AddWithValue("$bucket", bucketSeconds);
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    long bucket = reader.GetInt64(0);
                    pairs.Add((bucket, new SampleRow(
                        Timestamp: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                        CpuLoadAvg: Opt(reader, 2), CpuLoadMax: Opt(reader, 3),
                        CpuTempAvg: Opt(reader, 4), CpuTempMax: Opt(reader, 5),
                        CpuClockMax: Opt(reader, 6),
                        CpuPowerAvg: Opt(reader, 7), CpuPowerMax: Opt(reader, 8),
                        GpuLoadAvg: Opt(reader, 9), GpuLoadMax: Opt(reader, 10),
                        GpuTempAvg: Opt(reader, 11), GpuTempMax: Opt(reader, 12),
                        GpuHotspotAvg: Opt(reader, 13), GpuHotspotMax: Opt(reader, 14),
                        GpuPowerAvg: Opt(reader, 15), GpuPowerMax: Opt(reader, 16),
                        VramUsedMbAvg: Opt(reader, 17), VramUsedMbMax: Opt(reader, 18),
                        RamLoadAvg: Opt(reader, 19), RamLoadMax: Opt(reader, 20),
                        RamUsedGbAvg: Opt(reader, 21), RamUsedGbMax: Opt(reader, 22),
                        Disks: disksByBucket.TryGetValue(bucket, out List<DiskSampleValue>? d)
                            ? d : Array.Empty<DiskSampleValue>(),
                        Fans: fansByBucket.TryGetValue(bucket, out List<FanSampleValue>? f)
                            ? f : Array.Empty<FanSampleValue>(),
                        CpuLoadMin: Opt(reader, 23), CpuTempMin: Opt(reader, 24),
                        CpuPowerMin: Opt(reader, 25),
                        GpuLoadMin: Opt(reader, 26), GpuTempMin: Opt(reader, 27),
                        GpuHotspotMin: Opt(reader, 28), GpuPowerMin: Opt(reader, 29),
                        VramUsedMbMin: Opt(reader, 30),
                        RamLoadMin: Opt(reader, 31), RamUsedGbMin: Opt(reader, 32))));
                }
            }

            // Puuttuvien buckettien kohdalle all-null-rivi: graafi katkaisee
            // viivan siitä luotettavasti — keskiarvoaikaleimojen värinä estää
            // yhden puuttuvan bucketin tunnistamisen pelkistä aikaleimoista.
            var rows = new List<SampleRow>(pairs.Count + 4);
            for (int i = 0; i < pairs.Count; i++)
            {
                if (i > 0 && pairs[i].Bucket - pairs[i - 1].Bucket >= 2)
                {
                    long gapStart = (pairs[i - 1].Bucket + 1) * bucketSeconds;
                    rows.Add(EmptyRow(DateTimeOffset.FromUnixTimeSeconds(
                        gapStart + bucketSeconds / 2)));
                }

                rows.Add(pairs[i].Row);
            }

            // Aidot päätepisterivit: bucket-keskiarvo hukkaisi todellisen
            // ensimmäisen ja viimeisen näytteen (graafin päätepisteiden
            // tooltipit vastaavat raakarivejä).
            if (rows.Count > 0)
            {
                SampleRow? first = ReadEdgeRow(cutoff, newest: false);
                if (first is not null && first.Timestamp != rows[0].Timestamp)
                {
                    rows.Insert(0, first);
                }

                SampleRow? last = ReadEdgeRow(cutoff, newest: true);
                if (last is not null && last.Timestamp != rows[^1].Timestamp)
                {
                    rows.Add(last);
                }
            }

            return rows;
        }
    }

    private static SampleRow EmptyRow(DateTimeOffset ts) => new(
        ts, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null,
        Array.Empty<DiskSampleValue>(), Array.Empty<FanSampleValue>());

    /// <summary>Alueen ensimmäinen tai viimeinen raaka 5 s rivi lapsineen. Kutsutaan lukon sisältä.</summary>
    private SampleRow? ReadEdgeRow(long cutoff, bool newest)
    {
        string order = newest ? "DESC" : "ASC";

        long id;
        SampleRow row;
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT id, ts,
                    cpu_load_avg, cpu_load_max, cpu_temp_avg, cpu_temp_max, cpu_clock_max,
                    cpu_power_avg, cpu_power_max, gpu_load_avg, gpu_load_max,
                    gpu_temp_avg, gpu_temp_max, gpu_hotspot_avg, gpu_hotspot_max,
                    gpu_power_avg, gpu_power_max, vram_used_mb_avg, vram_used_mb_max,
                    ram_load_avg, ram_load_max, ram_used_gb_avg, ram_used_gb_max,
                    cpu_load_min, cpu_temp_min, cpu_power_min,
                    gpu_load_min, gpu_temp_min, gpu_hotspot_min, gpu_power_min,
                    vram_used_mb_min, ram_load_min, ram_used_gb_min
                FROM samples WHERE ts >= $since
                ORDER BY ts {order}, id {order} LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$since", cutoff);
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            id = reader.GetInt64(0);
            row = new SampleRow(
                Timestamp: DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                CpuLoadAvg: Opt(reader, 2), CpuLoadMax: Opt(reader, 3),
                CpuTempAvg: Opt(reader, 4), CpuTempMax: Opt(reader, 5),
                CpuClockMax: Opt(reader, 6),
                CpuPowerAvg: Opt(reader, 7), CpuPowerMax: Opt(reader, 8),
                GpuLoadAvg: Opt(reader, 9), GpuLoadMax: Opt(reader, 10),
                GpuTempAvg: Opt(reader, 11), GpuTempMax: Opt(reader, 12),
                GpuHotspotAvg: Opt(reader, 13), GpuHotspotMax: Opt(reader, 14),
                GpuPowerAvg: Opt(reader, 15), GpuPowerMax: Opt(reader, 16),
                VramUsedMbAvg: Opt(reader, 17), VramUsedMbMax: Opt(reader, 18),
                RamLoadAvg: Opt(reader, 19), RamLoadMax: Opt(reader, 20),
                RamUsedGbAvg: Opt(reader, 21), RamUsedGbMax: Opt(reader, 22),
                Disks: Array.Empty<DiskSampleValue>(),
                Fans: Array.Empty<FanSampleValue>(),
                CpuLoadMin: Opt(reader, 23), CpuTempMin: Opt(reader, 24),
                CpuPowerMin: Opt(reader, 25),
                GpuLoadMin: Opt(reader, 26), GpuTempMin: Opt(reader, 27),
                GpuHotspotMin: Opt(reader, 28), GpuPowerMin: Opt(reader, 29),
                VramUsedMbMin: Opt(reader, 30),
                RamLoadMin: Opt(reader, 31), RamUsedGbMin: Opt(reader, 32));
        }

        var disks = new List<DiskSampleValue>();
        using (var diskCmd = _connection.CreateCommand())
        {
            diskCmd.CommandText = """
                SELECT name, temp_avg, temp_max, temp_min, identifier FROM disk_samples
                WHERE sample_id = $id ORDER BY disk_index;
                """;
            diskCmd.Parameters.AddWithValue("$id", id);
            using SqliteDataReader r = diskCmd.ExecuteReader();
            while (r.Read())
            {
                disks.Add(new DiskSampleValue(
                    r.GetString(0), Opt(r, 1), Opt(r, 2), Opt(r, 3),
                    Identifier: r.GetString(4)));
            }
        }

        var fans = new List<FanSampleValue>();
        using (var fanCmd = _connection.CreateCommand())
        {
            fanCmd.CommandText = """
                SELECT name, rpm_avg, rpm_min, identifier FROM fan_samples WHERE sample_id = $id;
                """;
            fanCmd.Parameters.AddWithValue("$id", id);
            using SqliteDataReader r = fanCmd.ExecuteReader();
            while (r.Read())
            {
                fans.Add(new FanSampleValue(
                    r.GetString(0), Opt(r, 1), Opt(r, 2), Identifier: r.GetString(3)));
            }
        }

        return row with { Disks = disks, Fans = fans };
    }

    private static double? Opt(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    /// <summary>Avain–arvo-tila (esim. Windows-lokin lukukirjanmerkki).</summary>
    public string? GetMeta(string key)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM meta WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void SetMeta(string key, string value)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO meta (key, value) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Sulkeminen saman _lockin alla kuin kaikki operaatiot: taustatehtävä
        // (sample-, Windows-event-, historia- tai insights-kirjoitus) voi olla
        // aktiivinen tai jonossa lukolle, eikä yhteyttä saa sulkea sen alta.
        // _disposed estää uudet operaatiot; jo alkaneet valmistuvat ensin.
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Microsoft.Data.Sqlite poolaa yhteydet: pelkkä Dispose jättäisi
            // tiedostokahvan poolin haltuun eikä kantatiedostoa voisi poistaa.
            _connection.Close();
            SqliteConnection.ClearPool(_connection);
            _connection.Dispose();
        }
    }
}
