# HANDOFF — 10.7.2026 illan istunto

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/ROADMAP.md` ja tarvittaessa specit `docs/superpowers/specs/`-
ja planit `docs/superpowers/plans/`-kansioista.

## Tilanne yhdellä lauseella

**machine-insights.md v2 on valmis ja todennettu ajossa** (155 testiä läpi,
kaikki pushattu haaralle `claude/windows-11-program-setup-rxuyhn`), ja
repon juuressa on **REVIEW-BRIEF.md** ulkopuolista bugitarkastusta varten —
**seuraavaksi: tarkastuksen löydökset + LICENSE + release + repo julkiseksi.**

## Huomisen suunnitelma (sovittu 10.7. illalla)

1. **Ulkopuolinen bugitarkastus**: käyttäjä ajaa tarkastajan (esim. AI-
   katselmoijan) repoa vasten — tarkastaja ohjeistetaan lukemaan ensin
   `REVIEW-BRIEF.md` (juuressa, englanniksi, TAHALLAAN committoimatta).
   Sitten löydösten läpikäynti ja korjaukset yhdessä.
2. **LICENSE + release + repo julkiseksi** (käyttäjän päätökset: lisenssi
   esim. MIT — LibreHardwareMonitorLib on MPL-2.0, mainittava; README
   päivitettävä samalla, se kertoo yhä Vaihe 1:stä!) + paketointi
   (self-contained, autostart-polun päivitys).

## Mitä tämän päivän istunnossa tehtiin (10.7.2026)

1. **machine-insights.md v2** (spec `docs/superpowers/specs/
   2026-07-10-machine-insights-v2-design.md`, plan `docs/superpowers/
   plans/2026-07-10-machine-insights-v2.md`, TDD, 9 taskia):
   - **Johdanto tekoälylle** -osio (mikä tiedosto on, miten tulkitaan).
   - **Koneen kokoonpano**: uusi `MachineSpecReader` (Core/Insights) johtaa
     CPU/GPU/emolevy/RAM/levyt/Windows-version HardwareGroup-listasta;
     samannimiset levyt ryhmitellään ("2 × Samsung SSD 860 EVO 1TB").
   - **Trendit 7 pv vs 30 pv**: vain selvät muutokset (lämmöt ≥ 3 °C,
     kuorma/RAM ≥ 10 %-yks); `GetSampleStats` kutsutaan kahdesti.
   - **10 viimeisintä WARNING/CRITICAL-tapahtumaa** aikaleimoineen
     (paikallisaika), INFO ohitetaan.
   - **`AppSettings.InsightsNotes`** + monirivikenttä Asetukset → Yleiset
     ("Omat lisätiedot koneesta") — käyttäjän teksti liitetään tiedostoon.
   - Refaktorointi: `Build(MachineInsightsInput)` — syöterecord, ei enää
     paisuvaa parametrilistaa.
2. **Ajonaikaisessa todennuksessa löytyi ja korjattiin 2 bugia**:
   - RAM näytti 68 GB: LHM:n "Virtual Memory" -ryhmän sensorit ovat MYÖS
     nimeltään "Memory Used"/"Memory Available" → reader ohittaa ryhmät
     joiden nimessä on "Virtual"; laitenimet myös trimmataan (LHM-nimissä
     häntävälilyöntejä).
   - Käynnistyskirjoituksen kokoonpano jäi "—":ksi (kirjoitus ehti ennen
     ensimmäistä sensoriluentaa) → kirjoitus odottaa ensimmäistä luentaa
     (max 15 s, Volatile.Read-polling).
3. **ROADMAP päivitetty**: Vaihe 8:n valmiit kohdat merkitty (ilmoitukset/
   asetussivu/graafit, fi/en, machine-insights v2); avoinna LICENSE +
   paketointi.
4. **REVIEW-BRIEF.md** luotu repon juureen (englanniksi, ei committoitu):
   briiffi ulkopuoliselle bugitarkastajalle — sovelluksen kuvaus,
   arkkitehtuuri, säikeistysmalli, 10 invarianttia (= mikä on bugi) ja
   "tahalliset erikoisuudet" -lista (= mikä EI ole bugi), build/testi-
   komennot ja ehdotetut tarkastuspainopisteet.

## Arkkitehtuurikartta

```
src/HardwareMonitor.Core/         (ei UI-riippuvuuksia, yksikkötestattu)
  Sensors/       SensorService (LHM-luku), HardwareGroup, SensorReading
  Metrics/       KeyMetrics(+Disk/FanMetrics), KeyMetricsService.Extract()
  Storage/       SampleAggregator, HistoryDb (meta, ReadEventsSince,
                 GetSampleStats, ReadSampleRows), EventLogService
  Analysis/      ThresholdMonitor, RiskAnalyzer, LastStateService
  Insights/      MachineInsightsBuilder (Build(MachineInsightsInput)),
                 MachineSpecReader, MachineSpec, MachineInsightsInput ← UUTTA
  Reports/       ReportBuilder, CsvExporter
  Charts/        ChartHistoryBuilder (harvennus + sarjat graafeille)
  WindowsEvents/ WindowsEventClassifier, SystemEventReader, Collector
  Localization/  Strings(.resx/.en.resx) + LanguageResolver
  Settings/      AppSettings (ml. Language, InsightsNotes ← UUSI),
                 SettingsService, SettingsValidator
