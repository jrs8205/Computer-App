using System.Text.Json;
using System.Text.Json.Serialization;

namespace HardwareMonitor.Core.Settings;

/// <summary>
/// Lataa ja tallentaa asetukset JSON-tiedostoon
/// (%LOCALAPPDATA%\HardwareMonitor\settings.json). Vioittunut tai puuttuva
/// tiedosto ei ole virhe: silloin palautetaan oletusasetukset.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsService(string? directory = null)
    {
        directory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor");

        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            AppSettings settings =
                JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options)
                ?? new AppSettings();
            return Normalize(settings);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Täydentää nulliksi deserialisoituneet sisäoliot oletuksilla. Esim.
    /// {"Logging":null} tuottaa AppSettingsin, jossa Logging on null; sitä
    /// heti dereferoiva UI kaatuisi ennen ikkunan näyttämistä.
    /// </summary>
    private static AppSettings Normalize(AppSettings s)
    {
        s.Overlay ??= new OverlaySettings();
        s.Logging ??= new LoggingSettings();
        s.Thresholds ??= new ThresholdSettings();
        s.FanLabels ??= new Dictionary<string, string>();
        s.Language ??= "";
        s.InsightsNotes ??= "";
        return s;
    }

    public void Save(AppSettings settings)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
    }
}
