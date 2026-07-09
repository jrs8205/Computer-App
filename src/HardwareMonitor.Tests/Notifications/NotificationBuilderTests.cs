using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Notifications;
using Xunit;

namespace HardwareMonitor.Tests.Notifications;

public class NotificationBuilderTests
{
    private static ThresholdEvent Ev(string level, string message) =>
        new(level, "CPU", "CPU Package", 90, 85, message);

    [Fact]
    public void PoisPaalta_EiIlmoitusta()
    {
        var events = new[] { Ev("WARNING", "CPU-lämpötila ylitti varoitusrajan") };

        Assert.Null(NotificationBuilder.Build(events, enabled: false));
    }

    [Fact]
    public void EiTapahtumia_EiIlmoitusta()
    {
        Assert.Null(NotificationBuilder.Build(Array.Empty<ThresholdEvent>(), enabled: true));
    }

    [Fact]
    public void InfoTapahtumat_EivatLaukaiseIlmoitusta()
    {
        var events = new[] { Ev("INFO", "CPU-lämpötila palautui normaaliksi") };

        Assert.Null(NotificationBuilder.Build(events, enabled: true));
    }

    [Fact]
    public void YksiVaroitus_TuottaaVaroitusilmoituksen()
    {
        var events = new[] { Ev("WARNING", "CPU-lämpötila ylitti varoitusrajan: 90 °C (raja 85 °C)") };

        TrayNotification? n = NotificationBuilder.Build(events, enabled: true);

        Assert.NotNull(n);
        Assert.Equal(NotificationSeverity.Warning, n!.Severity);
        Assert.Equal("Varoitus", n.Title);
        Assert.Equal("CPU-lämpötila ylitti varoitusrajan: 90 °C (raja 85 °C)", n.Message);
    }

    [Fact]
    public void YksiKriittinen_TuottaaKriittisenIlmoituksen()
    {
        var events = new[] { Ev("CRITICAL", "CPU-lämpötila ylitti kriittisen rajan: 96 °C (raja 95 °C)") };

        TrayNotification? n = NotificationBuilder.Build(events, enabled: true);

        Assert.NotNull(n);
        Assert.Equal(NotificationSeverity.Critical, n!.Severity);
        Assert.Equal("Kriittinen hälytys", n.Title);
    }

    [Fact]
    public void UseampiHalytys_YhdistaaViestitJaNostaaVakavuuden()
    {
        var events = new[]
        {
            Ev("WARNING", "RAM-käyttö ylitti varoitusrajan: 90 % (raja 85 %)"),
            Ev("INFO", "GPU-lämpötila palautui normaaliksi"),
            Ev("CRITICAL", "CPU-lämpötila ylitti kriittisen rajan: 96 °C (raja 95 °C)"),
        };

        TrayNotification? n = NotificationBuilder.Build(events, enabled: true);

        Assert.NotNull(n);
        Assert.Equal(NotificationSeverity.Critical, n!.Severity);
        Assert.Equal("2 hälytystä", n.Title);
        Assert.Contains("RAM-käyttö ylitti varoitusrajan: 90 % (raja 85 %)", n.Message);
        Assert.Contains("CPU-lämpötila ylitti kriittisen rajan: 96 °C (raja 95 °C)", n.Message);
        Assert.DoesNotContain("palautui", n.Message);
    }

    [Fact]
    public void PitkaViesti_KatkaistaanBalloonTekstinRajaan()
    {
        // NotifyIcon.BalloonTipText sietää enintään 255 merkkiä.
        var events = new[] { Ev("WARNING", new string('x', 300)) };

        TrayNotification? n = NotificationBuilder.Build(events, enabled: true);

        Assert.NotNull(n);
        Assert.True(n!.Message.Length <= 255);
        Assert.EndsWith("…", n.Message);
    }
}
