namespace HardwareMonitor.Core.Maintenance;

/// <summary>Laitetyyppi linkinmuodostusta varten — ROG-mallisivupolku pätee vain emolevyihin.</summary>
public enum DeviceKind
{
    Motherboard,
    Gpu,
    Disk,
}

/// <summary>
/// Laitenimi → valmistajan tukisivun osoite. Pääsääntönä EI syvälinkkejä
/// mallisivuille (osoitteet muuttuvat ja hajoavat hiljaa) — poikkeuksena
/// ASUS ROG -emolevyt, joiden mallisivun osoite johdetaan laitenimestä
/// (käyttäjän vahvistama muoto 14.7.2026). Aluevalinta tulee Windowsin
/// kieliasetuksesta, ei IP-paikannuksesta.
/// </summary>
public static class VendorLinkResolver
{
    public static string? Resolve(string? deviceName, string language, DeviceKind kind)
    {
        string name = deviceName?.Trim() ?? "";
        if (name.Length == 0)
        {
            return null;
        }

        bool finnish = string.Equals(language, "fi", StringComparison.OrdinalIgnoreCase);

        if (name.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
        {
            if (kind == DeviceKind.Motherboard &&
                name.Contains("ROG", StringComparison.OrdinalIgnoreCase) &&
                RogMotherboardUrl(name, finnish) is { } rogUrl)
            {
                return rogUrl;
            }

            return finnish ? "https://www.asus.com/fi/support/" : "https://www.asus.com/support/";
        }

        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            // NVIDIAlla ei ole suomenkielistä sivustoa (fi-fi/drivers → 404) —
            // globaali ajurihakusivu toimii kaikilla kielillä.
            return "https://www.nvidia.com/Download/index.aspx";
        }

        if (name.Contains("Samsung SSD", StringComparison.OrdinalIgnoreCase))
        {
            return "https://semiconductor.samsung.com/consumer-storage/support/tools/";
        }

        return null;
    }

    /// <summary>
    /// "ASUS ROG STRIX Z390-F GAMING" →
    /// https://rog.asus.com/fi/motherboards/rog-strix/rog-strix-z390-f-gaming-model/
    /// Sarjapolku = slugin kaksi ensimmäistä sanaa. Liian lyhyt nimi
    /// (ei sarjaa + mallia) → null, jolloin käytetään yleistä tukisivua.
    /// </summary>
    private static string? RogMotherboardUrl(string name, bool finnish)
    {
        string[] tokens = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.Equals("ASUS", StringComparison.OrdinalIgnoreCase))
            .Select(t => new string(t.ToLowerInvariant()
                .Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray()))
            .Where(t => t.Length > 0)
            .ToArray();
        if (tokens.Length < 3)
        {
            return null;
        }

        string slug = string.Join("-", tokens);
        string series = $"{tokens[0]}-{tokens[1]}";
        string lang = finnish ? "/fi" : "";
        return $"https://rog.asus.com{lang}/motherboards/{series}/{slug}-model/";
    }
}
