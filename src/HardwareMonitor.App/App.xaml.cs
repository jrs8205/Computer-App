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

    // Local\ = istuntokohtainen: sama käyttäjä, sama kirjautuminen.
    private const string MutexName = @"Local\HardwareMonitor.SingleInstance";
    private const string ShowEventName = @"Local\HardwareMonitor.ShowMainWindow";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool startInTray = e.Args.Contains(TrayArgument, StringComparer.OrdinalIgnoreCase);

        // Toinen samanaikainen instanssi mittaisi samaan kantaan tuplarivit ja
        // kilpailisi settings/last_state-tiedostoista — sallitaan vain yksi.
        bool isFirstInstance;
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out isFirstInstance);
        }
        catch (Exception)
        {
            // Mutex on olemassa mutta eri oikeustasolla (esim. korotettu vs.
            // korottamaton) — kohdellaan kuin toista instanssia.
            isFirstInstance = false;
        }

        if (!isFirstInstance)
        {
            if (!startInTray)
            {
                SignalExistingInstance();
            }

            Shutdown();
            return;
        }

        // Kieli asetuksista ennen ikkunoiden luontia; DefaultThreadCurrentUICulture
        // kattaa myös taustasäikeet (raportit, analyysit, insights).
        AppSettings settings = new SettingsService().Load();
        CultureInfo ui = LanguageResolver.Resolve(
            settings.Language, CultureInfo.InstalledUICulture);
        CultureInfo.DefaultThreadCurrentUICulture = ui;
        Thread.CurrentThread.CurrentUICulture = ui;

        StartShowWindowListener();

        var window = new MainWindow(startInTray);
        MainWindow = window;

        if (!startInTray)
        {
            window.Show();
        }
    }

    /// <summary>Pyytää jo käynnissä olevaa instanssia näyttämään pääikkunansa.</summary>
    private static void SignalExistingInstance()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
            showEvent.Set();
        }
        catch (Exception)
        {
            // Ei saatu yhteyttä (poistumassa oleva tai eri oikeustason
            // instanssi) — poistutaan silti hiljaa, instansseja on jo yksi.
        }
    }

    /// <summary>Kuuntelee toisen instanssin näyttöpyyntöjä taustasäikeessä.</summary>
    private void StartShowWindowListener()
    {
        _showEvent = new EventWaitHandle(
            initialState: false, EventResetMode.AutoReset, ShowEventName);

        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                try
                {
                    Dispatcher.Invoke(() => (MainWindow as MainWindow)?.RestoreFromTray());
                }
                catch (Exception)
                {
                    return; // dispatcher sammunut — sovellus on poistumassa
                }
            }
        })
        {
            IsBackground = true,
            Name = "SingleInstanceListener",
        };
        listener.Start();
    }
}
