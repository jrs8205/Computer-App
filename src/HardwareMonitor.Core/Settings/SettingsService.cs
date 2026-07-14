using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HardwareMonitor.Core.IO;

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
    /// Täydentää nulliksi deserialisoituneet sisäoliot oletuksilla JA clamppaa
    /// numeeriset arvot samoihin rajoihin kuin asetussivu. Muuten esim.
    /// {"Logging":{"KeepHistoryDays":-1}} tekisi purge-cutoffista tulevaisuuden
    /// ja koko historia poistuisi; NaN-raja mykistäisi hälytyksen.
    /// </summary>
    private static AppSettings Normalize(AppSettings s)
    {
        s.Overlay ??= new OverlaySettings();
        s.Logging ??= new LoggingSettings();
        s.Thresholds ??= new ThresholdSettings();
        s.FanLabels ??= new Dictionary<string, string>();
        s.Language ??= "";
        s.InsightsNotes ??= "";
        s.Updates ??= new UpdateSettings();
        s.Updates.LastNotifiedVersion ??= "";

        var d = new LoggingSettings();
        s.Logging.SensorIntervalSeconds = ClampInt(s.Logging.SensorIntervalSeconds, 1, 60, d.SensorIntervalSeconds);
        s.Logging.KeepHistoryDays = ClampInt(s.Logging.KeepHistoryDays, 1, 365, d.KeepHistoryDays);

        OverlaySettings od = new();
        s.Overlay.Opacity = ClampDouble(s.Overlay.Opacity, 0.1, 1.0, od.Opacity);
        s.Overlay.FontSize = ClampDouble(s.Overlay.FontSize, 8, 32, od.FontSize);
        s.Overlay.MarginPx = ClampInt(s.Overlay.MarginPx, 0, 500, od.MarginPx);
        if (!Enum.IsDefined(s.Overlay.Corner))
        {
            s.Overlay.Corner = od.Corner;
        }

        ThresholdSettings td = new();
        ThresholdSettings t = s.Thresholds;
        t.CpuWarningTemp = ClampFloat(t.CpuWarningTemp, 20, 120, td.CpuWarningTemp);
        t.CpuCriticalTemp = ClampFloat(t.CpuCriticalTemp, 20, 120, td.CpuCriticalTemp);
        t.GpuWarningTemp = ClampFloat(t.GpuWarningTemp, 20, 120, td.GpuWarningTemp);
        t.GpuCriticalTemp = ClampFloat(t.GpuCriticalTemp, 20, 120, td.GpuCriticalTemp);
        t.GpuHotspotWarningTemp = ClampFloat(t.GpuHotspotWarningTemp, 20, 120, td.GpuHotspotWarningTemp);
        t.GpuHotspotCriticalTemp = ClampFloat(t.GpuHotspotCriticalTemp, 20, 120, td.GpuHotspotCriticalTemp);
        t.NvmeWarningTemp = ClampFloat(t.NvmeWarningTemp, 20, 120, td.NvmeWarningTemp);
        t.NvmeCriticalTemp = ClampFloat(t.NvmeCriticalTemp, 20, 120, td.NvmeCriticalTemp);
        t.RamWarningPercent = ClampFloat(t.RamWarningPercent, 10, 100, td.RamWarningPercent);
        t.RamCriticalPercent = ClampFloat(t.RamCriticalPercent, 10, 100, td.RamCriticalPercent);
        t.FanStopCpuTemp = ClampFloat(t.FanStopCpuTemp, 20, 120, td.FanStopCpuTemp);
        t.WarningSustainSeconds = ClampInt(t.WarningSustainSeconds, 1, 600, td.WarningSustainSeconds);
        t.CriticalSustainSeconds = ClampInt(t.CriticalSustainSeconds, 1, 600, td.CriticalSustainSeconds);
        t.EventCooldownMinutes = ClampInt(t.EventCooldownMinutes, 1, 60, td.EventCooldownMinutes);

        return s;
    }

    private static int ClampInt(int value, int min, int max, int fallback) =>
        value < min || value > max ? Math.Clamp(fallback, min, max) : value;

    private static double ClampDouble(double value, double min, double max, double fallback) =>
        !double.IsFinite(value) || value < min || value > max ? fallback : value;

    private static float ClampFloat(float value, float min, float max, float fallback) =>
        !float.IsFinite(value) || value < min || value > max ? fallback : value;

    public void Save(AppSettings settings)
    {
        // Atominen korvaus: prosessin tai levyn virhe ei saa jättää katkaistua
        // JSONia, jolloin seuraava käynnistys palauttaisi kaikki oletukset.
        AtomicFile.WriteAllText(
            _path, JsonSerializer.Serialize(settings, Options), Encoding.UTF8);
    }
}
