# HANDOFF — 9.7.2026 illan istunto

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/ROADMAP.md` ja tarvittaessa specit `docs/superpowers/specs/`-
ja planit `docs/superpowers/plans/`-kansioista.

## Tilanne yhdellä lauseella

**Vaihe 8 on valmis** (ilmoitukset, asetussivu, historia-graafit, WCAG AAA
-värit, fi/en-kielituki; 134 testiä läpi, kaikki pushattu haaralle
`claude/windows-11-program-setup-rxuyhn`) — **huomenna 10.7.2026 aamulla:
machine-insights.md kunnolliseksi "anna tekoälychatille" -tiedostoksi,
sen jälkeen LICENSE + release + repo julkiseksi.**

## Huomisen suunnitelma (sovittu 9.7. illalla)

1. **machine-insights.md → AI-chat-jakelukuntoon.** Käyttäjän alkuperäinen
   idea (Vaihe 6): tiedosto jonka voi antaa mille tahansa tekoälychatille
   koneen kontekstiksi. Nykyinen versio on suppea (taulukot + muutama
   havainto) — brainstormaa käyttäjän kanssa mitä lisätään, esim.:
   koneen kokoonpano (i9-9900K/RTX 2060/Z390-F/64 GB/levyt), johdanto
   joka kertoo AI:lle mikä tiedosto on ja miten sitä käytetään,
   raja-arvot ja niiden merkitys, trendit ja poikkeamat, tapahtuma-
   historian tiivistelmä, kieli (fi/en seuraa nyt UI-kieltä).
   Generointi: `MachineInsightsBuilder` (Core/Insights, TDD).
2. **LICENSE + release + repo julkiseksi** (käyttäjän päätökset:
   lisenssin valinta, releasen sisältö/paketointi self-contained,
   README?). Tehdään kun kohta 1 on valmis.

## Mitä illan istunnossa tehtiin (9.7.2026)

1. **Vaihe 8.1 Tray-ilmoitukset** (TDD): `Core/Notifications/
   NotificationBuilder` → balloon WARNING/CRITICAL-tapahtumista,
   asetus `AlertNotificationsEnabled`. Todennettu oikealla toastilla.
2. **Vaihe 8.2 Asetussivu** (TDD): Asetukset-välilehti (Yleiset/Raja-arvot/
   Kestot/Lokitus/Overlay), `SettingsValidator` Coressa, `NumericField-
   ViewModel` + `SettingsViewModel`, Palauta oletusrajat -nappi
   (viitteet säilyvät!), yläpalkki siivottu. Muutokset vaikuttavat heti.
3. **Vaihe 8.3 Historia-graafit** (TDD): `Core/Charts/ChartHistoryBuilder`
   (bucket-harvennus ~500 pisteeseen, null-aukot, samannimiset levyt
   #1/#2, tuuletin vain jos pyörii ≥5 % ajasta), `HistoryViewModel` +
   LiveCharts2 2.0.5, aikavälit 1 h/24 h/7 pv/30 pv, taustapäivitys 60 s
   (tick % 60 == 45).
4. **WCAG AAA -värit koko sovellukseen** (käyttäjän vaatimus, kirjattu
   pysyväismuistiin): harmaat → #BDBDBD, virhetekstit → #FF8A80,
   alapalkki → #004C87, graafien akselitekstit valkoiset koossa 14.
   Tooltipit kokonaislukuina.
5. **fi/en-kielituki koko sovellukseen** (TDD; spec + plan docs/
   superpowers/-kansioissa): resx, neutraali fi + en-satelliitti,
   käsintehdyt accessorit (`Strings` Coressa ~148 avainta ml.
   sensorityypit, `UiStrings` Appissa ~114), `LanguageResolver`,
   kielivalinta Asetukset → Yleiset (Automaattinen/Suomi/English),
   voimaan uudelleenkäynnistyksellä. Käyttäjä todensi molemmat kielet.
6. **Käynnistys ilman PowerShelliä**: repon juuressa "Hardware
   Monitor.lnk" (gitignoressa; admin-lippu tavussa 0x15) — käyttäjä
   käynnistää tuplaklikkauksella. `run.ps1 -AsAdmin` buildaa ensin ja
   käynnistää exen suoraan (ei enää jäävää PS-ikkunaa). Lisäksi:
   GPU-tuulettimet näkyvät overlayssa pysähdyksissäkin ("GPU Fan 1 — 0 RPM").

## Arkkitehtuurikartta

```
src/HardwareMonitor.Core/         (ei UI-riippuvuuksia, yksikkötestattu)
  Sensors/       SensorService (LHM-luku), HardwareGroup, SensorReading
  Metrics/       KeyMetrics(+Disk/FanMetrics), KeyMetricsService.Extract()
  Storage/       SampleAggregator, HistoryDb (meta, ReadEventsSince,
                 GetSampleStats, ReadSampleRows), EventLogService
  Analysis/      ThresholdMonitor, RiskAnalyzer, LastStateService
  Insights/      MachineInsightsBuilder (machine-insights.md) ← HUOMENNA
  Reports/       ReportBuilder, CsvExporter
  Charts/        ChartHistoryBuilder (harvennus + sarjat graafeille)
  WindowsEvents/ WindowsEventClassifier, SystemEventReader, Collector
  Localization/  Strings(.resx/.en.resx) + LanguageResolver ← UUSI
  Settings/      AppSettings (ml. Language), SettingsService,
                 SettingsValidator
