using System.Globalization;
using System.Resources;

namespace HardwareMonitor.App.Localization;

/// <summary>
/// Käyttöliittymän lokalisoidut tekstit (UiStrings.resx = fi/neutraali,
/// UiStrings.en.resx = en). Käsintehty accessor, koska resx-designer-
/// generointi ei toimi dotnet CLI:llä. Staattiset propertyt toimivat
/// XAML:n x:Static-laajennuksen kanssa.
/// </summary>
public static class UiStrings
{
    private static readonly ResourceManager Rm =
        new("HardwareMonitor.App.Localization.UiStrings", typeof(UiStrings).Assembly);

    private static string T(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>LHM:n raa'an sensorityypin lokalisointi; tuntematon tyyppi näytetään sellaisenaan.</summary>
    public static string SensorType(string rawType) =>
        Rm.GetString($"SensorType_{rawType}", CultureInfo.CurrentUICulture) ?? rawType;

    // Ikkuna ja yläpalkki
    public static string WindowTitle => T(nameof(WindowTitle));
    public static string Top_Subtitle => T(nameof(Top_Subtitle));
    public static string Top_OverlayOnDesktop => T(nameof(Top_OverlayOnDesktop));
    public static string Top_MoveOverlay => T(nameof(Top_MoveOverlay));
    public static string Top_MoveOverlayTip => T(nameof(Top_MoveOverlayTip));
    public static string Top_CreateReport => T(nameof(Top_CreateReport));
    public static string Top_CreateReportTip => T(nameof(Top_CreateReportTip));
    public static string Top_ExportCsv => T(nameof(Top_ExportCsv));
    public static string Top_ExportCsvTip => T(nameof(Top_ExportCsvTip));
    public static string Top_CopyInsights => T(nameof(Top_CopyInsights));
    public static string Top_CopyInsightsTip => T(nameof(Top_CopyInsightsTip));
    public static string Top_SaveInsights => T(nameof(Top_SaveInsights));
    public static string Top_SaveInsightsTip => T(nameof(Top_SaveInsightsTip));
    public static string Status_DebugLog => T(nameof(Status_DebugLog));

    // Päivitystarkistus (asetus)
    public static string Set_UpdateCheck => T(nameof(Set_UpdateCheck));
    public static string Set_UpdateCheckTip => T(nameof(Set_UpdateCheckTip));

    // Päivitysilmoitus ja -dialogi
    public static string Upd_NotifyTitle => T(nameof(Upd_NotifyTitle));
    public static string Upd_NotifyMessage => T(nameof(Upd_NotifyMessage));
    public static string Upd_DialogTitle => T(nameof(Upd_DialogTitle));
    public static string Upd_VersionLine => T(nameof(Upd_VersionLine));
    public static string Upd_WhatsNew => T(nameof(Upd_WhatsNew));
    public static string Upd_NoNotes => T(nameof(Upd_NoNotes));
    public static string Upd_InstallNow => T(nameof(Upd_InstallNow));
    public static string Upd_Later => T(nameof(Upd_Later));
    public static string Upd_Downloading => T(nameof(Upd_Downloading));
    public static string Upd_DownloadError => T(nameof(Upd_DownloadError));
    public static string Upd_SignatureError => T(nameof(Upd_SignatureError));
    public static string Upd_NoAsset => T(nameof(Upd_NoAsset));
    public static string Upd_OpenReleasePage => T(nameof(Upd_OpenReleasePage));

    // Välilehdet
    public static string Tab_AllSensors => T(nameof(Tab_AllSensors));
    public static string Tab_Settings => T(nameof(Tab_Settings));
    public static string Tab_History => T(nameof(Tab_History));
    public static string Tab_Maintenance => T(nameof(Tab_Maintenance));

    // Ylläpito-välilehti
    public static string Maint_Intro => T(nameof(Maint_Intro));
    public static string Maint_Motherboard => T(nameof(Maint_Motherboard));
    public static string Maint_Gpu => T(nameof(Maint_Gpu));
    public static string Maint_Disk => T(nameof(Maint_Disk));
    public static string Maint_BiosFormat => T(nameof(Maint_BiosFormat));
    public static string Maint_DriverFormat => T(nameof(Maint_DriverFormat));
    public static string Maint_FirmwareFormat => T(nameof(Maint_FirmwareFormat));
    public static string Maint_CopyModel => T(nameof(Maint_CopyModel));
    public static string Maint_OpenVendorPage => T(nameof(Maint_OpenVendorPage));
    public static string Maint_AppVersion => T(nameof(Maint_AppVersion));
    public static string Maint_CheckNow => T(nameof(Maint_CheckNow));
    public static string Maint_CheckUpToDate => T(nameof(Maint_CheckUpToDate));
    public static string Maint_CheckFound => T(nameof(Maint_CheckFound));
    public static string Maint_CheckFailed => T(nameof(Maint_CheckFailed));

    // Dashboard
    public static string Dash_ColorLegend => T(nameof(Dash_ColorLegend));
    public static string Dash_LegendOk => T(nameof(Dash_LegendOk));
    public static string Dash_LegendWarning => T(nameof(Dash_LegendWarning));
    public static string Dash_LegendCritical => T(nameof(Dash_LegendCritical));
    public static string Dash_LegendSame => T(nameof(Dash_LegendSame));
    public static string Dash_Load => T(nameof(Dash_Load));
    public static string Dash_Temp => T(nameof(Dash_Temp));
    public static string Dash_ClockMax => T(nameof(Dash_ClockMax));
    public static string Dash_Power => T(nameof(Dash_Power));
    public static string Dash_RamInUse => T(nameof(Dash_RamInUse));
    public static string Dash_Disks => T(nameof(Dash_Disks));
    public static string Dash_Fans => T(nameof(Dash_Fans));
    public static string Dash_FanHint => T(nameof(Dash_FanHint));
    public static string Dash_FanRenameTip => T(nameof(Dash_FanRenameTip));
    public static string Dash_InitialTitle => T(nameof(Dash_InitialTitle));
    public static string Dash_Collecting => T(nameof(Dash_Collecting));
    public static string Dash_SummaryTitle => T(nameof(Dash_SummaryTitle));
    public static string Dash_Recommendation => T(nameof(Dash_Recommendation));

    // Asetukset
    public static string Set_GroupGeneral => T(nameof(Set_GroupGeneral));
    public static string Set_MinimizeToTray => T(nameof(Set_MinimizeToTray));
    public static string Set_AutoStart => T(nameof(Set_AutoStart));
    public static string Set_AlertNotifications => T(nameof(Set_AlertNotifications));
    public static string Set_AlertNotificationsTip => T(nameof(Set_AlertNotificationsTip));
    public static string Set_LanguageAuto => T(nameof(Set_LanguageAuto));
    public static string Set_RestartNote => T(nameof(Set_RestartNote));
    public static string Set_InsightsNotes => T(nameof(Set_InsightsNotes));
    public static string Set_InsightsNotesTip => T(nameof(Set_InsightsNotesTip));
    public static string Set_GroupThresholds => T(nameof(Set_GroupThresholds));
    public static string Set_ColWarning => T(nameof(Set_ColWarning));
    public static string Set_ColCritical => T(nameof(Set_ColCritical));
    public static string Set_ResetDefaults => T(nameof(Set_ResetDefaults));
    public static string Set_ResetDefaultsTip => T(nameof(Set_ResetDefaultsTip));
    public static string Set_GroupDurations => T(nameof(Set_GroupDurations));
    public static string Set_GroupLogging => T(nameof(Set_GroupLogging));
    public static string Set_Corner => T(nameof(Set_Corner));
    public static string Set_CornerTopLeft => T(nameof(Set_CornerTopLeft));
    public static string Set_CornerTopRight => T(nameof(Set_CornerTopRight));
    public static string Set_CornerBottomLeft => T(nameof(Set_CornerBottomLeft));
    public static string Set_CornerBottomRight => T(nameof(Set_CornerBottomRight));
    public static string Set_Opacity => T(nameof(Set_Opacity));
    public static string Set_FontSize => T(nameof(Set_FontSize));
    public static string Set_ShownMetrics => T(nameof(Set_ShownMetrics));
    public static string Set_RowNvme => T(nameof(Set_RowNvme));
    public static string Set_RowWarnSustain => T(nameof(Set_RowWarnSustain));
    public static string Set_RowCritSustain => T(nameof(Set_RowCritSustain));
    public static string Set_RowCooldown => T(nameof(Set_RowCooldown));
    public static string Set_RowFanStop => T(nameof(Set_RowFanStop));
    public static string Set_RowInterval => T(nameof(Set_RowInterval));
    public static string Set_RowIntervalNote => T(nameof(Set_RowIntervalNote));
    public static string Set_RowRetention => T(nameof(Set_RowRetention));
    public static string Unit_Days => T(nameof(Unit_Days));

    // Historia
    public static string Hist_Range => T(nameof(Hist_Range));
    public static string Hist_7d => T(nameof(Hist_7d));
    public static string Hist_30d => T(nameof(Hist_30d));
    public static string Hist_AutoRefresh => T(nameof(Hist_AutoRefresh));
    public static string Hist_Temps => T(nameof(Hist_Temps));
    public static string Hist_Loads => T(nameof(Hist_Loads));
    public static string Hist_Fans => T(nameof(Hist_Fans));

    // Tray, dialogit ja tilarivi
    public static string Tray_Show => T(nameof(Tray_Show));
    public static string Tray_Exit => T(nameof(Tray_Exit));
    public static string Dlg_ReportFileName => T(nameof(Dlg_ReportFileName));
    public static string Dlg_ReportFilter => T(nameof(Dlg_ReportFilter));
    public static string Dlg_ReportEmpty => T(nameof(Dlg_ReportEmpty));
    public static string Dlg_CsvFileName => T(nameof(Dlg_CsvFileName));
    public static string Dlg_CsvEmpty => T(nameof(Dlg_CsvEmpty));
    public static string Dlg_InsightsFileName => T(nameof(Dlg_InsightsFileName));
    public static string Dlg_InsightsFilter => T(nameof(Dlg_InsightsFilter));
    public static string Dlg_InsightsEmpty => T(nameof(Dlg_InsightsEmpty));
    public static string Dlg_InsightsCopied => T(nameof(Dlg_InsightsCopied));
    public static string Dlg_SaveFailed => T(nameof(Dlg_SaveFailed));
    public static string Dlg_SavedNotOpened => T(nameof(Dlg_SavedNotOpened));
    public static string Status_Starting => T(nameof(Status_Starting));
    public static string Status_StartError => T(nameof(Status_StartError));
    public static string Status_UpdateError => T(nameof(Status_UpdateError));
    public static string Status_Line => T(nameof(Status_Line));
    public static string Event_AppStarted => T(nameof(Event_AppStarted));
    public static string Event_AppClosed => T(nameof(Event_AppClosed));
    public static string Event_LastState => T(nameof(Event_LastState));
}
