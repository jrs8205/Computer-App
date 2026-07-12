using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
        if (!IsEnabled())
        {
            return;
        }

        // Turvasiivous: jos olemassa oleva tehtävä osoittaa suojaamattomaan
        // polkuun TAI kohdetta ei saada luettua (tuntematon = turvaton), se on
        // mahdollinen korotusreitti — poistetaan se, ettei sitä jätetä
        // aktiiviseksi. Vanhemman version luoma korotettu tehtävä käyttäjän
        // kirjoitettavassa polussa on juuri tämä uhka.
        string? existingTarget = ReadExistingTaskTarget();
        if (existingTarget is null || !IsInProtectedDirectory(existingTarget))
        {
            RunSchtasks($"/Delete /F /TN \"{TaskName}\"");
            log?.Invoke(
                "Poistettiin autostart-tehtävä, jonka kohde ei ollut varmasti " +
                "suojattu (tuntematon tai suojaamaton polku, mahdollinen " +
                "korotusreitti).");
        }

        // Luo uudelleen nykyisestä exestä (SetEnabled kieltäytyy, jos nykyinen
        // polku ei ole suojattu — silloin dangerous-tehtävä on jo poistettu).
        SetEnabled(true, log);
    }

    /// <summary>
    /// Olemassa olevan tehtävän käynnistettävä exe-polku (ilman argumentteja),
    /// tai null jos sitä ei saada luettua. Luetaan schtasks /XML -tulosteesta.
    /// </summary>
    private static string? ReadExistingTaskTarget()
    {
        string? xml = RunSchtasksCapture($"/Query /TN \"{TaskName}\" /XML");
        if (xml is null)
        {
            return null;
        }

        Match m = Regex.Match(xml, "<Command>(.*?)</Command>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            return null;
        }

        // <Command> voi olla lainausmerkeissä; siistitään ne ja välit pois.
        return m.Groups[1].Value.Trim().Trim('"');
    }

    private static readonly string[] ProtectedRoots =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
    };

    /// <summary>
    /// Suojattu = Program Files -juurten alla JA koko asennuspuu (exe, kaikki
    /// ladattavat DLL:t, hakemisto ja esivanhemmat) on hallinnollisesti
    /// omistettu eikä salli ei-hallinnollista kirjoitusta. Windows-juurta ei
    /// hyväksytä (sen alla on käyttäjäkirjoitettavia polkuja), eikä pelkkä
    /// exe riitä (muokattu DLL latautuisi korotettuna).
    /// </summary>
    private static bool IsInProtectedDirectory(string path) =>
        ProtectedPaths.IsInstallTreeSecure(path, ProtectedRoots);

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

    /// <summary>Ajaa schtasksin ja palauttaa stdoutin, tai null jos epäonnistuu.</summary>
    private static string? RunSchtasksCapture(string arguments)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            });

            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
