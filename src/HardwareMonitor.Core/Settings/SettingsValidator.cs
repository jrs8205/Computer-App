using System.Globalization;
using HardwareMonitor.Core.Localization;

namespace HardwareMonitor.Core.Settings;

/// <summary>Numerokentän validointitulos: arvo TAI suomenkielinen virheviesti.</summary>
public sealed record ParseResult(float? Value, string? Error)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Asetussivun syötteiden validointi (puhdas, Vaihe 8.2). Parsinta hyväksyy
/// sekä desimaalipilkun (fi) että -pisteen (invariant) — fi kokeillaan ensin,
/// jotta "85,5" ei tulkkaudu invariantin tuhaterottimeksi.
/// </summary>
public static class SettingsValidator
{
    private static readonly CultureInfo Fi = CultureInfo.GetCultureInfo("fi-FI");

    public static ParseResult ParseNumber(string raw, float min, float max)
    {
        string trimmed = raw?.Trim() ?? "";
        if (trimmed.Length == 0
            || (!float.TryParse(trimmed, NumberStyles.Float, Fi, out float value)
                && !float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            || !float.IsFinite(value))
        {
            // TryParse hyväksyy myös "NaN"- ja ääretön-syötteet — NaN läpäisisi
            // raja-arvovertailut ja mykistäisi hälytyksen huomaamatta.
            return new ParseResult(null, Strings.Validate_EnterNumber);
        }

        if (value < min || value > max)
        {
            return new ParseResult(null, string.Format(Strings.Validate_AllowedRange, min, max));
        }

        return new ParseResult(value, null);
    }

    public static string? ValidateWarnCrit(float warn, float crit) =>
        warn >= crit ? Strings.Validate_WarnBelowCrit : null;
}
