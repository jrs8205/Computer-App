using HardwareMonitor.Core.Localization;

namespace HardwareMonitor.Core.WindowsEvents;

/// <summary>Luokiteltu Windows-tapahtuma tapahtumalokiin kirjattavaksi.</summary>
public sealed record WindowsEventClassification(
    string Level,
    string Component,
    string Message);

/// <summary>
/// Päättää mitkä Windowsin System-lokin tapahtumat ovat raudan kannalta
/// merkittäviä ja millä tasolla (designin luokittelutaulukko, luku 18).
/// Puhdas logiikka ilman Windows-riippuvuuksia — yksikkötestattava.
/// </summary>
public static class WindowsEventClassifier
{
    private const byte LevelError = 2;   // Windows: 1=Critical, 2=Error
    private const byte LevelWarning = 3; // 3=Warning, 4=Information

    public static WindowsEventClassification? Classify(WindowsLogEvent e)
    {
        bool isError = e.WindowsLevel is >= 1 and <= LevelError;

        return e.Provider switch
        {
            // HUOM: Component-arvot ("Järjestelmä", "Laitteisto", ...) ovat kantaan
            // tallennettavia luokitteluavaimia — niitä ei lokalisoida.
            "Microsoft-Windows-Kernel-Power" when e.EventId == 41 => new(
                "CRITICAL", "Järjestelmä", Strings.WinEvent_KernelPower),

            "EventLog" when e.EventId == 6008 => new(
                "WARNING", "Järjestelmä", Strings.WinEvent_UnexpectedShutdown),

            "Microsoft-Windows-WER-SystemErrorReporting" when e.EventId == 1001 => new(
                "CRITICAL", "Järjestelmä", Strings.WinEvent_Bsod),

            "Microsoft-Windows-WHEA-Logger" => new(
                isError ? "CRITICAL" : "WARNING",
                "Laitteisto",
                string.Format(
                    isError ? Strings.WinEvent_WheaError : Strings.WinEvent_WheaCorrected,
                    e.EventId)),

            "Display" or "nvlddmkm" when e.WindowsLevel <= LevelWarning => new(
                "WARNING", "GPU-ajuri",
                string.Format(Strings.WinEvent_DisplayDriver, e.Provider, e.EventId)),

            "disk" or "Ntfs" or "storahci" or "stornvme" when e.WindowsLevel <= LevelWarning => new(
                isError ? "CRITICAL" : "WARNING",
                "Levy",
                string.Format(Strings.WinEvent_DiskError, e.Provider, e.EventId)),

            _ => null,
        };
    }
}
