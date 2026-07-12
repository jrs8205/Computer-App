namespace HardwareMonitor.Core.Security;

/// <summary>
/// Polkutarkistus autostart-korotusta varten: ajastettu tehtävä saa käynnistää
/// ohjelman korkeimmilla oikeuksilla vain ACL-suojatusta sijainnista (Program
/// Files, Windows). Käyttäjän kirjoitettavissa olevasta polusta korotettu
/// tehtävä olisi UAC-ohitus — tavallinen prosessi voisi vaihtaa exen ja saada
/// koodinsa korotettuna seuraavassa kirjautumisessa.
/// </summary>
public static class ProtectedPaths
{
    /// <summary>Onko polku jonkin annetun juuren alla (kirjainkoosta riippumatta).</summary>
    public static bool IsUnderAny(string path, IEnumerable<string> roots)
    {
        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }

            string prefix = root.TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
