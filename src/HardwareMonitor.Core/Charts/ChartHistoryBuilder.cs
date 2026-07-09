using HardwareMonitor.Core.Localization;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Charts;

/// <summary>Yksi piste graafissa; null-arvo = aukko viivassa (ei nollaa).</summary>
public sealed record ChartPoint(DateTimeOffset Timestamp, double? Value);

public sealed record ChartSeries(string Name, IReadOnlyList<ChartPoint> Points);

public sealed record ChartHistory(
    IReadOnlyList<ChartSeries> Temperatures,
    IReadOnlyList<ChartSeries> Loads,
    IReadOnlyList<ChartSeries> Fans);

/// <summary>
/// Kokoaa historiagraafien sarjat koosteriveistä (spec
/// docs/superpowers/specs/2026-07-09-history-charts-design.md). Puhdas:
/// harvennus bucket-keskiarvolla, null-aukot säilyvät, samannimiset levyt
/// erotellaan järjestysnumerolla, tuulettimet saavat nimilaput ja aina
/// nollassa olevat tuulettimet suodatetaan pois.
/// </summary>
public static class ChartHistoryBuilder
{
    public static ChartHistory Build(
        IReadOnlyList<SampleRow> rows, int maxPoints,
        IReadOnlyDictionary<string, string> fanLabelsByRawName)
    {
        IReadOnlyList<IReadOnlyList<SampleRow>> buckets = Bucketize(rows, maxPoints);
        DateTimeOffset[] stamps = Timestamps(buckets, rows);

        var temps = new List<ChartSeries>
        {
            Series("CPU", buckets, stamps, r => r.CpuTempAvg),
            Series("GPU", buckets, stamps, r => r.GpuTempAvg),
            Series(Strings.Common_GpuHotspot, buckets, stamps, r => r.GpuHotspotAvg),
        };
        foreach ((string name, int occurrence, string display) in DiskKeys(rows))
        {
            temps.Add(Series(display, buckets, stamps,
                r => Nth(r.Disks, name, occurrence)?.TempAvg));
        }

        var loads = new List<ChartSeries>
        {
            Series("CPU", buckets, stamps, r => r.CpuLoadAvg),
            Series("GPU", buckets, stamps, r => r.GpuLoadAvg),
            Series("RAM", buckets, stamps, r => r.RamLoadAvg),
        };

        var fans = new List<ChartSeries>();
        foreach (string name in rows.SelectMany(r => r.Fans.Select(f => f.Name)).Distinct())
        {
            string display =
                fanLabelsByRawName.TryGetValue(name, out string? label) && label.Length > 0
                    ? label : name;
            ChartSeries series = Series(display, buckets, stamps,
                r => r.Fans.FirstOrDefault(f => f.Name == name)?.RpmAvg);

            // Sarja vain tuulettimelle joka pyörii vähintään 5 % tunnetusta
            // ajasta — GPU-tuulettimien satunnaiset pyörähdykset eivät
            // ansaitse omaa viivaa (käyttäjän palaute 9.7.2026).
            int known = series.Points.Count(p => p.Value.HasValue);
            int spinning = series.Points.Count(p => p.Value is > 0);
            if (spinning > 0 && spinning * 20 >= known)
            {
                fans.Add(series);
            }
        }

        return new ChartHistory(temps, loads, fans);
    }

    private static IReadOnlyList<IReadOnlyList<SampleRow>> Bucketize(
        IReadOnlyList<SampleRow> rows, int maxPoints)
    {
        if (rows.Count <= maxPoints)
        {
            return rows.Select(r => (IReadOnlyList<SampleRow>)new[] { r }).ToList();
        }

        var buckets = new List<IReadOnlyList<SampleRow>>();
        int size = (int)Math.Ceiling(rows.Count / (double)maxPoints);
        for (int start = 0; start < rows.Count; start += size)
        {
            buckets.Add(rows.Skip(start).Take(size).ToList());
        }

        return buckets;
    }

    /// <summary>Lohkon aikaleima on keskimmäisen rivin; ensimmäinen ja viimeinen lohko saavat datan päätepisteiden aikaleimat.</summary>
    private static DateTimeOffset[] Timestamps(
        IReadOnlyList<IReadOnlyList<SampleRow>> buckets, IReadOnlyList<SampleRow> rows)
    {
        var stamps = new DateTimeOffset[buckets.Count];
        for (int i = 0; i < buckets.Count; i++)
        {
            stamps[i] = buckets[i][buckets[i].Count / 2].Timestamp;
        }

        if (buckets.Count > 0)
        {
            stamps[0] = rows[0].Timestamp;
            stamps[^1] = rows[^1].Timestamp;
        }

        return stamps;
    }

    private static ChartSeries Series(
        string name, IReadOnlyList<IReadOnlyList<SampleRow>> buckets,
        DateTimeOffset[] stamps, Func<SampleRow, double?> select)
    {
        var points = new ChartPoint[buckets.Count];
        for (int i = 0; i < buckets.Count; i++)
        {
            double[] values = buckets[i].Select(select)
                .Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            points[i] = new ChartPoint(stamps[i],
                values.Length > 0 ? values.Average() : null);
        }

        return new ChartSeries(name, points);
    }

    private static DiskSampleValue? Nth(
        IReadOnlyList<DiskSampleValue> disks, string name, int occurrence)
    {
        int seen = 0;
        foreach (DiskSampleValue disk in disks)
        {
            if (disk.Name == name && seen++ == occurrence)
            {
                return disk;
            }
        }

        return null;
    }

    /// <summary>Levyavaimet: (nimi, monesko samanniminen); näyttönimi trimmattuna, duplikaateille " #n".</summary>
    private static IEnumerable<(string Name, int Occurrence, string Display)> DiskKeys(
        IReadOnlyList<SampleRow> rows)
    {
        var counts = new Dictionary<string, int>();
        foreach (SampleRow row in rows)
        {
            var inRow = new Dictionary<string, int>();
            foreach (DiskSampleValue disk in row.Disks)
            {
                inRow[disk.Name] = inRow.TryGetValue(disk.Name, out int n) ? n + 1 : 1;
            }

            foreach ((string name, int count) in inRow)
            {
                counts[name] = Math.Max(counts.TryGetValue(name, out int c) ? c : 0, count);
            }
        }

        foreach ((string name, int count) in counts)
        {
            for (int i = 0; i < count; i++)
            {
                yield return (name, i,
                    count > 1 ? $"{name.Trim()} #{i + 1}" : name.Trim());
            }
        }
    }
}
