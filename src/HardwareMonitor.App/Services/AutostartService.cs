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

        // --tray: Windowsin mukana käynnistyttäessä pääikkuna jää trayhin
        // ja vain overlay avautuu.
        return RunSchtasks(
            $"/Create /F /RL HIGHEST /SC ONLOGON /TN \"{TaskName}\" " +
            $"/TR \"\\\"{exe}\\\" {App.TrayArgument}\"") == 0;
    }

    /// <summary>
    /// Kirjoittaa olemassa olevan tehtävän uudelleen, jotta se osoittaa aina
    /// nykyiseen exe-polkuun nykyisillä argumenteilla (esim. --tray lisättiin
    /// vanhan tehtävän luonnin jälkeen). Ei tee mitään jos autostart ei ole päällä.
    /// </summary>
    public static void RefreshIfEnabled()
    {
        if (IsEnabled())
        {
            SetEnabled(true);
        }
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
