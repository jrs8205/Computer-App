using System.Diagnostics;
using System.Text;

namespace HardwareMonitor.Core.Logging;

/// <summary>
/// Yksinkertainen debug-loki, joka kirjoittaa sekä Visual Studion Debug-ikkunaan
/// että tiedostoon. Tämä on suunnitelman luvun 33 vaatimus: "tulosta sensorit myös
/// debug-lokiin". Loki kiertää kokorajan ylittyessä (debug.log → debug.old.log),
/// ettei tiedosto kasva rajatta pitkissä tray-ajoissa.
/// </summary>
public sealed class DebugLogger
{
    private const long DefaultMaxLogBytes = 20 * 1024 * 1024;

    private readonly string _logPath;
    private readonly string _oldLogPath;
    private readonly long _maxLogBytes;
    private readonly object _lock = new();

    public DebugLogger(string? logDirectory = null, long maxLogBytes = DefaultMaxLogBytes)
    {
        logDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor",
            "logs");

        Directory.CreateDirectory(logDirectory);
        _logPath = Path.Combine(logDirectory, "debug.log");
        _oldLogPath = Path.Combine(logDirectory, "debug.old.log");
        _maxLogBytes = maxLogBytes;
    }

    /// <summary>Lokitiedoston täysi polku, näytetään käyttäjälle UI:ssa.</summary>
    public string LogPath => _logPath;

    public void Log(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        Debug.WriteLine(line);

        lock (_lock)
        {
            RotateIfNeeded();
            File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Kirjoittaa monta riviä yhdellä tiedosto-operaatiolla — sensoripuun
    /// snapshot on satoja rivejä, eikä tiedostoa kannata avata joka riville.
    /// </summary>
    public void LogBatch(IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        string stamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ";
        var sb = new StringBuilder();
        foreach (string message in messages)
        {
            sb.Append(stamp).AppendLine(message);
        }

        Debug.WriteLine(sb.ToString());

        lock (_lock)
        {
            RotateIfNeeded();
            File.AppendAllText(_logPath, sb.ToString(), Encoding.UTF8);
        }
    }

    /// <summary>Kutsutaan lukon sisältä: kierrättää täyden lokin vanhaksi.</summary>
    private void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(_logPath);
            if (info.Exists && info.Length >= _maxLogBytes)
            {
                File.Move(_logPath, _oldLogPath, overwrite: true);
            }
        }
        catch (IOException)
        {
            // Kierron epäonnistuminen (esim. toinen prosessi lukee vanhaa lokia)
            // ei saa estää lokitusta — jatketaan nykyiseen tiedostoon.
        }
    }
}
