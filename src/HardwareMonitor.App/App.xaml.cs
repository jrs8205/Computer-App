using System.Globalization;
using System.Windows;
using HardwareMonitor.Core.Localization;
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.App;

public partial class App : Application
{
    /// <summary>
    /// Ajastettu tehtävä käynnistää sovelluksen tällä argumentilla: pääikkuna
    /// jää suoraan trayhin ja vain overlay avautuu (jos se on asetuksissa päällä).
    /// </summary>
    public const string TrayArgument = "--tray";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Kieli asetuksista ennen ikkunoiden luontia; DefaultThreadCurrentUICulture
        // kattaa myös taustasäikeet (raportit, analyysit, insights).
        AppSettings settings = new SettingsService().Load();
        CultureInfo ui = LanguageResolver.Resolve(
            settings.Language, CultureInfo.InstalledUICulture);
        CultureInfo.DefaultThreadCurrentUICulture = ui;
        Thread.CurrentThread.CurrentUICulture = ui;

        bool startInTray = e.Args.Contains(TrayArgument, StringComparer.OrdinalIgnoreCase);

        var window = new MainWindow(startInTray);
        MainWindow = window;

        if (!startInTray)
        {
            window.Show();
        }
    }
}
