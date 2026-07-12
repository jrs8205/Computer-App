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
        bool[] gapAfter = GapsAfter(stamps);

        var temps = new List<ChartSeries>
        {
            Series("CPU", buckets, stamps, gapAfter, rows, r => r.CpuTempAvg),
            Series("GPU", buckets, stamps, gapAfter, rows, r => r.GpuTempAvg),
            Series(Strings.Common_GpuHotspot, buckets, stamps, gapAfter, rows, r => r.GpuHotspotAvg),
        };
        foreach ((string name, int occurrence, string display) in DiskKeys(rows))
        {
            temps.Add(Series(display, buckets, stamps, gapAfter, rows,
                r => Nth(r.Disks, name, occurrence)?.TempAvg));
        }

        var loads = new List<ChartSeries>
        {
            Series("CPU", buckets, stamps, gapAfter, rows, r => r.CpuLoadAvg),
            Series("GPU", buckets, stamps, gapAfter, rows, r => r.GpuLoadAvg),
            Series("RAM", buckets, stamps, gapAfter, rows, r => r.RamLoadAvg),
        };

        var fans = new List<ChartSeries>();
        foreach (string name in rows.SelectMany(r => r.Fans.Select(f => f.Name)).Distinct())
        {
            string display =
                fanLabelsByRawName.TryGetValue(name, out string? label) && label.Length > 0
                    ? label : name;
            ChartSeries series = Series(display, buckets, stamps, gapAfter, rows,
                r => r.Fans.FirstOrDefault(f => f.Name == name)?.RpmAvg);

            // Sarja vain tuulettimelle joka pyörii vähintään 5 % tunnetusta
            // ajasta (käyttäjän palaute 9.7.2026). Osuus lasketaan rivien
            // SpinSharesta — harvennetun bucketin keskiarvo laimenee, joten
            // pisteistä laskettu osuus liioittelisi satunnaisia pyörähdyksiä.
            double shareSum = 0;
            int shareCount = 0;
            foreach (SampleRow row in rows)
            {
                FanSampleValue? fan = row.Fans.FirstOrDefault(f => f.Name == name);
                double? share = fan is null
                    ? null
                    : fan.SpinShare
                      ?? (fan.RpmAvg is { } rpm ? (rpm > 0 ? 1.0 : 0.0) : null);
                if (share is { } s)
                {
                    shareSum += s;
                    shareCount++;
                }
            }

            if (shareSum > 0 && shareSum * 20 >= shareCount)
            {
                fans.Add(series);
            }
        }

        return new ChartHistory(temps, loads, fans);
    }

    /// <summary>
    /// Merkitsee aikaleimavälit, joissa dataa puuttuu (kone sammuksissa tai
    /// unessa): väli yli 3 × mediaanivälin → viivaan piirretään katko.
    /// </summary>
    private static bool[] GapsAfter(DateTimeOffset[] stamps)
    {
        var gaps = new bool[stamps.Length];
        if (stamps.Length < 3)
        {
            return gaps;
        }

        var deltas = new double[stamps.Length - 1];
        for (int i = 1; i < stamps.Length; i++)
        {
            deltas[i - 1] = (stamps[i] - stamps[i - 1]).TotalSeconds;
        }

        double[] sorted = deltas.OrderBy(d => d).ToArray();
        double median = sorted[sorted.Length / 2];
        if (median <= 0)
        {
            return gaps;
        }

        for (int i = 1; i < stamps.Length; i++)
        {
            gaps[i - 1] = (stamps[i] - stamps[i - 1]).TotalSeconds > median * 3;
        }

        return gaps;
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
        DateTimeOffset[] stamps, bool[] gapAfter, IReadOnlyList<SampleRow> rows,
        Func<SampleRow, double?> select)
    {
        var points = new List<ChartPoint>(buckets.Count + 8);
        for (int i = 0; i < buckets.Count; i++)
        {
            double[] values = buckets[i].Select(select)
                .Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            double? value = values.Length > 0 ? values.Average() : null;

            // Päätepisteet saavat datan päätepisteiden ARVOT aikaleimojen
            // lisäksi — myös null säilyy: bucket-keskiarvo aukon kohdalla
            // olisi keksitty mittaus.
            if (i == 0)
            {
                value = select(rows[0]);
            }
            else if (i == buckets.Count - 1)
            {
                value = select(rows[^1]);
            }

            points.Add(new ChartPoint(stamps[i], value));

            if (i < buckets.Count - 1 && gapAfter[i])
            {
                // Null-piste aukon keskelle → LiveCharts katkaisee viivan.
                long mid = (stamps[i].ToUnixTimeSeconds()
                    + stamps[i + 1].ToUnixTimeSeconds()) / 2;
                points.Add(new ChartPoint(DateTimeOffset.FromUnixTimeSeconds(mid), null));
            }
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
