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

    /// <summary>Program Files -tyylinen ACL: adminit+SYSTEM kirjoittaa, Users lukee; omistaja Administrators.</summary>
    private static void SetAdminOnly(DirectoryInfo info)
    {
        DirectorySecurity security = info.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(security);
    }

    [Fact]
    public void VainAdminKirjoitettavaHakemisto_OnSuojattu()
    {
        SetAdminOnly(Directory.CreateDirectory(_dir));

        Assert.False(ProtectedPaths.HasNonAdminWriteAccess(_dir));
    }

    [Fact]
    public void KayttajanOmistamaHakemisto_AdminOnlyDacl_EiOleSuojattu()
    {
        // Omistajalla on implisiittinen WRITE_DAC — hän voi myöntää itselleen
        // Modify-oikeuden ja vaihtaa korotettuna käynnistyvän sisällön. Siksi
        // ei-hallinnollinen omistaja tekee polusta turvattoman, vaikka DACL
        // näyttäisi vain admin/SYSTEM-kirjoituksen.
        var info = Directory.CreateDirectory(_dir);
        DirectorySecurity security = info.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(WindowsIdentity.GetCurrent().User!); // käyttäjä omistajaksi
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        info.SetAccessControl(security);

        Assert.True(ProtectedPaths.HasNonAdminWriteAccess(_dir));
    }

    [Fact]
    public void InstallTreeSecure_HeikkoValihakemisto_EiSuojattu()
    {
        // root/ (suojattu juuri) / weak (käyttäjän kirjoitettava) / app / exe
        var root = Directory.CreateDirectory(Path.Combine(_dir, "root"));
        SetAdminOnly(root);
        var weak = Directory.CreateDirectory(Path.Combine(root.FullName, "weak"));
        var weakSec = weak.GetAccessControl();
        weakSec.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Modify, AccessControlType.Allow));
        weak.SetAccessControl(weakSec);
        var app = Directory.CreateDirectory(Path.Combine(weak.FullName, "app"));
        SetAdminOnly(app);
        string exe = Path.Combine(app.FullName, "app.exe");
        File.WriteAllText(exe, "x");

        Assert.False(ProtectedPaths.IsInstallTreeSecure(exe, new[] { root.FullName }));
    }

    [Fact]
    public void InstallTreeSecure_HeikkoDll_EiSuojattu()
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir, "root"));
        SetAdminOnly(root);
        var app = Directory.CreateDirectory(Path.Combine(root.FullName, "app"));
        SetAdminOnly(app);
        string exe = Path.Combine(app.FullName, "app.exe");
        File.WriteAllText(exe, "x");
        // Yksi DLL saa käyttäjän Modify-oikeuden → koko puu turvaton.
        string dll = Path.Combine(app.FullName, "dep.dll");
        File.WriteAllText(dll, "x");
        var dllSec = new FileInfo(dll).GetAccessControl();
        dllSec.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        dllSec.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Modify, AccessControlType.Allow));
        new FileInfo(dll).SetAccessControl(dllSec);

        Assert.False(ProtectedPaths.IsInstallTreeSecure(exe, new[] { root.FullName }));
    }

    [Fact]
    public void InstallTreeSecure_KaikkiSuojattu_OnTurvallinen()
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir, "root"));
        SetAdminOnly(root);
        var app = Directory.CreateDirectory(Path.Combine(root.FullName, "app"));
        SetAdminOnly(app);
        string exe = Path.Combine(app.FullName, "app.exe");
        File.WriteAllText(exe, "x");
        string dll = Path.Combine(app.FullName, "dep.dll");
        File.WriteAllText(dll, "x");

        Assert.True(ProtectedPaths.IsInstallTreeSecure(exe, new[] { root.FullName }));
    }

    [Fact]
    public void InstallTreeSecure_ExeEiJuurenAlla_EiTurvallinen()
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir, "root"));
        SetAdminOnly(root);
        string outside = Path.Combine(_dir, "muualla.exe");
        File.WriteAllText(outside, "x");

        Assert.False(ProtectedPaths.IsInstallTreeSecure(outside, new[] { root.FullName }));
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
