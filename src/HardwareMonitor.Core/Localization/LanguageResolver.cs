using System.Globalization;

namespace HardwareMonitor.Core.Localization;

/// <summary>
/// Ratkaisee UI-kielen asetuksesta: "fi"/"en" suoraan, muu (ml. "" =
/// automaattinen) Windowsin kielestä — suomi suomenkielisellä koneella,
/// muuten englanti.
/// </summary>
public static class LanguageResolver
{
    public static CultureInfo Resolve(string language, CultureInfo installedUi) =>
        language switch
        {
            "fi" => CultureInfo.GetCultureInfo("fi-FI"),
            "en" => CultureInfo.GetCultureInfo("en-US"),
            _ => installedUi.TwoLetterISOLanguageName == "fi"
                ? CultureInfo.GetCultureInfo("fi-FI")
                : CultureInfo.GetCultureInfo("en-US"),
        };
}
