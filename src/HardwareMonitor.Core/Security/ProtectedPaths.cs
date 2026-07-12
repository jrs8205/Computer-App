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

    /// <summary>SID:t, joiden kirjoitusoikeus tarkoittaa ettei polku ole suojattu.</summary>
    private static readonly SecurityIdentifier[] NonAdminSids =
    {
        new(WellKnownSidType.WorldSid, null),             // Everyone
        new(WellKnownSidType.AuthenticatedUserSid, null),
        new(WellKnownSidType.BuiltinUsersSid, null),
        new(WellKnownSidType.InteractiveSid, null),
    };

    private const FileSystemRights WriteRights =
        FileSystemRights.WriteData
        | FileSystemRights.AppendData
        | FileSystemRights.Delete
        | FileSystemRights.DeleteSubdirectoriesAndFiles
        | FileSystemRights.ChangePermissions
        | FileSystemRights.TakeOwnership;

    /// <summary>
    /// True, jos tavallinen (ei-admin) käyttäjä voi kirjoittaa polkuun —
    /// eli polusta EI saa käynnistää korotettua tehtävää. Virhetilanteessa
    /// (polku puuttuu, ACL ei luettavissa) tulkitaan turvattomaksi.
    /// </summary>
    public static bool HasNonAdminWriteAccess(string path)
    {
        try
        {
            FileSystemSecurity security = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();

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

                if (rule.IdentityReference is SecurityIdentifier sid
                    && Array.Exists(NonAdminSids, s => s.Equals(sid)))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return true; // tuntematon tila = ei korotusta
        }
    }
}
