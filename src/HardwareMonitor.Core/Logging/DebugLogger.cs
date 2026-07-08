using System.Diagnostics;
using System.Text;

namespace HardwareMonitor.Core.Logging;

/// <summary>
/// Yksinkertainen debug-loki, joka kirjoittaa sekä Visual Studion Debug-ikkunaan
/// että tiedostoon. Tämä on suunnitelman luvun 33 vaatimus: "tulosta sensorit myös
/// debug-lokiin". Varsinainen SQLite-sensoriloki ja tapahtumaloki (luvut 13–15)
/// rakennetaan myöhemmässä vaiheessa erillisiin palveluihin.
/// </summary>
public sealed class DebugLogger
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public DebugLogger(string? logDirectory = null)
    {
        logDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor",
            "logs");

        Directory.CreateDirectory(logDirectory);
        _logPath = Path.Combine(logDirectory, "debug.log");
    }

    /// <summary>Lokitiedoston täysi polku, näytetään käyttäjälle UI:ssa.</summary>
    public string LogPath => _logPath;

    public void Log(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        Debug.WriteLine(line);

        lock (_lock)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
