using System.Text;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Localization;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Reports;

/// <summary>
/// "Luo raportti" (määrittelyn luku 20). Raportti on tarkoituksella
/// selkokielinen: se luetaan Notepadissa sellaisenaan, joten ei taulukoita
/// vaan otsikot ja kokonaiset lauseet, ja jokainen arvo saa selityksen
/// ("kunnossa", "kävi lähellä varoitusrajaa"). Lopussa sanasto.
/// Puhdas funktio — kaikki data annetaan parametreina.
/// </summary>
public static class ReportBuilder
{
    /// <summary>Näin montaa yksikköä lähempänä varoitusrajaa arvo on "lähellä rajaa".</summary>
    private const double NearLimitMargin = 10;

    public static string Build(
        DateTimeOffset now,
        RiskAssessment assessment,
        KeyMetrics metrics,
        MetricStates states,
        SampleStats dayStats,
        SampleStats monthStats,
        IReadOnlyList<EventRow> dayEvents,
        ThresholdSettings limits)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Format(Strings.Report_Title, now));
        sb.AppendLine();
        sb.AppendLine(Strings.Report_Intro.ReplaceLineEndings());
        sb.AppendLine();

        AppendSummary(sb, assessment);
        AppendCurrentValues(sb, metrics, states, limits);
        AppendDayPeaks(sb, dayStats, limits);
        AppendMonthLevels(sb, monthStats);
        AppendEvents(sb, dayEvents);
        AppendGlossary(sb);

        return sb.ToString();
    }

    private static void AppendSummary(StringBuilder sb, RiskAssessment a)
    {
        sb.AppendLine(Strings.Report_SummaryHeading);
        sb.AppendLine();
        sb.AppendLine(string.Format(Strings.Report_MachineStatus, a.Status));
        sb.AppendLine(string.Format(Strings.Report_RiskLevel, a.RiskLevel));
        sb.AppendLine();
        sb.AppendLine(Strings.Report_LevelsExplanation.ReplaceLineEndings());
        sb.AppendLine();
        sb.AppendLine(Strings.Report_Observations);
        foreach (string observation in a.Observations)
        {
            sb.AppendLine($"- {observation}");
        }

        if (a.Recommendation is { } recommendation)
        {
            sb.AppendLine();
            sb.AppendLine(string.Format(Strings.Report_Recommendation, recommendation));
        }

        sb.AppendLine();
    }

    private static void AppendCurrentValues(
        StringBuilder sb, KeyMetrics m, MetricStates states, ThresholdSettings limits)
    {
        sb.AppendLine(Strings.Report_CurrentHeading);
        sb.AppendLine();

        AppendValueLine(sb, Strings.Common_CpuTemp, m.CpuPackageTempC, "°C",
            states.CpuTemp, limits.CpuWarningTemp);
        AppendValueLine(sb, Strings.Common_GpuTemp, m.GpuTempC, "°C",
            states.GpuTemp, limits.GpuWarningTemp);
        AppendValueLine(sb, Strings.Common_GpuHotspot, m.GpuHotspotTempC, "°C",
            states.GpuHotspot, limits.GpuHotspotWarningTemp);
        AppendValueLine(sb, Strings.Common_RamLoad, m.RamLoadPercent, "%",
            states.RamLoad, limits.RamWarningPercent);

        for (int i = 0; i < m.Disks.Count; i++)
        {
            ThresholdState state = i < states.Disks.Count
                ? states.Disks[i]
                : ThresholdState.Normal;
            AppendValueLine(sb,
                string.Format(Strings.Report_DiskLabel, m.Disks[i].Name.Trim()),
                m.Disks[i].TemperatureC, "°C", state, limits.NvmeWarningTemp);
        }

        sb.AppendLine();
    }

    private static void AppendValueLine(
        StringBuilder sb, string label, float? value, string unit,
        ThresholdState state, float warnLimit)
    {
        if (value is not { } v)
        {
            sb.AppendLine(string.Format(Strings.Report_NoReading, label));
            return;
        }

        string verdict = state switch
        {
            ThresholdState.Critical => Strings.Report_VerdictCritical,
            ThresholdState.Warning => Strings.Report_VerdictWarning,
            _ => string.Format(Strings.Report_VerdictOk, warnLimit, unit),
        };
        sb.AppendLine(string.Format(Strings.Report_ValueLine, label, v, unit, verdict));
    }

    private static void AppendDayPeaks(StringBuilder sb, SampleStats stats, ThresholdSettings limits)
    {
        sb.AppendLine(Strings.Report_DayHeading);
        sb.AppendLine();

        if (stats.SampleCount == 0)
        {
            sb.AppendLine(Strings.Report_NoHistoryPeriod);
            sb.AppendLine();
            return;
        }

        sb.AppendLine(Strings.Report_PeaksIntro);
        AppendPeakLine(sb, Strings.Common_CpuTemp, stats.CpuTemp.Max, "°C", limits.CpuWarningTemp);
        AppendPeakLine(sb, Strings.Common_GpuTemp, stats.GpuTemp.Max, "°C", limits.GpuWarningTemp);
        AppendPeakLine(sb, Strings.Common_GpuHotspot, stats.GpuHotspot.Max, "°C", limits.GpuHotspotWarningTemp);
        AppendPeakLine(sb, Strings.Common_RamLoad, stats.RamLoad.Max, "%", limits.RamWarningPercent);
        foreach (DiskStat disk in stats.Disks)
        {
            AppendPeakLine(sb, string.Format(Strings.Report_DiskLabel, disk.Name.Trim()),
                disk.TempMax, "°C", limits.NvmeWarningTemp);
        }

        sb.AppendLine();
    }

    private static void AppendPeakLine(
        StringBuilder sb, string label, double? max, string unit, float warnLimit)
    {
        if (max is not { } v)
        {
            return;
        }

        string comparison = string.Format(
            v >= warnLimit
                ? Strings.Report_PeakExceeded
                : warnLimit - v <= NearLimitMargin
                    ? Strings.Report_PeakNear
                    : Strings.Report_PeakWellBelow,
            warnLimit, unit);
        sb.AppendLine(string.Format(Strings.Report_PeakLine, label, v, unit, comparison));
    }

    private static void AppendMonthLevels(StringBuilder sb, SampleStats stats)
    {
        sb.AppendLine(Strings.Report_MonthHeading);
        sb.AppendLine();

        if (stats.SampleCount == 0)
        {
            sb.AppendLine(Strings.Report_NoHistory);
            sb.AppendLine();
            return;
        }

        sb.AppendLine(Strings.Report_TypicalIntro);
        AppendLevelLine(sb, Strings.Common_CpuTemp, stats.CpuTemp, "°C");
        AppendLevelLine(sb, Strings.Common_GpuTemp, stats.GpuTemp, "°C");
        AppendLevelLine(sb, Strings.Common_GpuHotspot, stats.GpuHotspot, "°C");
        AppendLevelLine(sb, Strings.Common_RamLoad, stats.RamLoad, "%");
        foreach (DiskStat disk in stats.Disks)
        {
            if (disk.TempAvg is { } avg && disk.TempMax is { } max)
            {
                sb.AppendLine(string.Format(Strings.Report_LevelLine,
                    string.Format(Strings.Report_DiskLabel, disk.Name.Trim()), avg, "°C", max));
            }
        }

        sb.AppendLine();
    }

    private static void AppendLevelLine(StringBuilder sb, string label, MetricStat stat, string unit)
    {
        if (stat.Avg is { } avg && stat.Max is { } max)
        {
            sb.AppendLine(string.Format(Strings.Report_LevelLine, label, avg, unit, max));
        }
    }

    private static void AppendEvents(StringBuilder sb, IReadOnlyList<EventRow> events)
    {
        sb.AppendLine(Strings.Report_EventsHeading);
        sb.AppendLine();

        List<EventRow> important = events.Where(e => e.Level != "INFO").ToList();
        if (important.Count == 0)
        {
            sb.AppendLine(Strings.Report_NoEvents);
        }
        else
        {
            foreach (EventRow e in important)
            {
                string level = e.Level switch
                {
                    "CRITICAL" => Strings.Report_EventLevelCritical,
                    "WARNING" => Strings.Report_EventLevelWarning,
                    _ => Strings.Report_EventLevelError,
                };
                sb.AppendLine(string.Format(Strings.Report_EventLine,
                    e.Timestamp.ToLocalTime(), level, e.Message));
            }

            sb.AppendLine();
            sb.AppendLine(Strings.Report_InfoOmitted);
        }

        sb.AppendLine();
    }

    private static void AppendGlossary(StringBuilder sb)
    {
        sb.AppendLine(Strings.Report_GlossaryHeading);
        sb.AppendLine();
        sb.AppendLine(Strings.Report_Glossary.ReplaceLineEndings());
    }
}
