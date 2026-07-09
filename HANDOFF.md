# HANDOFF — 9.7.2026 aamupäivän istunto

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/ROADMAP.md` (vaiheiden tila) ja tarvittaessa specit
`docs/superpowers/specs/`-kansiosta.

## Tilanne yhdellä lauseella

Vaiheet 1–7 ovat valmiit, testattu ja pushattu haaralle
`claude/windows-11-program-setup-rxuyhn`; **sovittu jatko: Vaihe 8
(viimeistely) illalla 9.7.2026** — ilmoitukset, asetussivu, graafit
(LiveCharts2); kielituki fi/en, LICENSE + repon julkistus ja paketointi
ihan lopuksi (käyttäjän päätös 8.7.2026).

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
3. **Todennus:** ajossa kirjanmerkki asettui (124277); 0 tapahtumaa
   kirjattiin ja Get-WinEvent-vertailu vahvisti että 30 pv:n System-lokissa
   on vain informatiivisia rivejä — kone on terve, tulos oikea.
4. **Vaihe 6 — Riskianalyysi (TDD) + käyttäjän UI-toiveet:**
   - `RiskAnalyzer` (Core/Analysis, puhdas): pisteytys nykytiloista (5/15 p),
     24 h tapahtumista (raja-arvot 2/6, WHEA 6/15, Kernel-Power/BSOD 15,
     6008 6, GPU-ajuri 6, Windows-levyvirhe 10) ja kaatumislipusta (10) →
     Hyvä/Varoitus/Kriittinen + Matala/Kohonnut/Korkea + havainnot + suositus
     suurimman pistelähteen mukaan. Kaatumistapahtuma (sensor=`last_state`)
     EI pisteydy lipun lisäksi (tuplapiste-esto, testattu).
   - `LastStateService`: last_state.json 5 s välein (CleanShutdown=false),
     Dispose merkitsee siistin sulkemisen; Write siistin merkinnän jälkeen
     ei likaa lippua (kilpajuoksusuoja). Käynnistys kirjaa kaatumisesta
     WARNING-tapahtuman viimeisimmillä arvoilla.
   - `MachineInsightsBuilder` (Core/Insights): machine-insights.md —
     normaalitasot/huiput/tapahtumat/optimointivinkit; kirjoitus
     käynnistyksessä + 30 min välein (`MainViewModel.MachineInsightsPath`).
   - `HistoryDb.ReadEventsSince` + `GetSampleStats` (avg/max + per-levy/
     tuuletin GROUP BY).
   - **Dashboardin tilapaneeli**: värillinen tilapiste + "Koneen tila: X ·
     Riskitaso: Y", havainnot arvoineen ja rajoineen, oranssi suositusrivi,
     **väriselite** (vihreä = kunnossa · oranssi = varoitusraja ylittynyt ·
     punainen = kriittinen raja ylittynyt). Analyysi lasketaan joka tick;
     tapahtuma-/huippukoosteet päivittyvät taustalla 60 s välein.
   - **Overlayn reunus aina näkyvissä**: vihreä kun kaikki kunnossa
     (käyttäjän toive), oranssi/punainen hälytyksissä, syaani siirtotilassa.
5. **Todennus:** 84 testiä läpi. Ajossa: paneeli Hyvä-tilassa (kuvakaappaus),
   prosessin tappo + uudelleenkäynnistys → "Edellinen istunto päättyi
   yllättäen", Varoitus/Kohonnut, suositus näkyi; last_state.json ja
   machine-insights.md syntyivät oikealla datalla.
6. **Vaihe 7 — Raportit (TDD; käyttäjän vaatimus: selkokieliset viennit):**
   - `ReportBuilder` (Core/Reports, puhdas): tekstiraportti ilman
     taulukoita — yhteenveto + "Mitä tasot tarkoittavat", arvot muodossa
     "X: 45 °C — kunnossa (varoitusraja 85 °C)" tai "— VAROITUS: …",
     24 h huiput vertailulauseella (raja ≥: "ylitti", ≤10 yksikköä päässä:
     "kävi lähellä", muuten "jäi selvästi alle"), 30 pv normaalitasot,
     tapahtumat ilman INFO-rivejä, sanasto. Levynimet trimmataan (LHM
     jättää loppuvälilyönnin).
   - `CsvExporter` (Core/Reports): fi-Excel-muoto — erotin `;`,
     desimaalipilkku kulttuurista (fi-FI myös aikaerotin '.'), suomen-
     kieliset otsikot yksiköineen, levy/tuuletin-sarakkeet pivotoitu
     nimillä (unioni kaikista riveistä), puuttuva arvo = tyhjä kenttä.
   - `HistoryDb.ReadSampleRows(since)` — koosterivit lapsiriveineen.
   - MainWindow: napit "Luo raportti…" / "Vie CSV…" yläpalkissa →
     SaveFileDialog → kirjoitus **UTF-8 BOM:lla** (ääkköset Excelissä) →
     tiedosto avataan oletusohjelmassa. MainViewModel.BuildReport/BuildCsv
     (null jos dataa ei vielä ole → ystävällinen MessageBox).
7. **Todennus:** 100 testiä läpi; raportti + CSV generoitu oikeasta
   kannasta scratchpad-konsolilla (998 riviä; sisältö luettu läpi),
   napit todennettu kuvakaappauksella.

## Arkkitehtuurikartta

```
src/HardwareMonitor.Core/         (ei UI-riippuvuuksia, yksikkötestattu)
  Sensors/       SensorService (LHM-luku), HardwareGroup, SensorReading
  Metrics/       KeyMetrics(+Disk/FanMetrics), KeyMetricsService.Extract()
  Storage/       SampleAggregator, AggregatedSample, HistoryDb (+meta-taulu,
                 ReadEventsSince, GetSampleStats, ReadSampleRows),
                 EventLogService, SampleStats, SampleRow
  Analysis/      ThresholdMonitor, ThresholdState, RiskAnalyzer (pisteytys +
                 havainnot), LastStateService (last_state.json)
  Insights/      MachineInsightsBuilder (machine-insights.md)
  Reports/       ReportBuilder (selkokielinen raportti), CsvExporter (fi-Excel)
  WindowsEvents/ WindowsLogEvent, WindowsEventClassifier, IWindowsEventSource,
                 SystemEventReader (EventLogReader), WindowsEventCollector
  Settings/      AppSettings, SettingsService (settings.json)
  Logging/       DebugLogger (debug.log)
