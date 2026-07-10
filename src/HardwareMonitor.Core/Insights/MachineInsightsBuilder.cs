using System.Text;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Localization;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Insights;

/// <summary>
/// Rakentaa konetuntemus-lokin (machine-insights.md): jatkuvasti päivittyvä
/// yhteenveto koneen normaalitasoista, huipuista, tapahtumista ja
/// optimointiehdotuksista. Tarkoitettu sekä käyttäjän että tekoälyavustajan
/// (Claude) luettavaksi tulevissa istunnoissa. Puhdas funktio — testattava.
/// </summary>
public static class MachineInsightsBuilder
{
    private static readonly HashSet<string> WindowsDiskProviders =
        new(StringComparer.OrdinalIgnoreCase) { "disk", "Ntfs", "storahci", "stornvme" };

    public static string Build(MachineInsightsInput input)
    {
        (DateTimeOffset now, MachineSpec _, SampleStats stats, SampleStats _,
            IReadOnlyList<EventRow> events, ThresholdSettings limits) = input;
        var sb = new StringBuilder();
        sb.AppendLine(Strings.Insights_Title);
        sb.AppendLine();
        sb.AppendLine(string.Format(Strings.Insights_Intro, now).ReplaceLineEndings());
        sb.AppendLine();

        if (stats.SampleCount == 0)
        {
            sb.AppendLine(Strings.Insights_NotEnoughData);
            return sb.ToString();
        }

        AppendLevels(sb, stats, limits);
        (int whea, int crashes, int thresholds, int gpuDriver, int winDisk) = CountEvents(events);
        AppendEvents(sb, whea, crashes, thresholds, gpuDriver, winDisk);
        AppendInsights(sb, stats, limits, whea, crashes, winDisk);

        return sb.ToString();
    }

    private static void AppendLevels(StringBuilder sb, SampleStats stats, ThresholdSettings limits)
    {
        sb.AppendLine(Strings.Insights_LevelsHeading);
        sb.AppendLine();
        sb.AppendLine(Strings.Insights_LevelsTableHeader);
        sb.AppendLine("|---|---|---|---|");
        AppendRow(sb, Strings.Common_CpuTemp, stats.CpuTemp, "°C", limits.CpuWarningTemp);
        AppendRow(sb, Strings.Insights_CpuLoad, stats.CpuLoad, "%", null);
        AppendRow(sb, Strings.Common_GpuTemp, stats.GpuTemp, "°C", limits.GpuWarningTemp);
        AppendRow(sb, Strings.Common_GpuHotspot, stats.GpuHotspot, "°C", limits.GpuHotspotWarningTemp);
        AppendRow(sb, Strings.Common_RamLoad, stats.RamLoad, "%", limits.RamWarningPercent);
        sb.AppendLine();

        if (stats.Disks.Count > 0)
        {
            sb.AppendLine(Strings.Insights_DisksHeading);
            sb.AppendLine();
            sb.AppendLine(Strings.Insights_DisksTableHeader);
            sb.AppendLine("|---|---|---|---|");
            foreach (DiskStat disk in stats.Disks)
            {
                sb.AppendLine(
                    $"| {disk.Name} | {Fmt(disk.TempAvg, "°C")} | {Fmt(disk.TempMax, "°C")} " +
                    $"| {limits.NvmeWarningTemp:0} °C |");
            }

            sb.AppendLine();
        }

        if (stats.Fans.Count > 0)
        {
            sb.AppendLine(Strings.Insights_FansHeading);
            sb.AppendLine();
            sb.AppendLine(Strings.Insights_FansTableHeader);
            sb.AppendLine("|---|---|---|");
            foreach (FanStat fan in stats.Fans)
            {
                sb.AppendLine($"| {fan.Name} | {Fmt(fan.RpmAvg, "")} | {Fmt(fan.RpmMax, "")} |");
            }

            sb.AppendLine();
        }
    }

