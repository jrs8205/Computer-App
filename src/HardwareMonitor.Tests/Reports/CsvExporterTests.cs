using System.Globalization;
using HardwareMonitor.Core.Reports;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Reports;

public class CsvExporterTests
{
    private static readonly CultureInfo Finnish = CultureInfo.GetCultureInfo("fi-FI");
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 10, 30, 15, TimeSpan.FromHours(3));

    private static SampleRow Row(
        DateTimeOffset ts, double? cpuTempAvg = 36.5,
        IReadOnlyList<DiskSampleValue>? disks = null,
        IReadOnlyList<FanSampleValue>? fans = null) => new(
        Timestamp: ts,
        CpuLoadAvg: 12, CpuLoadMax: 30,
        CpuTempAvg: cpuTempAvg, CpuTempMax: 58,
        CpuClockMax: 4700,
        CpuPowerAvg: 25, CpuPowerMax: 60,
        GpuLoadAvg: 5, GpuLoadMax: 20,
        GpuTempAvg: 44, GpuTempMax: 48,
        GpuHotspotAvg: 55, GpuHotspotMax: 61,
        GpuPowerAvg: 18, GpuPowerMax: 40,
        VramUsedMbAvg: 900, VramUsedMbMax: 1000,
        RamLoadAvg: 15, RamLoadMax: 21,
        RamUsedGbAvg: 9.5, RamUsedGbMax: 13.2,
        Disks: disks ?? new[] { new DiskSampleValue("970 EVO Plus", 58, 62) },
        Fans: fans ?? new[] { new FanSampleValue("AIO-pumppu", 1955) });

    [Fact]
    public void OtsikotSuomeksiYksikoineen()
    {
        string csv = CsvExporter.Build(new[] { Row(Now) }, Finnish);
        string header = csv.Split('\n')[0];

        Assert.StartsWith("Aika;", header);
        Assert.Contains("CPU lämpö °C (ka)", header);
        Assert.Contains("CPU lämpö °C (max)", header);
        Assert.Contains("RAM käyttö % (max)", header);
        Assert.Contains("Levy 970 EVO Plus lämpö °C (max)", header);
        Assert.Contains("Tuuletin AIO-pumppu RPM (ka)", header);
    }

    [Fact]
    public void ArvotSuomalaisellaDesimaalipilkullaJaPuolipisteella()
    {
        string csv = CsvExporter.Build(new[] { Row(Now) }, Finnish);
        string dataRow = csv.TrimEnd().Split('\n')[1];

        Assert.Contains("9.7.2026 10.30.15", dataRow); // fi-FI: aikaerotin on piste
        Assert.Contains(";36,5;", dataRow); // desimaalipilkku, erotin ;
        Assert.Contains(";1955", dataRow);
    }

    [Fact]
    public void PuuttuvaArvoOnTyhjaKentta()
    {
        string csv = CsvExporter.Build(new[] { Row(Now, cpuTempAvg: null) }, Finnish);
        string dataRow = csv.TrimEnd().Split('\n')[1];

        Assert.Contains(";;", dataRow);
    }

    [Fact]
    public void LevysarakkeetPivotoidaanKaikistaRiveista()
    {
        var rows = new[]
        {
            Row(Now, disks: new[] { new DiskSampleValue("NVMe", 58, 62) }),
            Row(Now.AddSeconds(5), disks: new[]
            {
                new DiskSampleValue("NVMe", 59, 63),
                new DiskSampleValue("USB-levy", 30, 31),
            }),
        };

        string csv = CsvExporter.Build(rows, Finnish);
        string[] lines = csv.TrimEnd().Split('\n');

        Assert.Contains("Levy USB-levy lämpö °C (max)", lines[0]);
        // Ensimmäisellä rivillä USB-levyä ei ollut → sen kentät tyhjiä,
        // mutta sarakemäärä sama kuin otsikossa.
        Assert.Equal(lines[0].Count(c => c == ';'), lines[1].Count(c => c == ';'));
        Assert.Equal(lines[0].Count(c => c == ';'), lines[2].Count(c => c == ';'));
    }
}
