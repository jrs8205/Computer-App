using System.Security.AccessControl;
using System.Security.Principal;
using HardwareMonitor.Core.Security;
using Xunit;

namespace HardwareMonitor.Tests.Security;

public class ProtectedPathsTests : IDisposable
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
    public void KayttajilleKirjoitettavaHakemisto_EiOleSuojattu()
    {
        // Esim. C:\Windows\Temp on Windows-juuren alla mutta käyttäjien
        // kirjoitettavissa — pelkkä polkuetuliite ei riitä suojaukseen.
        var info = Directory.CreateDirectory(_dir);
        DirectorySecurity security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);

        Assert.True(ProtectedPaths.HasNonAdminWriteAccess(_dir));
    }

    [Fact]
    public void NykyiselleKayttajalleMyonnettyKirjoitus_EiOleSuojattu()
    {
        // Jos ACL myöntää kirjoituksen suoraan nykyiselle käyttäjälle (tai
        // mukautetulle ei-hallinnolliselle ryhmälle), polku EI ole suojattu —
        // vain admin/SYSTEM/TrustedInstaller-kirjoitus sallitaan.
        var info = Directory.CreateDirectory(_dir);
        DirectorySecurity security = info.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.Modify, AccessControlType.Allow));
        info.SetAccessControl(security);

        Assert.True(ProtectedPaths.HasNonAdminWriteAccess(_dir));
    }

    [Fact]
    public void VainAdminKirjoitettavaHakemisto_OnSuojattu()
    {
        var info = Directory.CreateDirectory(_dir);
        DirectorySecurity security = info.GetAccessControl();
        // Kuten Program Files: adminit ja SYSTEM kirjoittavat, Users vain lukee.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        info.SetAccessControl(security);

        Assert.False(ProtectedPaths.HasNonAdminWriteAccess(_dir));
    }

    [Fact]
    public void PuuttuvaPolku_TulkitaanTurvattomaksi()
    {
        Assert.True(ProtectedPaths.HasNonAdminWriteAccess(
            Path.Combine(_dir, "ei-olemassa")));
    }

    private static readonly string[] Roots =
    {
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"C:\Windows",
    };

    [Fact]
    public void PolkuJuurenAlla_OnSuojattu()
    {
        Assert.True(ProtectedPaths.IsUnderAny(
            @"C:\Program Files\Hardware Monitor\HardwareMonitor.exe", Roots));
    }

    [Fact]
    public void SyvaAlipolku_OnSuojattu()
    {
        Assert.True(ProtectedPaths.IsUnderAny(
            @"C:\Windows\System32\drivers\etc\hosts", Roots));
    }

    [Fact]
    public void KirjainkokoEiVaikuta()
    {
        Assert.True(ProtectedPaths.IsUnderAny(
            @"c:\program files\App\app.exe", Roots));
    }

    [Fact]
    public void KayttajanKansio_EiOleSuojattu()
    {
        Assert.False(ProtectedPaths.IsUnderAny(
            @"C:\Users\jrs82\Downloads\Computer-App\bin\HardwareMonitor.exe", Roots));
    }

    [Fact]
    public void SamallaEtuliitteellaAlkavaSisarkansio_EiOleSuojattu()
    {
        // "C:\Program FilesEvil" alkaa samoin merkein muttei ole juuren alla.
        Assert.False(ProtectedPaths.IsUnderAny(
            @"C:\Program FilesEvil\app.exe", Roots));
    }

    [Fact]
    public void TyhjatJuuretOhitetaan()
    {
        // Esim. 32-bit ympäristössä ProgramFilesX86 voi puuttua ("").
        Assert.False(ProtectedPaths.IsUnderAny(
            @"C:\Temp\app.exe", new[] { "", @"C:\Windows" }));
    }
}
