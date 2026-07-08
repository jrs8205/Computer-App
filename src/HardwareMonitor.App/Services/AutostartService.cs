using System.Diagnostics;

namespace HardwareMonitor.App.Services;

/// <summary>
/// Automaattikäynnistys Windowsin mukana Task Schedulerilla. Ajastettu tehtävä
/// /RL HIGHEST käynnistää sovelluksen kirjautuessa admin-oikeuksin ilman
/// UAC-kyselyä — Run-rekisteriavain ei siihen pysty, ja ilman adminia
/// CPU-lämpösensorit jäisivät pimeiksi. Luonti/poisto vaatii, että tämä
/// sovellus on itse käynnissä adminina.
/// </summary>
public static class AutostartService
{
    private const string TaskName = "HardwareMonitor";

    public static bool IsEnabled() =>
        RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;

    /// <summary>Palauttaa true jos operaatio onnistui.</summary>
    public static bool SetEnabled(bool on)
    {
        if (!on)
        {
            return RunSchtasks($"/Delete /F /TN \"{TaskName}\"") == 0;
        }

        string? exe = Environment.ProcessPath;
        if (exe is null)
        {
            return false;
        }

        return RunSchtasks($"/Create /F /RL HIGHEST /SC ONLOGON /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\"") == 0;
    }

    private static int RunSchtasks(string arguments)
    {
        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        if (process is null)
        {
            return -1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }
}
