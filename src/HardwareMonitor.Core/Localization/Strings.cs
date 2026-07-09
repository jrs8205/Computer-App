using System.Globalization;
using System.Resources;

namespace HardwareMonitor.Core.Localization;

/// <summary>
/// Coren lokalisoidut tekstit (Strings.resx = fi/neutraali, Strings.en.resx = en).
/// Käsintehty accessor, koska resx-designer-generointi ei toimi dotnet CLI:llä.
/// HUOM: tapahtumien Component-arvot ("Laitteisto", "Järjestelmä", "GPU-ajuri",
/// "Levy", ...) ovat kantaan tallennettavia luokitteluavaimia — niitä EI
/// lokalisoida, muuten historiallisen datan luokittelu hajoaa.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Rm =
        new("HardwareMonitor.Core.Localization.Strings", typeof(Strings).Assembly);

    private static string T(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    // SettingsValidator
    public static string Validate_EnterNumber => T(nameof(Validate_EnterNumber));
    public static string Validate_AllowedRange => T(nameof(Validate_AllowedRange));
    public static string Validate_WarnBelowCrit => T(nameof(Validate_WarnBelowCrit));

    // NotificationBuilder
    public static string Notify_Warning => T(nameof(Notify_Warning));
    public static string Notify_Critical => T(nameof(Notify_Critical));
    public static string Notify_Multiple => T(nameof(Notify_Multiple));

    // ThresholdMonitor
    public static string Threshold_ExceededCritical => T(nameof(Threshold_ExceededCritical));
    public static string Threshold_ExceededWarning => T(nameof(Threshold_ExceededWarning));
    public static string Threshold_FanStopped => T(nameof(Threshold_FanStopped));
    public static string Threshold_FanRecovered => T(nameof(Threshold_FanRecovered));
    public static string Threshold_Recovered => T(nameof(Threshold_Recovered));
    public static string Threshold_LabelCpuTemp => T(nameof(Threshold_LabelCpuTemp));
    public static string Threshold_LabelGpuTemp => T(nameof(Threshold_LabelGpuTemp));
    public static string Threshold_LabelGpuHotspot => T(nameof(Threshold_LabelGpuHotspot));
    public static string Threshold_LabelRamLoad => T(nameof(Threshold_LabelRamLoad));
    public static string Threshold_LabelDiskTemp => T(nameof(Threshold_LabelDiskTemp));

    // WindowsEventClassifier
    public static string WinEvent_KernelPower => T(nameof(WinEvent_KernelPower));
    public static string WinEvent_UnexpectedShutdown => T(nameof(WinEvent_UnexpectedShutdown));
    public static string WinEvent_Bsod => T(nameof(WinEvent_Bsod));
    public static string WinEvent_WheaError => T(nameof(WinEvent_WheaError));
    public static string WinEvent_WheaCorrected => T(nameof(WinEvent_WheaCorrected));
    public static string WinEvent_DisplayDriver => T(nameof(WinEvent_DisplayDriver));
    public static string WinEvent_DiskError => T(nameof(WinEvent_DiskError));

    // Yhteiset mittarinimet
    public static string Common_CpuTemp => T(nameof(Common_CpuTemp));
    public static string Common_GpuTemp => T(nameof(Common_GpuTemp));
    public static string Common_GpuHotspot => T(nameof(Common_GpuHotspot));
    public static string Common_RamLoad => T(nameof(Common_RamLoad));

    // RiskAnalyzer
    public static string Risk_PrevSessionCrashed => T(nameof(Risk_PrevSessionCrashed));
    public static string Risk_FanProblem => T(nameof(Risk_FanProblem));
    public static string Risk_ExceedsCritical => T(nameof(Risk_ExceedsCritical));
    public static string Risk_ExceedsWarning => T(nameof(Risk_ExceedsWarning));
    public static string Risk_DiskFallbackName => T(nameof(Risk_DiskFallbackName));
    public static string Risk_WheaCount => T(nameof(Risk_WheaCount));
    public static string Risk_NoWhea => T(nameof(Risk_NoWhea));
    public static string Risk_CrashCount => T(nameof(Risk_CrashCount));
    public static string Risk_NoCrashes => T(nameof(Risk_NoCrashes));
    public static string Risk_GpuDriverCount => T(nameof(Risk_GpuDriverCount));
    public static string Risk_WinDiskCount => T(nameof(Risk_WinDiskCount));
    public static string Risk_ThresholdCount => T(nameof(Risk_ThresholdCount));
    public static string Risk_PeakCpu => T(nameof(Risk_PeakCpu));
    public static string Risk_PeakHotspot => T(nameof(Risk_PeakHotspot));
    public static string Risk_PeakRam => T(nameof(Risk_PeakRam));
    public static string Risk_StatusCritical => T(nameof(Risk_StatusCritical));
    public static string Risk_StatusWarning => T(nameof(Risk_StatusWarning));
    public static string Risk_StatusGood => T(nameof(Risk_StatusGood));
    public static string Risk_LevelHigh => T(nameof(Risk_LevelHigh));
    public static string Risk_LevelElevated => T(nameof(Risk_LevelElevated));
    public static string Risk_LevelLow => T(nameof(Risk_LevelLow));
    public static string Risk_RecommendWhea => T(nameof(Risk_RecommendWhea));
    public static string Risk_RecommendDisk => T(nameof(Risk_RecommendDisk));
    public static string Risk_RecommendThermal => T(nameof(Risk_RecommendThermal));
    public static string Risk_RecommendSystem => T(nameof(Risk_RecommendSystem));
    public static string Risk_RecommendGpuDriver => T(nameof(Risk_RecommendGpuDriver));

    // CsvExporter
    public static string Csv_Time => T(nameof(Csv_Time));
    public static string Csv_CpuLoad => T(nameof(Csv_CpuLoad));
    public static string Csv_CpuTemp => T(nameof(Csv_CpuTemp));
    public static string Csv_CpuClock => T(nameof(Csv_CpuClock));
    public static string Csv_CpuPower => T(nameof(Csv_CpuPower));
    public static string Csv_GpuLoad => T(nameof(Csv_GpuLoad));
    public static string Csv_GpuTemp => T(nameof(Csv_GpuTemp));
    public static string Csv_GpuHotspot => T(nameof(Csv_GpuHotspot));
    public static string Csv_GpuPower => T(nameof(Csv_GpuPower));
    public static string Csv_Vram => T(nameof(Csv_Vram));
    public static string Csv_RamLoad => T(nameof(Csv_RamLoad));
    public static string Csv_RamUsed => T(nameof(Csv_RamUsed));
    public static string Csv_DiskTemp => T(nameof(Csv_DiskTemp));
    public static string Csv_FanRpm => T(nameof(Csv_FanRpm));
    public static string Csv_SuffixAvg => T(nameof(Csv_SuffixAvg));
    public static string Csv_SuffixMax => T(nameof(Csv_SuffixMax));

    // ReportBuilder
    public static string Report_Title => T(nameof(Report_Title));
    public static string Report_Intro => T(nameof(Report_Intro));
    public static string Report_SummaryHeading => T(nameof(Report_SummaryHeading));
    public static string Report_MachineStatus => T(nameof(Report_MachineStatus));
    public static string Report_RiskLevel => T(nameof(Report_RiskLevel));
    public static string Report_LevelsExplanation => T(nameof(Report_LevelsExplanation));
    public static string Report_Observations => T(nameof(Report_Observations));
    public static string Report_Recommendation => T(nameof(Report_Recommendation));
    public static string Report_CurrentHeading => T(nameof(Report_CurrentHeading));
    public static string Report_DiskLabel => T(nameof(Report_DiskLabel));
    public static string Report_NoReading => T(nameof(Report_NoReading));
    public static string Report_VerdictCritical => T(nameof(Report_VerdictCritical));
    public static string Report_VerdictWarning => T(nameof(Report_VerdictWarning));
    public static string Report_VerdictOk => T(nameof(Report_VerdictOk));
    public static string Report_ValueLine => T(nameof(Report_ValueLine));
    public static string Report_DayHeading => T(nameof(Report_DayHeading));
    public static string Report_NoHistoryPeriod => T(nameof(Report_NoHistoryPeriod));
    public static string Report_PeaksIntro => T(nameof(Report_PeaksIntro));
    public static string Report_PeakExceeded => T(nameof(Report_PeakExceeded));
    public static string Report_PeakNear => T(nameof(Report_PeakNear));
    public static string Report_PeakWellBelow => T(nameof(Report_PeakWellBelow));
    public static string Report_PeakLine => T(nameof(Report_PeakLine));
    public static string Report_MonthHeading => T(nameof(Report_MonthHeading));
    public static string Report_NoHistory => T(nameof(Report_NoHistory));
    public static string Report_TypicalIntro => T(nameof(Report_TypicalIntro));
    public static string Report_LevelLine => T(nameof(Report_LevelLine));
    public static string Report_EventsHeading => T(nameof(Report_EventsHeading));
    public static string Report_NoEvents => T(nameof(Report_NoEvents));
    public static string Report_EventLevelCritical => T(nameof(Report_EventLevelCritical));
    public static string Report_EventLevelWarning => T(nameof(Report_EventLevelWarning));
    public static string Report_EventLevelError => T(nameof(Report_EventLevelError));
    public static string Report_EventLine => T(nameof(Report_EventLine));
    public static string Report_InfoOmitted => T(nameof(Report_InfoOmitted));
    public static string Report_GlossaryHeading => T(nameof(Report_GlossaryHeading));
    public static string Report_Glossary => T(nameof(Report_Glossary));

    // MachineInsightsBuilder
    public static string Insights_Title => T(nameof(Insights_Title));
    public static string Insights_Intro => T(nameof(Insights_Intro));
    public static string Insights_NotEnoughData => T(nameof(Insights_NotEnoughData));
    public static string Insights_LevelsHeading => T(nameof(Insights_LevelsHeading));
    public static string Insights_LevelsTableHeader => T(nameof(Insights_LevelsTableHeader));
    public static string Insights_CpuLoad => T(nameof(Insights_CpuLoad));
    public static string Insights_DisksHeading => T(nameof(Insights_DisksHeading));
    public static string Insights_DisksTableHeader => T(nameof(Insights_DisksTableHeader));
    public static string Insights_FansHeading => T(nameof(Insights_FansHeading));
    public static string Insights_FansTableHeader => T(nameof(Insights_FansTableHeader));
    public static string Insights_EventsHeading => T(nameof(Insights_EventsHeading));
    public static string Insights_EventWhea => T(nameof(Insights_EventWhea));
    public static string Insights_EventCrashes => T(nameof(Insights_EventCrashes));
    public static string Insights_EventThresholds => T(nameof(Insights_EventThresholds));
    public static string Insights_EventGpuDriver => T(nameof(Insights_EventGpuDriver));
    public static string Insights_EventWinDisk => T(nameof(Insights_EventWinDisk));
    public static string Insights_InsightsHeading => T(nameof(Insights_InsightsHeading));
    public static string Insights_CpuHot => T(nameof(Insights_CpuHot));
    public static string Insights_HotspotHot => T(nameof(Insights_HotspotHot));
    public static string Insights_RamHigh => T(nameof(Insights_RamHigh));
    public static string Insights_DiskHot => T(nameof(Insights_DiskHot));
    public static string Insights_Whea => T(nameof(Insights_Whea));
    public static string Insights_Crashes => T(nameof(Insights_Crashes));
    public static string Insights_WinDisk => T(nameof(Insights_WinDisk));
    public static string Insights_AllGood => T(nameof(Insights_AllGood));
}
