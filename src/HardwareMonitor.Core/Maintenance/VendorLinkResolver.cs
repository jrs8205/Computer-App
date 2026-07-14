namespace HardwareMonitor.Core.Maintenance;

/// <summary>
/// Laitenimi → valmistajan tukisivun osoite. Tarkoituksella EI syvälinkkejä
/// mallisivuille (osoitteet muuttuvat ja hajoavat hiljaa) — linkki vie
/// valmistajan tukisivulle ja mallinimen voi kopioida hakua varten.
/// Aluevalinta tulee Windowsin kieliasetuksesta, ei IP-paikannuksesta.
/// </summary>
public static class VendorLinkResolver
{
    public static string? Resolve(string? deviceName, string language)
    {
        string name = deviceName?.Trim() ?? "";
        if (name.Length == 0)
        {
            return null;
        }

        bool finnish = string.Equals(language, "fi", StringComparison.OrdinalIgnoreCase);

        if (name.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
        {
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
}