src/HardwareMonitor.App/          (WPF, MVVM ilman kirjastoja)
  Localization/  UiStrings(.resx/.en.resx)
  App.xaml.cs    OnStartup: kulttuuri ENNEN ikkunoita; --tray
  MainWindow     5 välilehteä; Asetukset → Yleiset: uusi lisätietokenttä
  ViewModels/    Main (taustatyöt Interlocked-lipuin; _latestGroups +
                 OsDescriptionText ← UUTTA), Dashboard, Overlay,
                 Settings (InsightsNotes-property), History
src/HardwareMonitor.Tests/        155 testiä (xUnit); TestCulture
                                  kiinnittää fi-UI-kulttuurin
```

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj
.\run.ps1 -AsAdmin                  # TAI tuplaklikkaa "Hardware Monitor.lnk"
```

- **`dotnet test` EI buildaa App-projektia** — ilman buildia ajat vanhaa exeä.
- **Pysäytä HardwareMonitor.exe ennen buildia** (käyttäjä sulkee trayn
  kautta Lopeta — Stop-Process kirjaisi kaatumistapahtuman). Sovellus jäi
  illalla ajoon uusimmalla buildilla.
- **UUSI GOTCHA**: LHM:n "Virtual Memory" -ryhmän sensorit ovat samannimisiä
  kuin fyysisen muistin ("Memory Used"/"Memory Available") — RAM-laskenta
  vain ryhmistä joiden nimessä EI ole "Virtual". LHM-laitenimissä voi olla
  häntävälilyöntejä → Trim readerissa.
- **Lokalisointi-gotchat**: tapahtumien Component-arvot ovat kannan
  LUOKITTELUAVAIMIA — ei lokalisoida. En-satelliitista puuttuva avain
  palautuu NEUTRAALIIN (fi) — kirjaa myös identtiset en-arvot. Uudet
  tekstit: avain molempiin resx:iin + accessor käsin.
- **WCAG AAA** (käyttäjän vaatimus): uusien tekstien kontrasti ≥ 7:1.
- WPF: paikallinen arvo sidottuun DP:hen tuhoaa sidonnan; Loaded ei
  laukea näyttämättömälle ikkunalle; UIPI estää syötteet korotettuun
  ikkunaan. Microsoft.Data.Sqlite poolaa → HistoryDb.Dispose ClearPool.
- ThresholdSettings-olion VIITE ei saa vaihtua (asetussivu ja monitor
  jakavat saman olion).

## Tiedostosijainnit ajossa

- Asetukset: `%LOCALAPPDATA%\HardwareMonitor\settings.json` (ml. InsightsNotes)
- Debug-loki: `%LOCALAPPDATA%\HardwareMonitor\logs\debug.log`
- Historia: `%LOCALAPPDATA%\HardwareMonitor\data\history.db` (+ -wal/-shm)
- Viimeisin tila: `%LOCALAPPDATA%\HardwareMonitor\data\last_state.json`
- Konetuntemus-loki: `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md`
  — kirjoitus käynnistyksessä (odottaa 1. sensoriluennan, max 15 s)
  + 30 min välein (tick % 1800 == 60)
- Ajastettu tehtävä: `schtasks /Query /TN HardwareMonitor /XML`

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440.
  Fan #2 = AIO-pumppu (~1950 RPM). Koneessa KAKSI samannimistä 860 EVO:ta.
  Käyttäjän InsightsNotes: "Prossulla on AIO-vesijäähdytys."
- Committoimatta TAHALLAAN: REVIEW-BRIEF.md ja windows-11-hardware-
  monitor-ohjelman-aloitusmaarittely.md (sama sisältö kuin
  docs/requirements.md, vain rivinvaihdot eroavat); "Hardware Monitor.lnk"
  gitignoressa.
- Events-taulussa on yhä vanhoja testihälytyksiä (raja 30 °C) ja seka-
  kielisiä viestejä — dataa, ei bugi (kirjattu myös REVIEW-BRIEFiin).
- README on VANHENTUNUT (kertoo Vaihe 1:stä) — päivitys julkaisuvaiheessa.
