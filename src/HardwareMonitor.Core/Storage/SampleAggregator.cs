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
        // Täsmäys ensisijaisesti LHM:n pysyvällä laitetunnisteella: jos levy
        // irtoaa tai ilmestyy kesken jakson, indeksi- tai esiintymäpohjainen
        // avain siirtyisi ja eri fyysisten levyjen lukemat sekoittuisivat.
        // Nimi + esiintymä on varapolku tunnisteettomille lukemille.
        var readingsByKey = new Dictionary<string, List<DiskMetrics>>();
        var keyOrder = new List<(string Key, string Name)>();

        foreach (KeyMetrics m in _buffer)
        {
            var seen = new Dictionary<string, int>();
            foreach (DiskMetrics disk in m.Disks)
            {
                string key;
                if (disk.Identifier.Length > 0)
                {
                    key = "id:" + disk.Identifier;
                }
                else
                {
                    int occurrence = seen.TryGetValue(disk.Name, out int n) ? n : 0;
                    seen[disk.Name] = occurrence + 1;
                    key = $"name:{disk.Name}#{occurrence}";
                }

                if (!readingsByKey.TryGetValue(key, out List<DiskMetrics>? list))
                {
                    readingsByKey[key] = list = new List<DiskMetrics>();
                    keyOrder.Add((key, disk.Name));
                }

                list.Add(disk);
            }
        }

        var result = new List<DiskAggregate>();
        for (int i = 0; i < keyOrder.Count; i++)
        {
            List<DiskMetrics> readings = readingsByKey[keyOrder[i].Key];
            var activities = readings.Where(d => d.ActivityPercent.HasValue)
                .Select(d => d.ActivityPercent!.Value).ToList();
            result.Add(new DiskAggregate(
                Index: i,
                Name: keyOrder[i].Name,
                TempC: ToAggregate(readings.Select(d => d.TemperatureC)),
                ActivityMaxPercent: activities.Count == 0 ? null : activities.Max(),
                Identifier: readings[0].Identifier));
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
