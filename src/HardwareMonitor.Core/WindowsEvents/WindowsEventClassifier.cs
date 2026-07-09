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
            "Microsoft-Windows-Kernel-Power" when e.EventId == 41 => new(
                "CRITICAL", "Järjestelmä",
                "Kone sammui yllättäen tai kaatui (Kernel-Power 41)"),

            "EventLog" when e.EventId == 6008 => new(
                "WARNING", "Järjestelmä",
                "Edellinen sammutus oli odottamaton (EventLog 6008)"),

            "Microsoft-Windows-WER-SystemErrorReporting" when e.EventId == 1001 => new(
                "CRITICAL", "Järjestelmä",
                "Windows kaatui siniseen ruutuun eli BSOD (BugCheck 1001)"),

            "Microsoft-Windows-WHEA-Logger" => new(
                isError ? "CRITICAL" : "WARNING",
                "Laitteisto",
                isError
                    ? $"Vakava rautavirhe: CPU/RAM/PCIe (WHEA-Logger {e.EventId})"
                    : $"Korjattu rautavirhe (WHEA-Logger {e.EventId})"),

            "Display" or "nvlddmkm" when e.WindowsLevel <= LevelWarning => new(
                "WARNING", "GPU-ajuri",
                $"Näyttöajurivirhe tai ajurin palautuminen ({e.Provider} {e.EventId})"),

            "disk" or "Ntfs" or "storahci" or "stornvme" when e.WindowsLevel <= LevelWarning => new(
                isError ? "CRITICAL" : "WARNING",
                "Levy",
                $"Levy- tai tiedostojärjestelmävirhe ({e.Provider} {e.EventId})"),

            _ => null,
        };
    }
}
