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

        CREATE TABLE IF NOT EXISTS meta (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
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
                fanCmd.CommandText = """
                    SELECT f.name, AVG(f.rpm_avg), MAX(f.rpm_max)
                    FROM fan_samples f JOIN samples s ON s.id = f.sample_id
                    WHERE s.ts >= $since GROUP BY f.name ORDER BY f.name;
                    """;
                fanCmd.Parameters.AddWithValue("$since", cutoff);
                using SqliteDataReader reader = fanCmd.ExecuteReader();
                while (reader.Read())
                {
                    MetricStat stat = ReadStat(reader, 1);
                    fans.Add(new FanStat(reader.GetString(0), stat.Avg, stat.Max));
                }
            }

            return new SampleStats(count, cpuTemp, cpuLoad, gpuTemp, gpuHotspot, ramLoad, disks, fans);
        }
    }

    private static MetricStat ReadStat(SqliteDataReader reader, int avgOrdinal) => new(
        Avg: reader.IsDBNull(avgOrdinal) ? null : reader.GetDouble(avgOrdinal),
        Max: reader.IsDBNull(avgOrdinal + 1) ? null : reader.GetDouble(avgOrdinal + 1));

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
        // Microsoft.Data.Sqlite poolaa yhteydet: pelkkä Dispose jättäisi
        // tiedostokahvan poolin haltuun eikä kantatiedostoa voisi poistaa.
        _connection.Close();
        SqliteConnection.ClearPool(_connection);
        _connection.Dispose();
    }
}
