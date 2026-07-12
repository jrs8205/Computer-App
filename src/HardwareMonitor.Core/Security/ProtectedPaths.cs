using System.Security.AccessControl;
using System.Security.Principal;

namespace HardwareMonitor.Core.Security;

/// <summary>
/// Polkutarkistus autostart-korotusta varten: ajastettu tehtävä saa käynnistää
/// ohjelman korkeimmilla oikeuksilla vain ACL-suojatusta sijainnista.
/// Käyttäjän kirjoitettavissa olevasta polusta korotettu tehtävä olisi
/// UAC-ohitus — tavallinen prosessi voisi vaihtaa exen ja saada koodinsa
/// korotettuna seuraavassa kirjautumisessa. Pelkkä polkuetuliite ei riitä
/// (esim. C:\Windows\Temp on käyttäjien kirjoitettavissa), joten kohteen
/// todellinen ACL tarkistetaan erikseen.
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

    // TrustedInstaller (NT SERVICE\TrustedInstaller) — Program Filesin todellinen
    // omistaja; ei WellKnownSidType-vakiota, joten SID kirjoitettuna.
    private const string TrustedInstallerSid =
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    /// <summary>Ainoat SID:t, joiden kirjoitusoikeus EI tee polusta turvatonta.</summary>
    private static readonly SecurityIdentifier[] AdminSids =
    {
        new(WellKnownSidType.BuiltinAdministratorsSid, null),
        new(WellKnownSidType.LocalSystemSid, null),
        new(TrustedInstallerSid),
    };

    private const FileSystemRights WriteRights =
        FileSystemRights.WriteData
        | FileSystemRights.AppendData
        | FileSystemRights.Delete
        | FileSystemRights.DeleteSubdirectoriesAndFiles
        | FileSystemRights.ChangePermissions
        | FileSystemRights.TakeOwnership;

    private static bool IsAdminSid(SecurityIdentifier? sid) =>
        sid is not null && Array.Exists(AdminSids, s => s.Equals(sid));

    /// <summary>
    /// True, jos joku muu kuin hallinnollinen taho (admin/SYSTEM/
    /// TrustedInstaller) voi kirjoittaa polkuun — eli polusta EI saa
    /// käynnistää korotettua tehtävää. Turvattomuus todetaan jos:
    /// (a) omistaja ei ole hallinnollinen (omistajalla on implisiittinen
    /// WRITE_DAC → voi myöntää itselleen kirjoituksen), TAI (b) DACL:ssa on
    /// kirjoitus-ACE ei-hallinnolliselle SID:lle. Virhetilanteessa (polku
    /// puuttuu, ACL ei luettavissa) turvaton.
    /// </summary>
    public static bool HasNonAdminWriteAccess(string path)
    {
        try
        {
            FileSystemSecurity security = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();

            // Omistaja: ei-hallinnollinen omistaja voi WRITE_DAC:illa avata
            // itselleen kirjoituksen, joten se tekee polusta turvattoman.
            if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner
                || !IsAdminSid(owner))
            {
                return true;
            }

            foreach (FileSystemAccessRule rule in
                     security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType != AccessControlType.Allow)
                {
                    continue;
                }

                // Vain tähän kohteeseen kohdistuvat ACE:t; pelkästään lapsille
                // periytyvä (esim. CREATOR OWNER Program Filesissä) ei anna
                // oikeuksia itse kohteeseen.
                if ((rule.PropagationFlags & PropagationFlags.InheritOnly) != 0)
                {
                    continue;
                }

                if ((rule.FileSystemRights & WriteRights) == 0)
                {
                    continue;
                }

                if (rule.IdentityReference is not SecurityIdentifier sid || !IsAdminSid(sid))
                {
                    return true; // ei-hallinnollinen kirjoitus → turvaton
                }
            }

            return false;
        }
        catch (Exception)
        {
            return true; // tuntematon tila = ei korotusta
        }
    }

    /// <summary>
    /// True vain jos koko asennuspuu on turvallinen korotettuun
    /// automaattikäynnistykseen: exe on jonkin suojatun juuren alla, eikä
    /// exen, sen ladattavien tiedostojen (*.dll/*.exe), asennushakemiston
    /// eikä yhdenkään esivanhemman (juureen asti) omistaja/ACL salli
    /// ei-hallinnollista kirjoitusta. Reparse-pisteet (junction/symlink)
    /// tulkitaan turvattomiksi. Suojattu juuri itse on OS:n suojaama, joten
    /// tarkistus pysähtyy siihen.
    /// </summary>
    public static bool IsInstallTreeSecure(string exePath, IEnumerable<string> protectedRoots)
    {
        try
        {
            string? matchedRoot = null;
            foreach (string root in protectedRoots)
            {
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                string prefix = root.TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (exePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchedRoot = root.TrimEnd(Path.DirectorySeparatorChar);
                    break;
                }
            }

            if (matchedRoot is null || !File.Exists(exePath))
            {
                return false;
            }

            string? installDir = Path.GetDirectoryName(exePath);
            if (installDir is null)
            {
                return false;
            }

            // Exe + kaikki ladattavat tiedostot asennushakemistossa.
            if (HasNonAdminWriteAccess(exePath) || IsReparsePoint(exePath))
            {
                return false;
            }

            foreach (string file in Directory.EnumerateFiles(
                         installDir, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (HasNonAdminWriteAccess(file) || IsReparsePoint(file))
                    {
                        return false;
                    }
                }
            }

            // Asennushakemisto ja esivanhemmat juureen (poislukien juuri, joka
            // on OS:n suojaama).
            for (string? dir = installDir;
                 dir is not null
                 && !dir.Equals(matchedRoot, StringComparison.OrdinalIgnoreCase);
                 dir = Path.GetDirectoryName(dir))
            {
                if (HasNonAdminWriteAccess(dir) || IsReparsePoint(dir))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception)
        {
            return true; // ei luettavissa → turvaton
        }
    }
}
