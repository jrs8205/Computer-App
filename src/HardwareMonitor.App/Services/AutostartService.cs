using System.Diagnostics;
using System.IO;
using HardwareMonitor.Core.Security;

namespace HardwareMonitor.App.Services;

/// <summary>
/// Automaattikäynnistys Windowsin mukana Task Schedulerilla. Ajastettu tehtävä
/// /RL HIGHEST käynnistää sovelluksen kirjautuessa admin-oikeuksin ilman
/// UAC-kyselyä — Run-rekisteriavain ei siihen pysty, ja ilman adminia
/// CPU-lämpösensorit jäisivät pimeiksi. Korotus sallitaan vain ACL-suojatusta
/// polusta (Program Files, Windows): kirjoitettavasta polusta korotettu
/// tehtävä olisi UAC-ohitus. Luonti/poisto vaatii, että tämä sovellus on
/// itse käynnissä adminina.
/// </summary>
public static class AutostartService
{
    private const string TaskName = "HardwareMonitor";

    public static bool IsEnabled() =>
        RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;

    /// <summary>Palauttaa true jos operaatio onnistui.</summary>
    public static bool SetEnabled(bool on, Action<string>? log = null)
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

        // Ei suojattu polku → EI tehtävää lainkaan: rajoitettu tehtävä ei
        // pystyisi käynnistämään requireAdministrator-manifestilla varustettua
        // exeä kirjautumisessa, ja korotettu tehtävä kirjoitettavasta polusta
        // olisi UAC-ohitus. Mahdollinen vanha tehtävä (asennettuun versioon)
        // jätetään ennalleen.
        if (!IsInProtectedDirectory(exe))
        {
            log?.Invoke(
                "Autostartia ei kytketty tästä sijainnista: ohjelma ei ole " +
                "ACL-suojatussa polussa. Asenna sovellus (Program Files) ja " +
                "kytke automaattikäynnistys asennetusta versiosta.");
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
    public static void RefreshIfEnabled(Action<string>? log = null)
    {
        if (IsEnabled())
        {
            SetEnabled(true, log);
        }
    }

    /// <summary>
    /// Suojattu = Program Files -juurten alla JA hakemiston sekä exen ACL ei
    /// salli tavallisen käyttäjän kirjoitusta. Windows-juurta ei hyväksytä:
    /// sen alla on käyttäjäkirjoitettavia polkuja (esim. C:\Windows\Temp),
    /// eikä sovellusta koskaan asenneta sinne.
    /// </summary>
    private static bool IsInProtectedDirectory(string path)
    {
        bool underProtectedRoot = ProtectedPaths.IsUnderAny(path, new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        });
        if (!underProtectedRoot)
        {
            return false;
        }

        string? directory = Path.GetDirectoryName(path);
        return directory is not null
            && !ProtectedPaths.HasNonAdminWriteAccess(directory)
            && !ProtectedPaths.HasNonAdminWriteAccess(path);
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
