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
    public double Opacity { get; set; } = 0.85;
    public double FontSize { get; set; } = 14;
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowDisks { get; set; } = true;
    public bool ShowFans { get; set; }
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

    /// <summary>Käyttäjän omat nimet tuulettimille: sensorin Identifier -> nimi.</summary>
    public Dictionary<string, string> FanLabels { get; set; } = new();

    /// <summary>Pienennä- ja sulje-nappi vievät ilmaisinalueelle; mittaus jatkuu taustalla.</summary>
    public bool MinimizeToTray { get; set; } = true;
}
