using System.Globalization;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using Xunit;

namespace HardwareMonitor.Tests.Localization;

public class EnglishResourceTests
{
    /// <summary>Vaihtaa testisäikeen UI-kulttuurin englanniksi ja palauttaa lopuksi.</summary>
    private sealed class EnglishUi : IDisposable
    {
        private readonly CultureInfo _old = CultureInfo.CurrentUICulture;

        public EnglishUi() =>
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        public void Dispose() => CultureInfo.CurrentUICulture = _old;
    }

    [Fact]
    public void RajaArvoviesti_KaantyyEnglanniksi()
    {
        using var _ = new EnglishUi();
        var monitor = new ThresholdMonitor(new ThresholdSettings());
        var metrics = new KeyMetrics(null, 90, null, null, null, null, null, null,
            null, null, null, null, null,
            Array.Empty<DiskMetrics>(), Array.Empty<FanMetrics>());
        var t0 = new DateTimeOffset(2026, 7, 9, 21, 0, 0, TimeSpan.FromHours(3));

        var all = new List<ThresholdEvent>();
        for (int s = 0; s <= 60; s++)
        {
            all.AddRange(monitor.Update(metrics, t0.AddSeconds(s),
                new Dictionary<string, string>()).Events);
        }

        ThresholdEvent e = Assert.Single(all);
        Assert.Contains("exceeded the warning limit", e.Message);
        Assert.DoesNotContain("ylitti", e.Message);
    }

    [Fact]
    public void Validointivirhe_KaantyyEnglanniksi()
    {
        using var _ = new EnglishUi();
        Assert.Equal("Enter a number",
            SettingsValidator.ParseNumber("abc", 20, 120).Error);
    }

    [Fact]
    public void SuomiPysyyNeutraalinaOletuksena()
    {
        // Neutraali resurssi = nykyiset fi-tekstit sellaisinaan (TestCulture kiinnittää fi:n).
        Assert.Equal("Anna numero",
            SettingsValidator.ParseNumber("abc", 20, 120).Error);
    }
}
