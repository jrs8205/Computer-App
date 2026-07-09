using HardwareMonitor.Core.WindowsEvents;
using Xunit;

namespace HardwareMonitor.Tests.WindowsEvents;

public class WindowsEventClassifierTests
{
    private static WindowsLogEvent Make(
        string provider, int eventId, byte windowsLevel = 2) =>
        new(DateTimeOffset.Now, provider, eventId, windowsLevel, RecordId: 1);

    [Fact]
    public void KernelPower41_OnCritical()
    {
        var result = WindowsEventClassifier.Classify(
            Make("Microsoft-Windows-Kernel-Power", 41, windowsLevel: 1));

        Assert.NotNull(result);
        Assert.Equal("CRITICAL", result.Level);
        Assert.Equal("Järjestelmä", result.Component);
        Assert.Contains("Kernel-Power 41", result.Message);
    }

    [Fact]
    public void KernelPowerMuuId_Ohitetaan()
    {
        // Esim. id 42 = lepotilaan siirtyminen — ei kiinnosta.
        Assert.Null(WindowsEventClassifier.Classify(
            Make("Microsoft-Windows-Kernel-Power", 42, windowsLevel: 4)));
    }

    [Fact]
    public void EventLog6008_OnWarning()
    {
        var result = WindowsEventClassifier.Classify(Make("EventLog", 6008));

        Assert.NotNull(result);
        Assert.Equal("WARNING", result.Level);
        Assert.Equal("Järjestelmä", result.Component);
    }

    [Fact]
    public void BugCheck1001_OnCritical()
    {
        var result = WindowsEventClassifier.Classify(
            Make("Microsoft-Windows-WER-SystemErrorReporting", 1001, windowsLevel: 2));

        Assert.NotNull(result);
        Assert.Equal("CRITICAL", result.Level);
        Assert.Equal("Järjestelmä", result.Component);
        Assert.Contains("BSOD", result.Message);
    }

    [Theory]
    [InlineData(1)] // Critical
    [InlineData(2)] // Error
    public void WheaVirhetaso_OnCritical(byte windowsLevel)
    {
        var result = WindowsEventClassifier.Classify(
            Make("Microsoft-Windows-WHEA-Logger", 18, windowsLevel));

        Assert.NotNull(result);
        Assert.Equal("CRITICAL", result.Level);
        Assert.Equal("Laitteisto", result.Component);
    }

    [Theory]
    [InlineData(3)] // Warning
    [InlineData(4)] // Information — korjattu virhe kirjataan silti
    public void WheaKorjattu_OnWarning(byte windowsLevel)
    {
        var result = WindowsEventClassifier.Classify(
            Make("Microsoft-Windows-WHEA-Logger", 19, windowsLevel));

        Assert.NotNull(result);
        Assert.Equal("WARNING", result.Level);
        Assert.Equal("Laitteisto", result.Component);
    }

    [Theory]
    [InlineData("Display", 4101, 3)]
    [InlineData("nvlddmkm", 13, 2)]
    public void Nayttoajuri_OnWarning(string provider, int eventId, byte windowsLevel)
    {
        var result = WindowsEventClassifier.Classify(Make(provider, eventId, windowsLevel));

        Assert.NotNull(result);
        Assert.Equal("WARNING", result.Level);
        Assert.Equal("GPU-ajuri", result.Component);
    }

    [Fact]
    public void DisplayInformaatio_Ohitetaan()
    {
        Assert.Null(WindowsEventClassifier.Classify(Make("Display", 4101, windowsLevel: 4)));
    }

    [Theory]
    [InlineData("disk", 7, 2)]
    [InlineData("Ntfs", 55, 1)]
    [InlineData("storahci", 129, 2)]
    [InlineData("stornvme", 129, 2)]
    public void LevyVirhetaso_OnCritical(string provider, int eventId, byte windowsLevel)
    {
        var result = WindowsEventClassifier.Classify(Make(provider, eventId, windowsLevel));

        Assert.NotNull(result);
        Assert.Equal("CRITICAL", result.Level);
        Assert.Equal("Levy", result.Component);
    }

    [Fact]
    public void LevyVaroitustaso_OnWarning()
    {
        var result = WindowsEventClassifier.Classify(Make("disk", 153, windowsLevel: 3));

        Assert.NotNull(result);
        Assert.Equal("WARNING", result.Level);
    }

    [Fact]
    public void LevyInformaatio_Ohitetaan()
    {
        // Esim. Ntfs kirjoittaa paljon informatiivisia rivejä — ei kirjata.
        Assert.Null(WindowsEventClassifier.Classify(Make("Ntfs", 98, windowsLevel: 4)));
    }

    [Fact]
    public void TuntematonProvider_Ohitetaan()
    {
        Assert.Null(WindowsEventClassifier.Classify(Make("Service Control Manager", 7036, 2)));
    }

    [Fact]
    public void ViestiSisaltaaProviderinJaIdn()
    {
        var result = WindowsEventClassifier.Classify(Make("disk", 7, windowsLevel: 2));

        Assert.NotNull(result);
        Assert.Contains("disk", result.Message);
        Assert.Contains("7", result.Message);
    }
}
