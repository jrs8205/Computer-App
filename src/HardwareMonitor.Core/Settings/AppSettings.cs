namespace HardwareMonitor.Core.Settings;

/// <summary>Näytön kulma, johon overlay asemoidaan.</summary>
public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Overlayn käyttäjäasetukset (specin Vaihe 2.5).</summary>
public sealed class OverlaySettings
{
    public bool Enabled { get; set; }
    public OverlayCorner Corner { get; set; } = OverlayCorner.TopRight;
    public int MarginPx { get; set; } = 16;

    /// <summary>Käyttäjän raahaama oma sijainti; false = asemointi kulman mukaan.</summary>
    public bool UseCustomPosition { get; set; }
    public double CustomLeft { get; set; }
    public double CustomTop { get; set; }
    public double Opacity { get; set; } = 0.85;
    public double FontSize { get; set; } = 14;
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowDisks { get; set; } = true;
    public bool ShowFans { get; set; }
}

/// <summary>Varoitus- ja kriittisrajat (määrittelyn luvut 16 ja 29).</summary>
public sealed class ThresholdSettings
{
    public float CpuWarningTemp { get; set; } = 85;
    public float CpuCriticalTemp { get; set; } = 95;
    public float GpuWarningTemp { get; set; } = 85;
    public float GpuCriticalTemp { get; set; } = 95;
    public float GpuHotspotWarningTemp { get; set; } = 95;
    public float GpuHotspotCriticalTemp { get; set; } = 105;
    public float NvmeWarningTemp { get; set; } = 70;
    public float NvmeCriticalTemp { get; set; } = 82;
    public float RamWarningPercent { get; set; } = 85;
    public float RamCriticalPercent { get; set; } = 95;

    /// <summary>Pysähtynyt tuuletin on kriittinen vasta kun CPU on vähintään näin kuuma.</summary>
    public float FanStopCpuTemp { get; set; } = 80;

    /// <summary>Yhtäjaksoinen ylitys sekunneissa ennen WARNING-tapahtumaa (piikki vs. kesto).</summary>
    public int WarningSustainSeconds { get; set; } = 30;

    public int CriticalSustainSeconds { get; set; } = 10;

    /// <summary>Sama sääntö+taso ei kirjaudu uudelleen tätä useammin (luku 26).</summary>
    public int EventCooldownMinutes { get; set; } = 5;
}

/// <summary>Lokituksen asetukset (määrittelyn luku 29).</summary>
public sealed class LoggingSettings
{
    /// <summary>Montako 1 s -lukemaa kootaan yhteen kantariviin.</summary>
    public int SensorIntervalSeconds { get; set; } = 5;

    public int KeepHistoryDays { get; set; } = 30;
}

/// <summary>
/// Sovelluksen kaikki asetukset. Laajenee Vaihe 4:ssä raja-arvoilla (specin luku 29).
/// </summary>
public sealed class AppSettings
{
    public OverlaySettings Overlay { get; set; } = new();

    public LoggingSettings Logging { get; set; } = new();

    public ThresholdSettings Thresholds { get; set; } = new();

    /// <summary>Käyttäjän omat nimet tuulettimille: sensorin Identifier -> nimi.</summary>
    public Dictionary<string, string> FanLabels { get; set; } = new();

    /// <summary>Pienennä- ja sulje-nappi vievät ilmaisinalueelle; mittaus jatkuu taustalla.</summary>
    public bool MinimizeToTray { get; set; } = true;
}
