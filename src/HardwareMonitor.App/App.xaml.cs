using System.Windows;

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

        bool startInTray = e.Args.Contains(TrayArgument, StringComparer.OrdinalIgnoreCase);

        var window = new MainWindow(startInTray);
        MainWindow = window;

        if (!startInTray)
        {
            window.Show();
        }
    }
}
