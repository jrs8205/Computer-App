using System.Text.Json;

namespace HardwareMonitor.Core.Updates;

/// <summary>
/// Päivitystarkistuksen puhdas logiikka: releases/latest-vastauksen jäsennys
/// ja versiovertailu. Verkko-I/O on App-kerroksen UpdateServicessä.
/// </summary>
public static class UpdateChecker
{
    public static UpdateInfo? ParseLatestRelease(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("tag_name", out JsonElement tag) ||
                !root.TryGetProperty("html_url", out JsonElement url))
            {
                return null;
            }

            string version = (tag.GetString() ?? "").TrimStart('v', 'V');
            string releaseUrl = url.GetString() ?? "";
            if (version.Length == 0 || releaseUrl.Length == 0)
            {
                return null;
            }

            string notes = root.TryGetProperty("body", out JsonElement body)
                ? body.GetString() ?? ""
                : "";

            string? assetUrl = null;
            if (root.TryGetProperty("assets", out JsonElement assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out JsonElement n)
                        ? n.GetString() ?? ""
                        : "";
                    if (name.StartsWith("HardwareMonitor-Setup-", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        asset.TryGetProperty("browser_download_url", out JsonElement dl))
                    {
                        assetUrl = dl.GetString();
                        break;
                    }
                }
            }

            return new UpdateInfo(version, releaseUrl, assetUrl, notes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsNewer(string currentVersion, string latestVersion) =>
        Version.TryParse(currentVersion, out Version? current) &&
        Version.TryParse(latestVersion, out Version? latest) &&
        latest > current;

    /// <summary>
    /// Ilmoitetaan vain uudemmasta versiosta ja vain kerran per versio —
    /// "Myöhemmin"-valinta ei johda jankutukseen joka käynnistyksellä.
    /// </summary>
    public static bool ShouldNotify(
        string latestVersion, string currentVersion, string lastNotifiedVersion) =>
        IsNewer(currentVersion, latestVersion) &&
        !string.Equals(latestVersion, lastNotifiedVersion, StringComparison.OrdinalIgnoreCase);
}
