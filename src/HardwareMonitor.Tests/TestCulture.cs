using System.Globalization;
using System.Runtime.CompilerServices;

namespace HardwareMonitor.Tests;

/// <summary>
/// Testit ajetaan aina suomenkielisellä UI-kulttuurilla (neutraali kieli),
/// jotta suomenkieliset assertiot eivät riipu ajokoneen kielestä.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Init() =>
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fi-FI");
}
