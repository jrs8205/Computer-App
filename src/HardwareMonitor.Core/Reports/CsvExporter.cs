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

    public static string Build(IReadOnlyList<SampleRow> rows, CultureInfo culture)
    {
        // Sarakkeiden unioni kaikista riveistä esiintymisjärjestyksessä —
        // esim. kesken jakson kytketty USB-levy saa omat sarakkeensa.
        List<string> diskNames = rows.SelectMany(r => r.Disks.Select(d => d.Name))
            .Distinct().ToList();
        List<string> fanNames = rows.SelectMany(r => r.Fans.Select(f => f.Name))
            .Distinct().ToList();

        var sb = new StringBuilder();
        AppendHeader(sb, diskNames, fanNames);

        foreach (SampleRow row in rows)
        {
            AppendRow(sb, row, diskNames, fanNames, culture);
        }

        return sb.ToString();
    }

    private static void AppendHeader(
        StringBuilder sb, List<string> diskNames, List<string> fanNames)
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

        columns.AddRange(diskNames.SelectMany(n =>
            new[]
            {
                Avg(string.Format(Strings.Csv_DiskTemp, n)),
                Max(string.Format(Strings.Csv_DiskTemp, n)),
            }));
        columns.AddRange(fanNames.Select(n => string.Format(Strings.Csv_FanRpm, n)));

        sb.AppendLine(string.Join(Separator, columns.Select(Escape)));
    }

    private static void AppendRow(
        StringBuilder sb, SampleRow row,
        List<string> diskNames, List<string> fanNames, CultureInfo culture)
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

        Dictionary<string, DiskSampleValue> disks = row.Disks
            .GroupBy(d => d.Name).ToDictionary(g => g.Key, g => g.First());
        foreach (string name in diskNames)
        {
            if (disks.TryGetValue(name, out DiskSampleValue? disk))
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