src/HardwareMonitor.App/          (WPF, MVVM ilman kirjastoja)
  App.xaml(.cs)         OnStartup: --tray → ikkuna piiloon; OnMainWindowClose
  MainWindow            TabControl, yläpalkin asetusrivi + raportti/CSV-napit,
                        tray-NotifyIcon, StartMonitoring() (ctor jos tray,
                        muuten Loaded); Dashboardissa tilapaneeli + väriselite
  OverlayWindow         läpi-klikattava topmost-paneeli, siirtotila, reunus
                        aina näkyvissä (vihreä/oranssi/punainen/syaani)
  ViewModels/           Main (Windows-skannaus, analyysikoosteet, insights,
                        last_state), Dashboard (+ApplySummary), Overlay, ...
  Services/             AutostartService (schtasks, --tray, RefreshIfEnabled)
src/HardwareMonitor.Tests/        84 testiä (xUnit), kaikki läpi
```

Datavirta joka sekunti: `SensorService.Read()` → `KeyMetricsService.Extract()`
→ Dashboard + Overlay + `ThresholdMonitor.Update()` + `RiskAnalyzer.Assess()`
(tilapaneeli) + `SampleAggregator.Add()` (joka 5. s → HistoryDb; samalla
last_state.json). Taustalla: WindowsEventCollector 300 s, analyysikoosteet
60 s, machine-insights.md 1800 s välein.

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
  aamupäivän istunnon lopussa 9.7. Debug-exe jäi ajoon ikkuna näkyvissä
  (admin, ilman --tray-lippua) — pysäytä se ennen illan ensimmäistä buildia.
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
- Viimeisin tila: `%LOCALAPPDATA%\HardwareMonitor\data\last_state.json`
- Konetuntemus-loki: `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md`
  — **lue tämä istunnon alussa**, se kertoo koneen normaalitasot ja ongelmat
- Ajastettu tehtävä: `schtasks /Query /TN HardwareMonitor /XML` (Arguments: --tray)

## Seuraavat askeleet (Vaihe 8, illalla 9.7.2026)

Ehdotettu järjestys illan istunnolle:

1. ~~**Ilmoitukset**~~ **TEHTY 9.7. illalla:** `Core/Notifications/
   NotificationBuilder` (puhdas, TDD: pois päältä / ei tapahtumia / INFO →
   null; WARNING/CRITICAL → otsikko+viesti+vakavuus; useampi yhdistetään;
   katkaisu 255 merkkiin) → `MainViewModel.NotificationRequested` →
   `MainWindow.ShowTrayNotification` (ShowBalloonTip 10 s, Warning/Error-
   ikoni). Asetus `AlertNotificationsEnabled` (oletus true) + checkbox
   "Hälytysilmoitukset" yläpalkissa. Todennettu ajossa madalletulla rajalla
   (30 °C): toast näkyi, tilapaneeli ja overlay reagoivat; rajat palautettu.
2. ~~**Asetussivu**~~ **TEHTY 9.7. illalla:** Asetukset-välilehti (spec:
   docs/superpowers/specs/2026-07-09-settings-page-design.md, plan:
   docs/superpowers/plans/2026-07-09-settings-page.md). Core:
   `SettingsValidator` (TDD: fi/piste-parsinta, välit, warn<crit). App:
   `NumericFieldViewModel` (teksti↔float + virhetila) + `SettingsViewModel`
   (rivilistat, Palauta oletusrajat — viitteet säilyvät!). Ryhmät: Yleiset /
   Raja-arvot / Kestot / Lokitus / Overlay; yläpalkkiin jäi vain Overlay-
   kytkin, Siirrä overlayta ja raporttinapit. Muutos vaikuttaa heti
   (todennettu: raja 30 → tilapaneeli Varoitus sekunneissa; fonttikoko →
   overlay heti; "abc"/96 → virheviestit; käyttäjä testasi kaikki 7 kohtaa).
   116 testiä. Kielivalinnalle varattu paikka Yleiset-ryhmään (resx myöh.).
3. **Graafit**: LiveCharts2 (uusi NuGet Appiin) — lämpöhistoria
   HistoryDb.ReadSampleRows-datasta uudelle välilehdelle.
4. Viimeisenä (erikseen käyttäjän kanssa): kielituki fi/en (resx),
   LICENSE + repo julkiseksi, paketointi (self-contained julkaisu).

Pientä hiottavaa (sopii väliin): fan_samples/insights käyttävät raakanimiä
("Fan #2"), ei nimilappuja ("AIO-pumppu") — voisi mapata nimilaput;
0 RPM -tuulettimet pois insights-taulukosta; JSON/PDF-vienti (luku 20
"myöhemmin"); ylitysten kestolaskenta raporttiin ("RAM yli 90 %: 12 min").

## Huomio testikaatumisista 9.7.

Istunnon testeissä prosessi tapettiin (Stop-Process), joten events-taulussa
on aitoja "Edellinen istunto päättyi yllättäen" -rivejä ja käynnissä olevan
istunnon tilapaneeli näyttää Varoitusta (kaatumislippu +10 p). Tämä on
tarkoituksellista: kaatumisTAPAHTUMA (sensor=last_state) ei pisteydy —
vain heti kaatumista seuraavan istunnon lippu nostaa pisteitä. Siisti
sulkeminen + uudelleenkäynnistys palauttaa Hyvä-tilan.

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440. Fan #2 =
  AIO-pumppu (~1950 RPM vakio). C:-aseman (970 EVO Plus) ohjainlämpö
  "Temperature #2" käy ~83 °C:ssa.
- System-loki 30 pv (9.7.2026): ei Kernel-Power 41:tä, ei WHEA:ta, ei levy-
  eikä TDR-virheitä — terve kone, hyvä vertailutaso Vaihe 6:n analyysille.
- Tuulettimien nimiä ei saa rajapinnasta tällä emolevyllä — siksi nimilaput;
  HWiNFO shared memory kirjattu jatkokehitysideaksi.
- Kaikki designit: `docs/superpowers/specs/`, suunnitelmat: `docs/superpowers/plans/`.
