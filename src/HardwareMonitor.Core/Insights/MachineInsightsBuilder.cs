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
        (DateTimeOffset now, MachineSpec spec, SampleStats stats, SampleStats stats7d,
            IReadOnlyList<EventRow> events, ThresholdSettings limits) = input;
        var sb = new StringBuilder();
        sb.AppendLine(Strings.Insights_Title);
        sb.AppendLine();
        sb.AppendLine(string.Format(Strings.Insights_Intro, now).ReplaceLineEndings());
        sb.AppendLine();
        AppendAiIntro(sb);
        AppendSpec(sb, spec);

        // Vain taso- ja trendiosiot vaativat sensoridataa — tapahtumat (esim.
        // Windows-lokin Kernel-Power/WHEA) voivat olla kannassa jo ennen
        // ensimmäistäkään näytettä, eivätkä ne saa kadota juuri silloin.
        if (stats.SampleCount == 0)
        {
            sb.AppendLine(Strings.Insights_NotEnoughData);
            sb.AppendLine();
        }
        else
        {
            AppendLevels(sb, stats, limits);
            AppendTrends(sb, stats, stats7d);
        }

        (int whea, int crashes, int thresholds, int gpuDriver, int winDisk) = CountEvents(events);
        AppendEvents(sb, whea, crashes, thresholds, gpuDriver, winDisk);
        AppendRecentEvents(sb, events);
        AppendInsights(sb, stats, limits, whea, crashes, winDisk);

        return sb.ToString();
    }

    private static void AppendAiIntro(StringBuilder sb)
    {
        sb.AppendLine(Strings.Insights_AiIntroHeading);
        sb.AppendLine();
        sb.AppendLine(Strings.Insights_AiIntroBody.ReplaceLineEndings());
        sb.AppendLine();
    }

    private static void AppendSpec(StringBuilder sb, MachineSpec spec)
    {
        sb.AppendLine(Strings.Insights_SpecHeading);
        sb.AppendLine();
        sb.AppendLine(string.Format(Strings.Insights_SpecCpu, spec.CpuName ?? "—"));
        sb.AppendLine(string.Format(Strings.Insights_SpecGpu, spec.GpuName ?? "—"));
        sb.AppendLine(string.Format(
            Strings.Insights_SpecMotherboard, spec.MotherboardName ?? "—"));
        sb.AppendLine(string.Format(
            Strings.Insights_SpecRam, spec.RamTotalGb is { } gb ? $"{gb} GB" : "—"));
        sb.AppendLine(string.Format(Strings.Insights_SpecDisks, FormatDisks(spec.DiskNames)));
        sb.AppendLine(string.Format(
            Strings.Insights_SpecOs,
            string.IsNullOrWhiteSpace(spec.OsDescription) ? "—" : spec.OsDescription));
        if (!string.IsNullOrWhiteSpace(spec.UserNotes))
        {
            sb.AppendLine(string.Format(Strings.Insights_SpecNotes, spec.UserNotes.Trim()));
        }

        sb.AppendLine();
    }

    /// <summary>Samannimiset levyt ryhmitellään: "2 × Samsung SSD 860 EVO 1TB".</summary>
    private static string FormatDisks(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return "—";
        }

        return string.Join("; ", names
            .GroupBy(n => n)
            .Select(g => g.Count() > 1 ? $"{g.Count()} × {g.Key}" : g.Key));
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

    private const double TempTrendThreshold = 3;
    private const double PercentTrendThreshold = 10;

    private static void AppendTrends(StringBuilder sb, SampleStats stats30, SampleStats stats7)
    {
        sb.AppendLine(Strings.Insights_TrendsHeading);
        sb.AppendLine();

        if (stats7.SampleCount == 0)
        {
            sb.AppendLine(Strings.Insights_TrendsNotEnough);
            sb.AppendLine();
            return;
        }

        var lines = new List<string>();
        AddTrend(lines, Strings.Common_CpuTemp,
            stats7.CpuTemp.Avg, stats30.CpuTemp.Avg, "°C", TempTrendThreshold);
        AddTrend(lines, Strings.Insights_CpuLoad,
            stats7.CpuLoad.Avg, stats30.CpuLoad.Avg, "%", PercentTrendThreshold);
        AddTrend(lines, Strings.Common_GpuTemp,
            stats7.GpuTemp.Avg, stats30.GpuTemp.Avg, "°C", TempTrendThreshold);
        AddTrend(lines, Strings.Common_GpuHotspot,
            stats7.GpuHotspot.Avg, stats30.GpuHotspot.Avg, "°C", TempTrendThreshold);
        AddTrend(lines, Strings.Common_RamLoad,
            stats7.RamLoad.Avg, stats30.RamLoad.Avg, "%", PercentTrendThreshold);
        foreach (DiskStat disk7 in stats7.Disks)
        {
            DiskStat? disk30 = stats30.Disks.FirstOrDefault(d => d.Name == disk7.Name);
            if (disk30 is not null)
            {
                AddTrend(lines, disk7.Name,
                    disk7.TempAvg, disk30.TempAvg, "°C", TempTrendThreshold);
            }
        }

        if (lines.Count == 0)
        {
            sb.AppendLine(Strings.Insights_TrendsNone);
        }
        else
        {
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
        }

        sb.AppendLine();
    }

    private static void AddTrend(
        List<string> lines, string label,
        double? avg7, double? avg30, string unit, double threshold)
    {
        if (avg7 is not { } a7 || avg30 is not { } a30 || Math.Abs(a7 - a30) < threshold)
        {
            return;
        }

        string format = a7 > a30 ? Strings.Insights_TrendRise : Strings.Insights_TrendFall;
        lines.Add(string.Format(format, label, Fmt(a30, unit), Fmt(a7, unit)));
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

    private static void AppendRecentEvents(StringBuilder sb, IReadOnlyList<EventRow> events)
    {
        var recent = events
            .Where(e => e.Level != "INFO")
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToList();
        if (recent.Count == 0)
        {
            return;
        }

        sb.AppendLine(Strings.Insights_RecentEventsHeading);
        sb.AppendLine();
        foreach (EventRow e in recent)
        {
            // Aikaleima paikallisajassa — kannassa aika on UTC-offsetilla.
            sb.AppendLine(string.Format(
                Strings.Insights_RecentEventLine, e.Timestamp.ToLocalTime(), e.Level, e.Message));
        }

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
