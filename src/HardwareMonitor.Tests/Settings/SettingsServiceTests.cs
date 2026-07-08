using HardwareMonitor.Core.Settings;
using Xunit;

namespace HardwareMonitor.Tests.Settings;

public sealed class SettingsServiceTests : IDisposable
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
    public void Load_TiedostoaEiOle_PalauttaaOletukset()
    {
        var service = new SettingsService(_dir);

        AppSettings settings = service.Load();

        Assert.False(settings.Overlay.Enabled);
        Assert.Equal(OverlayCorner.TopRight, settings.Overlay.Corner);
        Assert.Equal(0.85, settings.Overlay.Opacity);
        Assert.True(settings.Overlay.ShowCpu);
        Assert.False(settings.Overlay.ShowFans);
    }

    [Fact]
    public void SaveJaLoad_ArvotSailyvat()
    {
        var service = new SettingsService(_dir);
        var settings = new AppSettings();
        settings.Overlay.Enabled = true;
        settings.Overlay.Corner = OverlayCorner.BottomLeft;
        settings.Overlay.Opacity = 0.5;
        settings.Overlay.ShowDisks = false;

        service.Save(settings);
        AppSettings loaded = new SettingsService(_dir).Load();

        Assert.True(loaded.Overlay.Enabled);
        Assert.Equal(OverlayCorner.BottomLeft, loaded.Overlay.Corner);
        Assert.Equal(0.5, loaded.Overlay.Opacity);
        Assert.False(loaded.Overlay.ShowDisks);
    }

    [Fact]
    public void Load_VioittunutTiedosto_PalauttaaOletuksetEikaHeitaPoikkeusta()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ tämä ei ole jsonia");
        var service = new SettingsService(_dir);

        AppSettings settings = service.Load();

        Assert.False(settings.Overlay.Enabled);
    }
}