    private static void AppendRow(
        StringBuilder sb, string label, MetricStat stat, string unit, float? warnLimit)
    {
        string limit = warnLimit is { } w ? $"{w:0} {unit}" : "—";
        sb.AppendLine($"| {label} | {Fmt(stat.Avg, unit)} | {Fmt(stat.Max, unit)} | {limit} |");
    }

    private static (int Whea, int Crashes, int Thresholds, int GpuDriver, int WinDisk)
        CountEvents(IReadOnlyList<EventRow> events)
    {
        int whea = 0, crashes = 0, thresholds = 0, gpuDriver = 0, winDisk = 0;
        foreach (EventRow e in events)
        {
            if (e.Level == "INFO")
            {
                continue;
            }

            switch (e.Component)
            {
                case "Laitteisto": whea++; break;
                case "Järjestelmä": crashes++; break;
                case "GPU-ajuri": gpuDriver++; break;
                case "Levy" when e.Sensor is { } s && WindowsDiskProviders.Contains(s):
                    winDisk++;
                    break;
                default: thresholds++; break;
            }
        }

        return (whea, crashes, thresholds, gpuDriver, winDisk);
    }

    private static void AppendEvents(
        StringBuilder sb, int whea, int crashes, int thresholds, int gpuDriver, int winDisk)
    {
        sb.AppendLine(Strings.Insights_EventsHeading);
        sb.AppendLine();
        sb.AppendLine(string.Format(Strings.Insights_EventWhea, whea));
        sb.AppendLine(string.Format(Strings.Insights_EventCrashes, crashes));
        sb.AppendLine(string.Format(Strings.Insights_EventThresholds, thresholds));
        sb.AppendLine(string.Format(Strings.Insights_EventGpuDriver, gpuDriver));
        sb.AppendLine(string.Format(Strings.Insights_EventWinDisk, winDisk));
        sb.AppendLine();
    }

    private static void AppendInsights(
        StringBuilder sb, SampleStats stats, ThresholdSettings limits,
        int whea, int crashes, int winDisk)
    {
        sb.AppendLine(Strings.Insights_InsightsHeading);
        sb.AppendLine();

        var insights = new List<string>();

        if (stats.CpuTemp.Max is { } cpuMax && cpuMax >= limits.CpuWarningTemp)
        {
            insights.Add(string.Format(Strings.Insights_CpuHot, cpuMax, limits.CpuWarningTemp));
        }

        if (stats.GpuHotspot.Max is { } hotMax && hotMax >= limits.GpuHotspotWarningTemp)
        {
            insights.Add(string.Format(
                Strings.Insights_HotspotHot, hotMax, limits.GpuHotspotWarningTemp));
        }

        if (stats.RamLoad.Max is { } ramMax && ramMax >= limits.RamWarningPercent)
        {
            insights.Add(string.Format(
                Strings.Insights_RamHigh, ramMax, limits.RamWarningPercent));
        }

        foreach (DiskStat disk in stats.Disks)
        {
            if (disk.TempMax is { } max && max >= limits.NvmeWarningTemp)
            {
                insights.Add(string.Format(
                    Strings.Insights_DiskHot, disk.Name, max, limits.NvmeWarningTemp));
            }
        }

        if (whea > 0)
        {
            insights.Add(string.Format(Strings.Insights_Whea, whea));
        }

        if (crashes > 0)
        {
            insights.Add(string.Format(Strings.Insights_Crashes, crashes));
        }

        if (winDisk > 0)
        {
            insights.Add(string.Format(Strings.Insights_WinDisk, winDisk));
        }

        if (insights.Count == 0)
        {
            insights.Add(Strings.Insights_AllGood);
        }

        foreach (string insight in insights)
        {
            sb.AppendLine($"- {insight}");
        }
    }

    private static string Fmt(double? value, string unit) =>
        value is { } v ? $"{v:0} {unit}".TrimEnd() : "—";
}
