using HardwareMonitor.Core.Security;
using Xunit;

namespace HardwareMonitor.Tests.Security;

public class ProtectedPathsTests
{
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
