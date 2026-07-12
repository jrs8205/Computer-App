using HardwareMonitor.Core.Logging;
using Xunit;

namespace HardwareMonitor.Tests.Logging;

public sealed class DebugLoggerTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "HardwareMonitorTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Log_KirjoittaaAikaleimallisenRivin()
    {
        using var logger = new DebugLogger(_dir);

        logger.Log("testirivi");
        logger.Flush();

        string[] lines = File.ReadAllLines(logger.LogPath);
        string line = Assert.Single(lines);
        Assert.EndsWith("testirivi", line);
        // Aikaerotin riippuu ajokoneen kulttuurista (fi-FI käyttää pistettä).
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}[:.]\d{2}[:.]\d{2} ", line);
    }

    [Fact]
    public void LogBatch_KirjoittaaKaikkiRivitAikaleimoineen()
    {
        using var logger = new DebugLogger(_dir);

        logger.LogBatch(new[] { "eka", "toka", "kolmas" });
        logger.Flush();

        string[] lines = File.ReadAllLines(logger.LogPath);
        Assert.Equal(3, lines.Length);
        Assert.EndsWith("eka", lines[0]);
        Assert.EndsWith("kolmas", lines[2]);
        Assert.All(lines, l => Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}[:.]\d{2}[:.]\d{2} ", l));
    }

    [Fact]
    public void LogBatch_TyhjaLista_EiLuoTiedostoa()
    {
        using var logger = new DebugLogger(_dir);

        logger.LogBatch(Array.Empty<string>());
        logger.Flush();

        Assert.False(File.Exists(logger.LogPath));
    }

    [Fact]
    public void Log_RajanYlittyessa_KiertaaLokinVanhaksi()
    {
        using var logger = new DebugLogger(_dir, maxLogBytes: 200);
        string oldPath = Path.Combine(_dir, "debug.old.log");

        for (int i = 0; i < 20; i++)
        {
            logger.Log($"täyterivi {i} — kasvatetaan tiedostoa yli rajan");
        }

        logger.Flush();

        Assert.True(File.Exists(oldPath), "vanhaa lokia ei syntynyt");
        Assert.True(new FileInfo(logger.LogPath).Length < 200 + 100,
            "aktiivinen loki ei tyhjentynyt kierrossa");
    }

    [Fact]
    public void Log_ToinenKierto_KorvaaAiemmanVanhanLokin()
    {
        using var logger = new DebugLogger(_dir, maxLogBytes: 200);

        for (int i = 0; i < 60; i++)
        {
            logger.Log($"täyterivi {i} — kasvatetaan tiedostoa yli rajan monta kertaa");
        }

        logger.Flush();

        // Kierto ei saa kaatua siihen, että debug.old.log on jo olemassa.
        Assert.True(File.Exists(Path.Combine(_dir, "debug.old.log")));
        Assert.True(File.Exists(logger.LogPath));
    }
}
