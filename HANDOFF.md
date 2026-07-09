# HANDOFF — 9.7.2026 aamun istunto

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/ROADMAP.md` (vaiheiden tila) ja tarvittaessa specit
`docs/superpowers/specs/`-kansiosta.

## Tilanne yhdellä lauseella

Vaiheet 1–5 ovat valmiit, testattu ja pushattu haaralle
`claude/windows-11-program-setup-rxuyhn`; seuraavaksi **Vaihe 6:
riskianalyysi** (pisteytys + selkokielinen yhteenveto, "ennen kaatumista"
-puskuri + last_state.json, konetuntemus-loki machine-insights.md), sitten
Vaihe 7 (raportit) ja Vaihe 8 (viimeistely; kielituki fi/en ja repon
julkistus ihan lopuksi — käyttäjän päätös 8.7.2026).

## Mitä tässä istunnossa tehtiin (9.7.2026)

1. **Autostart → tray (käyttäjän pyyntö):** Windowsin mukana käynnistyttäessä
   pääikkuna ei enää välähdä näkyviin, vaan vain overlay avautuu ja sovellus
   menee suoraan trayhin. Toteutus: ajastettu tehtävä antaa `--tray`-
   argumentin (`App.TrayArgument`); `App.OnStartup` luo MainWindow'n itse
   (StartupUri poistettu, `ShutdownMode="OnMainWindowClose"`) eikä kutsu
   `Show()`; MainWindow käynnistää mittauksen suoraan konstruktorista, koska
   Loaded ei laukea näyttämättömälle ikkunalle. `AutostartService.
   RefreshIfEnabled()` kirjoittaa tehtävän uudelleen joka käynnistyksessä →
   vanha tehtävä sai uuden argumentin automaattisesti ja exe-polku pysyy
   ajan tasalla. Todennettu ruutukaappauksella (overlay näkyy, ikkunaa ei)
   ja schtasks-kyselyllä.
2. **Vaihe 5 — Windows Event Log (TDD):** `Core/WindowsEvents/`:
   - `WindowsLogEvent` DTO + `WindowsEventClassifier` (puhdas tilaton
     luokittelija: Kernel-Power 41 → CRITICAL, EventLog 6008 → WARNING,
     BugCheck 1001 → CRITICAL, WHEA error-taso → CRITICAL / muu → WARNING,
     Display/nvlddmkm → WARNING, disk/Ntfs/storahci/stornvme error →
     CRITICAL / warning → WARNING, kaikki muu → null; suomenkieliset viestit)
   - `IWindowsEventSource` + `SystemEventReader` (EventLogReader, XPath
     suodattaa providerit ja EventRecordID:n jo kyselyssä; paketti
     System.Diagnostics.EventLog 8.0.1)
   - `WindowsEventCollector`: lue → luokittele → `events`-tauluun tapahtuman
     omalla aikaleimalla (sensor=provider, value=eventId); kirjanmerkki
     `windows_last_record_id` uuteen `meta`-tauluun (HistoryDb.Get/SetMeta)
     → ei duplikaatteja uudelleenkäynnistyksissä
   - MainViewModel: skannaus käynnistyksessä + 5 min välein (tick % 300)
     taustasäikeessä, päällekkäisyys estetty Interlocked-lipulla
3. **Todennus:** 59 testiä läpi (19 luokittelija-, 2 meta-, 6 collector-
   testiä uusia). Ajossa kirjanmerkki asettui (124277); 0 tapahtumaa
   kirjattiin ja Get-WinEvent-vertailu vahvisti että 30 pv:n System-lokissa
   on vain informatiivisia rivejä — kone on terve, tulos oikea.

## Arkkitehtuurikartta

```
src/HardwareMonitor.Core/         (ei UI-riippuvuuksia, yksikkötestattu)
  Sensors/       SensorService (LHM-luku), HardwareGroup, SensorReading
  Metrics/       KeyMetrics(+Disk/FanMetrics), KeyMetricsService.Extract()
  Storage/       SampleAggregator, AggregatedSample, HistoryDb (+meta-taulu),
                 EventLogService
  Analysis/      ThresholdMonitor (tilat + tapahtumat), ThresholdState
  WindowsEvents/ WindowsLogEvent, WindowsEventClassifier, IWindowsEventSource,
                 SystemEventReader (EventLogReader), WindowsEventCollector
  Settings/      AppSettings, SettingsService (settings.json)
  Logging/       DebugLogger (debug.log)
