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

        // Event luodaan HETI mutexin jälkeen: jos toinen instanssi ehtii
        // signaloida ennen kuin ikkuna on valmis, AutoReset-event säilyttää
        // signaalin kunnes kuuntelija käynnistyy.
        _showEvent = new EventWaitHandle(
            initialState: false, EventResetMode.AutoReset, ShowEventName);

        // Kieli asetuksista ennen ikkunoiden luontia; DefaultThreadCurrentUICulture
        // kattaa myös taustasäikeet (raportit, analyysit, insights).
        AppSettings settings = new SettingsService().Load();
        CultureInfo ui = LanguageResolver.Resolve(
            settings.Language, CultureInfo.InstalledUICulture);
        CultureInfo.DefaultThreadCurrentUICulture = ui;
        Thread.CurrentThread.CurrentUICulture = ui;

        var window = new MainWindow(startInTray);
        MainWindow = window;

        // Kuuntelija vasta kun MainWindow on olemassa — muuten signaali
        // kulutettaisiin tilassa, jossa ikkunaa ei vielä voi näyttää.
        StartShowWindowListener();

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
            // Luo-tai-avaa: jos ensimmäinen instanssi ei ole vielä ehtinyt
            // luoda eventtiä, signaali jää silti talteen (AutoReset säilyy
            // asetettuna kunnes joku odottaa sitä).
            using var showEvent = new EventWaitHandle(
                initialState: false, EventResetMode.AutoReset, ShowEventName);
            showEvent.Set();
        }
        catch (Exception)
        {
            // Ei saatu yhteyttä (eri oikeustason instanssi tms.) —
            // poistutaan silti hiljaa, instansseja on jo yksi.
        }
    }

    /// <summary>Kuuntelee toisen instanssin näyttöpyyntöjä taustasäikeessä.</summary>
    private void StartShowWindowListener()
    {
        var listener = new Thread(() =>
        {
            while (_showEvent!.WaitOne())
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
