using System.Globalization;
using System.Text;
using HardwareMonitor.Core.Localization;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Reports;

/// <summary>
/// Sensorihistorian CSV-vienti Exceliin (määrittelyn luku 21). Ihmisluettava:
/// suomenkieliset sarakeotsikot yksiköineen, erotin ';' ja desimaalierotin
/// annetun kulttuurin mukaan (suomalainen Excel odottaa puolipistettä ja
/// desimaalipilkkua). Levy- ja tuuletinsarakkeet pivotoidaan nimillä.
/// </summary>
public static class CsvExporter
{
    private const char Separator = ';';

    /// <summary>Levysarake: monesko samanniminen levy rivillä + näyttönimi.</summary>
    private sealed record DiskColumn(string Name, int Occurrence, string Display);

    public static string Build(IReadOnlyList<SampleRow> rows, CultureInfo culture)
    {
        // Sarakkeiden unioni kaikista riveistä esiintymisjärjestyksessä —
        // esim. kesken jakson kytketty USB-levy saa omat sarakkeensa.
        // Samannimiset levyt (kaksi identtistä 860 EVO:ta) saavat omat
        // sarakkeensa esiintymänumerolla kuten graafeissa (invariantti 10).
        List<DiskColumn> diskColumns = DiskColumns(rows);
        List<string> fanNames = rows.SelectMany(r => r.Fans.Select(f => f.Name))
            .Distinct().ToList();

        var sb = new StringBuilder();
        AppendHeader(sb, diskColumns, fanNames);

        foreach (SampleRow row in rows)
        {
            AppendRow(sb, row, diskColumns, fanNames, culture);
        }

        return sb.ToString();
    }

    private static List<DiskColumn> DiskColumns(IReadOnlyList<SampleRow> rows)
    {
        // Nimen sarakemäärä = suurin esiintymämäärä yhdellä rivillä.
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

        var columns = new List<DiskColumn>();
        foreach ((string name, int count) in counts)
        {
            for (int i = 0; i < count; i++)
            {
                columns.Add(new DiskColumn(name, i,
                    count > 1 ? $"{name.Trim()} #{i + 1}" : name.Trim()));
            }
        }

        return columns;
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

    private static void AppendHeader(
        StringBuilder sb, List<DiskColumn> diskColumns, List<string> fanNames)
    {
        static string Avg(string name) => $"{name} {Strings.Csv_SuffixAvg}";
        static string Max(string name) => $"{name} {Strings.Csv_SuffixMax}";

        var columns = new List<string>
        {
            Strings.Csv_Time,
            Avg(Strings.Csv_CpuLoad), Max(Strings.Csv_CpuLoad),
            Avg(Strings.Csv_CpuTemp), Max(Strings.Csv_CpuTemp),
            Max(Strings.Csv_CpuClock),
            Avg(Strings.Csv_CpuPower), Max(Strings.Csv_CpuPower),
            Avg(Strings.Csv_GpuLoad), Max(Strings.Csv_GpuLoad),
            Avg(Strings.Csv_GpuTemp), Max(Strings.Csv_GpuTemp),
            Avg(Strings.Csv_GpuHotspot), Max(Strings.Csv_GpuHotspot),
            Avg(Strings.Csv_GpuPower), Max(Strings.Csv_GpuPower),
            Avg(Strings.Csv_Vram), Max(Strings.Csv_Vram),
            Avg(Strings.Csv_RamLoad), Max(Strings.Csv_RamLoad),
            Avg(Strings.Csv_RamUsed), Max(Strings.Csv_RamUsed),
        };

        columns.AddRange(diskColumns.SelectMany(c =>
            new[]
            {
                Avg(string.Format(Strings.Csv_DiskTemp, c.Display)),
                Max(string.Format(Strings.Csv_DiskTemp, c.Display)),
            }));
        columns.AddRange(fanNames.Select(n => string.Format(Strings.Csv_FanRpm, n)));

        sb.AppendLine(string.Join(Separator, columns.Select(Escape)));
    }

    private static void AppendRow(
        StringBuilder sb, SampleRow row,
        List<DiskColumn> diskColumns, List<string> fanNames, CultureInfo culture)
    {
        var fields = new List<string>
        {
            row.Timestamp.ToLocalTime().ToString("d.M.yyyy HH:mm:ss", culture),
            Num(row.CpuLoadAvg, culture), Num(row.CpuLoadMax, culture),
            Num(row.CpuTempAvg, culture), Num(row.CpuTempMax, culture),
            Num(row.CpuClockMax, culture),
            Num(row.CpuPowerAvg, culture), Num(row.CpuPowerMax, culture),
            Num(row.GpuLoadAvg, culture), Num(row.GpuLoadMax, culture),
            Num(row.GpuTempAvg, culture), Num(row.GpuTempMax, culture),
            Num(row.GpuHotspotAvg, culture), Num(row.GpuHotspotMax, culture),
            Num(row.GpuPowerAvg, culture), Num(row.GpuPowerMax, culture),
            Num(row.VramUsedMbAvg, culture), Num(row.VramUsedMbMax, culture),
            Num(row.RamLoadAvg, culture), Num(row.RamLoadMax, culture),
            Num(row.RamUsedGbAvg, culture), Num(row.RamUsedGbMax, culture),
        };

        foreach (DiskColumn column in diskColumns)
        {
            if (Nth(row.Disks, column.Name, column.Occurrence) is { } disk)
            {
                fields.Add(Num(disk.TempAvg, culture));
                fields.Add(Num(disk.TempMax, culture));
            }
            else
            {
                fields.Add("");
                fields.Add("");
            }
        }

        Dictionary<string, FanSampleValue> fans = row.Fans
            .GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First());
        foreach (string name in fanNames)
        {
            fields.Add(fans.TryGetValue(name, out FanSampleValue? fan)
                ? Num(fan.RpmAvg, culture)
                : "");
        }

        sb.AppendLine(string.Join(Separator, fields.Select(Escape)));
    }

    /// <summary>Enintään yksi desimaali — riittävä tarkkuus, siisti Excelissä.</summary>
    private static string Num(double? value, CultureInfo culture) =>
        value is { } v ? v.ToString("0.#", culture) : "";

    private static string Escape(string field) =>
        field.Contains(Separator) || field.Contains('"') || field.Contains('\n')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}