src/HardwareMonitor.App/          (WPF, MVVM ilman kirjastoja)
  Localization/  UiStrings(.resx/.en.resx) ← UUSI
  App.xaml.cs    OnStartup: kulttuuri ENNEN ikkunoita; --tray
  MainWindow     5 välilehteä: Dashboard / Kaikki sensorit / Asetukset /
                 Historia; yläpalkki: overlay-kytkimet + raporttinapit
  ViewModels/    Main (taustatyöt Interlocked-lipuin), Dashboard,
                 Overlay, Settings (NumericField), History (LiveCharts2)
src/HardwareMonitor.Tests/        134 testiä (xUnit); TestCulture
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
  kautta Lopeta — Stop-Process kirjaisi kaatumistapahtuman). Sovellus
  saattaa olla ajossa aamulla (mahdollisesti englanniksi — kieli takaisin:
  Asetukset/Settings → Kieli / Language → Automaattinen + uudelleenkäynnistys).
- **Lokalisointi-gotchat**: tapahtumien Component-arvot ("Laitteisto",
  "Järjestelmä", "GPU-ajuri", "Levy", "Tuuletin") ovat kantaan
  tallennettavia LUOKITTELUAVAIMIA — ei saa lokalisoida (RiskAnalyzer ja
  MachineInsightsBuilder vertaavat niitä). En-satelliitista puuttuva
  avain palautuu NEUTRAALIIN (fi) — kirjaa myös identtiset en-arvot.
  Uudet tekstit: avain molempiin resx:iin + accessor-luokkaan (käsin;
  resx-designer ei toimi dotnet CLI:llä). UI-kulttuuri asetetaan
  App.OnStartupissa (DefaultThreadCurrentUICulture kattaa taustasäikeet).
- **LiveCharts2**: chartin oletustausta on VALKOINEN — Background XAML:ssa.
  NU1701 (SkiaSharp.Views.WPF net462) vaimennettu App-csprojissa.
- **WCAG AAA** (käyttäjän vaatimus): uusien tekstien kontrasti ≥ 7:1;
  käytössä #BDBDBD (harmaa), #FF8A80 (virhe), valkoiset akselitekstit.
- WPF: paikallinen arvo sidottuun DP:hen tuhoaa sidonnan; Loaded ei
  laukea näyttämättömälle ikkunalle; UIPI estää syötteet korotettuun
  ikkunaan (käyttäjä klikkaa, Claude todentaa kuvakaappauksin).
- Microsoft.Data.Sqlite poolaa: HistoryDb.Dispose kutsuu ClearPool.

## Tiedostosijainnit ajossa

- Asetukset: `%LOCALAPPDATA%\HardwareMonitor\settings.json` (ml. Language)
- Debug-loki: `%LOCALAPPDATA%\HardwareMonitor\logs\debug.log`
- Historia: `%LOCALAPPDATA%\HardwareMonitor\data\history.db` (+ -wal/-shm)
- Viimeisin tila: `%LOCALAPPDATA%\HardwareMonitor\data\last_state.json`
- Konetuntemus-loki: `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md`
  ← huomisen työn kohde; kirjoitus käynnistyksessä + 30 min välein
- Ajastettu tehtävä: `schtasks /Query /TN HardwareMonitor /XML`

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440.
  Fan #2 = AIO-pumppu (~1950 RPM). C:-levyn (970 EVO Plus) ohjainlämpö
  "Temperature #2" käy ~83 °C:ssa. Koneessa KAKSI samannimistä 860 EVO:ta.
- Illan testailu jätti events-tauluun testihälytyksiä (raja 30 °C)
  ja kieli oli hetken en — historiadatan sekakieliset viestit ovat
  odotettuja (tapahtumat ovat dataa, näytetään tallennuskielellään).
- Overlay-fonttikoko on käyttäjän valinnasta 16 — älä "palauta".
