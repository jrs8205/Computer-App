using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace HardwareMonitor.Core.Logging;

/// <summary>
/// Debug-loki, joka kirjoittaa tiedostoon TAUSTASÄIKEELLÄ, ettei hidas levy
/// tai virustarkistus pysäytä mittausta (UI-tikkiä). Julkiset metodit vain
/// jonottavat rivit eivätkä KOSKAAN heitä sovellukseen päin. Loki kiertää
/// kokorajan ylittyessä (debug.log → debug.old.log). Suunnitelman luku 33.
/// </summary>
public sealed class DebugLogger : IDisposable
{
    private const long DefaultMaxLogBytes = 20 * 1024 * 1024;
    private const int MaxQueue = 10_000;

    private readonly string _logPath;
    private readonly string _oldLogPath;
    private readonly long _maxLogBytes;
    private readonly BlockingCollection<string> _queue =
        new(new ConcurrentQueue<string>(), MaxQueue);
    private readonly Thread _worker;

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

        _worker = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "DebugLogWriter",
        };
        _worker.Start();
    }

    /// <summary>Lokitiedoston täysi polku, näytetään käyttäjälle UI:ssa.</summary>
    public string LogPath => _logPath;

    public void Log(string message) => Enqueue(Stamp(message));

    /// <summary>
    /// Jonottaa monta riviä yhtenä lohkona — sensoripuun snapshot on satoja
    /// rivejä eikä sitä kannata pilkkoa erillisiin tiedosto-operaatioihin.
    /// </summary>
    public void LogBatch(IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        string stamp = Stamp("");
        var sb = new StringBuilder();
        foreach (string message in messages)
        {
            sb.Append(stamp).AppendLine(message);
        }

        // Poistetaan viimeinen rivinvaihto — writer lisää sen per lohko.
        Enqueue(sb.ToString().TrimEnd('\r', '\n'));
    }

    /// <summary>Odottaa kunnes jono on tyhjä (testejä varten).</summary>
    public void Flush()
    {
        while (!_queue.IsCompleted && _queue.Count > 0)
        {
            Thread.Sleep(10);
        }

        Thread.Sleep(20); // anna käynnissä olevan kirjoituksen valmistua
    }

    private static string Stamp(string message) =>
        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";

    private void Enqueue(string block)
    {
        // Ei KOSKAAN heitä kutsujalle: jono täynnä tai suljettu → pudotetaan.
        try
        {
            Debug.WriteLine(block);
            if (!_queue.IsAddingCompleted)
            {
                _queue.TryAdd(block);
            }
        }
        catch (Exception)
        {
        }
    }

    private void ProcessQueue()
    {
        foreach (string block in _queue.GetConsumingEnumerable())
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(_logPath, block + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                // Kirjoitusvirhe (levy täynnä, lukittu) ei saa kaataa säiettä
                // eikä sovellusta — pudotetaan rivi ja jatketaan.
            }
        }
    }

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
        }
    }

    public void Dispose()
    {
        try
        {
            _queue.CompleteAdding();
            _worker.Join(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
        }
    }
}