src/HardwareMonitor.App/          (WPF, MVVM ilman kirjastoja)
  App.xaml(.cs)         OnStartup: --tray → ikkuna piiloon; OnMainWindowClose
  MainWindow            TabControl, yläpalkin asetusrivi, tray-NotifyIcon,
                        StartMonitoring() (ctor jos tray, muuten Loaded)
  OverlayWindow         läpi-klikattava topmost-paneeli, siirtotila, reunusvärit
  ViewModels/           Main (+Windows-skannaus), Dashboard, Overlay, ...
  Services/             AutostartService (schtasks, --tray, RefreshIfEnabled)
src/HardwareMonitor.Tests/        59 testiä (xUnit), kaikki läpi
```

Datavirta joka sekunti: `SensorService.Read()` → `KeyMetricsService.Extract()`
→ Dashboard + Overlay + `ThresholdMonitor.Update()` + `SampleAggregator.Add()`
(joka 5. s → HistoryDb). Lisäksi joka 300. s `WindowsEventCollector.Scan()`
taustasäikeessä.

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj
.\run.ps1 -AsAdmin                  # sensorit vaativat adminin
.\tools\generate-icon.ps1           # ikonin uudelleengenerointi
```

- **`dotnet test` EI buildaa App-projektia** — ilman `dotnet build`ia ajat
  vanhaa exeä ja "muutos ei näy".
- **Pysäytä HardwareMonitor.exe ennen buildia** (lukitsee DLL:t). HUOM:
  istunnon lopussa 9.7. jätettiin Debug-exe ajoon `--tray`-tilassa.
- App-csprojissa `UseWindowsForms` + `<Using Remove>` System.Windows.Forms
  ja System.Drawing — globaalit usingit törmäävät WPF-tyyppeihin.
- Microsoft.Data.Sqlite poolaa yhteydet: HistoryDb.Dispose kutsuu ClearPool.
- WPF: älä koskaan aseta paikallista arvoa sidottuun DP:hen — visuaalitilat
  VM:n kautta. WPF: näyttämättömälle ikkunalle ei laukea Loaded.
- FindWindow ei näe korotetun prosessin ikkunoita — UI-todennus
  ruutukaappauksella (System.Drawing CopyFromScreen).
- Kannan voi lukea ohjelman ajaessa ReadOnly-yhteydellä (WAL sallii);
  sqlite3-CLI:tä ei koneella ole — käytä pientä dotnet-konsolia tarvittaessa.

## Tiedostosijainnit ajossa

- Asetukset: `%LOCALAPPDATA%\HardwareMonitor\settings.json`
- Debug-loki: `%LOCALAPPDATA%\HardwareMonitor\logs\debug.log`
- Historia+tapahtumat: `%LOCALAPPDATA%\HardwareMonitor\data\history.db` (+ -wal/-shm)
- Ajastettu tehtävä: `schtasks /Query /TN HardwareMonitor /XML` (Arguments: --tray)

## Seuraavat askeleet

1. **Vaihe 6 — Riskianalyysi** (luvut 17, 19, 31): pisteytys + selkokielinen
   yhteenveto UI:hin; "ennen kaatumista" -puskuri + `last_state.json`;
   **konetuntemus-loki** (machine-insights.md — käyttäjän idea: jatkuvasti
   oppiva optimointitiedosto, jota myös Claude lukee tulevissa istunnoissa).
   Windows-tapahtumat ovat nyt events-taulussa analyysin käytettävissä.
2. Vaihe 7 — raportit (luku 20): "Luo raportti" Markdown/TXT, CSV-vienti.
3. Vaihe 8 — ilmoitukset, asetussivu, graafit, kielituki fi/en, LICENSE +
   repo julkiseksi (viimeisenä), paketointi.

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440. Fan #2 =
  AIO-pumppu (~1950 RPM vakio). C:-aseman (970 EVO Plus) ohjainlämpö
  "Temperature #2" käy ~83 °C:ssa.
- System-loki 30 pv (9.7.2026): ei Kernel-Power 41:tä, ei WHEA:ta, ei levy-
  eikä TDR-virheitä — terve kone, hyvä vertailutaso Vaihe 6:n analyysille.
- Tuulettimien nimiä ei saa rajapinnasta tällä emolevyllä — siksi nimilaput;
  HWiNFO shared memory kirjattu jatkokehitysideaksi.
- Kaikki designit: `docs/superpowers/specs/`, suunnitelmat: `docs/superpowers/plans/`.
