# HANDOFF — 8.7.2026 illan istunto

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/ROADMAP.md` (vaiheiden tila) ja tarvittaessa specit
`docs/superpowers/specs/`-kansiosta.

## Tilanne yhdellä lauseella

Vaiheet 1–4 ovat valmiit, testattu ja pushattu haaralle
`claude/windows-11-program-setup-rxuyhn`; seuraavaksi on tarkoitus tehdä
**Vaihe 5: Windows Event Log** (Kernel-Power 41, WHEA, näyttöajurivirheet),
sitten Vaihe 6 (riskianalyysi + konetuntemus-loki), Vaihe 7 (raportit) ja
Vaihe 8 (viimeistely; kielituki fi/en ja repon julkistus tehdään ihan lopuksi
— käyttäjän päätös 8.7.2026).

## Mitä tänään tehtiin (aikajärjestyksessä)

1. **Ympäristö kuntoon:** .NET 8 SDK asennettu (winget). Juurisyy CPU-sensorien
   puuttumiseen selvitetty: Windows 11:n haavoittuvien ajurien estolista blokkaa
   WinRing0:n → LibreHardwareMonitorLib päivitetty 0.9.4 → **0.9.6** ja koneelle
   asennettu **PawnIO-ajuri** (pawnio.eu; tarkistus `sc.exe query PawnIO`).
   Nyt kaikki sensorit löytyvät (CPU-lämmöt/kellot, Nuvoton-tuulettimet, VRM…).
2. **Vaihe 2 — Dashboard:** `KeyMetricsService` (Core/Metrics) poimii tärkeimmät
   arvot raakasensoripuusta; Dashboard-välilehti korteilla (CPU/GPU/RAM/Levyt/
   Tuulettimet) + "Kaikki sensorit" -välilehti; uusi xUnit-testiprojekti.
   Bugikorjaus: NVMe-levyillä ei admin-tilassa ole "Temperature"-pääsensoria
   vaan "Temperature #1/#2" → fallback lisätty (C:-aseman 970 EVO Plus näkyy nyt).
3. **Vaihe 2.5 — Overlay:** läpi-klikattava always-on-top-paneeli
   (WS_EX_TRANSPARENT + NOACTIVATE + TOOLWINDOW), rivivalinnat/kulma/läpinäkyvyys
   asetuksista, RAM-riville kokonaismäärä ("14,2/64 GB").
4. **Vaihe 2.6 — Nimilaput, tray, ikoni, autostart:** tuulettimien nimeäminen
   kaksoisklikkauksella (avain = sensorin pysyvä Identifier; käyttäjä nimesi
   Fan #2:n "AIO-pumppu"), tray-pienennys (pienennä JA sulje → tray, mittaus
   jatkuu; valikko Näytä/Overlay/Lopeta), vektori-ikoni (Assets/icon.svg +
   `tools/generate-icon.ps1` → app.ico), autostart Task Schedulerilla
   (/RL HIGHEST → käynnistyy adminina ilman UAC:ta).
5. **Vaihe 3 — SQLite-lokitus:** `SampleAggregator` kokoaa 1 s -lukemat
   **5 s koosteriveiksi (min/avg/max)** — piikit eivät katoa, kanta pysyy pienenä;
   `HistoryDb` (WAL, Microsoft.Data.Sqlite 8.0.11, taulut samples/disk_samples/
   fan_samples/events), `EventLogService`, retention 30 vrk, statusrivillä
   lokirivilaskuri. Kanta: `%LOCALAPPDATA%\HardwareMonitor\data\history.db`.
6. **Overlay-UX:** koon väpätys korjattu (kiinteät kenttäleveydet + salpalukko),
   **raahaussiirto** ("Siirrä overlayta" -kytkin: läpi-klikattavuus pois siirron
   ajaksi, sijainti talteen; kulmavalinta palauttaa automatiikan).
7. **Vaihe 4 — Raja-arvot:** `ThresholdMonitor` (Core/Analysis, puhdas tilakone):
   väritila UI:hin heti, tapahtuma vasta yhtäjaksoisen keston jälkeen
   (WARNING 30 s / CRITICAL 10 s), palautumis-INFO kestolla ja huipulla,
   cooldown 5 min, tuuletinsääntö (istunnossa pyörinyt tuuletin 0 RPM + CPU
   ≥ 80 °C → CRITICAL). Kortit ja levyrivit värjätään
   (#A5D6A7/#FFB74D/#EF5350), overlayn reunus näyttää pahimman tilan.
   Todennettu päästä päähän lasketulla rajalla; rajat palautettu oletuksiin.
8. **Bugikorjaus (käyttäjän löytämä):** overlayn hälytysreunus katosi pysyvästi
   siirron jälkeen — WPF:ssä paikallinen arvo sidottuun ominaisuuteen tuhoaa
   sidonnan (ClearValue EI palauta sitä). Korjattu: kaikki reunusvärit kulkevat
   OverlayViewModelin kautta yhtä sidontaa pitkin.

## Arkkitehtuurikartta

```
src/HardwareMonitor.Core/         (ei UI-riippuvuuksia, yksikkötestattu)
  Sensors/   SensorService (LHM-luku), HardwareGroup, SensorReading
  Metrics/   KeyMetrics(+Disk/FanMetrics), KeyMetricsService.Extract()
  Storage/   SampleAggregator, AggregatedSample, HistoryDb, EventLogService
  Analysis/  ThresholdMonitor (tilat + tapahtumat), ThresholdState
  Settings/  AppSettings (Overlay/Logging/Thresholds/FanLabels/MinimizeToTray),
             SettingsService (settings.json)
  Logging/   DebugLogger (debug.log)
src/HardwareMonitor.App/          (WPF, MVVM ilman kirjastoja)
  MainWindow            TabControl (Dashboard + Kaikki sensorit), yläpalkin
                        asetusrivi, tray-NotifyIcon, tuuletinnimien muokkaus
  OverlayWindow         läpi-klikattava topmost-paneeli, siirtotila, reunusvärit
  ViewModels/           Main, Dashboard(+DiskRow/FanRow), Overlay, Sensor, Hardware
  Services/             AutostartService (schtasks)
  Assets/               icon.svg (master), app.ico (generoitu)
src/HardwareMonitor.Tests/        32 testiä (xUnit), kaikki läpi
```

Datavirta joka sekunti: `SensorService.Read()` → `KeyMetricsService.Extract()`
→ Dashboard + Overlay + `ThresholdMonitor.Update()` (värit+tapahtumat) +
`SampleAggregator.Add()` (joka 5. s → HistoryDb taustasäikeessä).

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj
.\run.ps1 -AsAdmin                  # sensorit vaativat adminin
.\tools\generate-icon.ps1           # ikonin uudelleengenerointi
```

- **`dotnet test` EI buildaa App-projektia** (testit riippuvat vain Coresta) —
  ilman `dotnet build`ia ajat vanhaa exeä ja "muutos ei näy".
- **Pysäytä HardwareMonitor.exe ennen buildia** (lukitsee DLL:t).
- App-csprojissa `UseWindowsForms` + `<Using Remove>` System.Windows.Forms ja
  System.Drawing — globaalit usingit törmäävät WPF-tyyppeihin.
- Microsoft.Data.Sqlite poolaa yhteydet: HistoryDb.Dispose kutsuu ClearPool.
- WPF: älä koskaan aseta paikallista arvoa sidottuun DP:hen — visuaalitilat VM:n kautta.
- FindWindow ei näe korotetun prosessin ikkunoita — UI-todennus ruutukaappauksella.

## Tiedostosijainnit ajossa

- Asetukset: `%LOCALAPPDATA%\HardwareMonitor\settings.json`
- Debug-loki: `%LOCALAPPDATA%\HardwareMonitor\logs\debug.log`
- Historia+tapahtumat: `%LOCALAPPDATA%\HardwareMonitor\data\history.db` (+ -wal/-shm)

## Seuraavat askeleet (huomenna)

1. **Vaihe 5 — Windows Event Log** (määrittelyn luvut 18, 32; System.Diagnostics.
   Eventing.Reader): Kernel-Power 41, WHEA-Logger, näyttöajurivirheet,
   Disk/Ntfs/NVMe, BugCheck. Tulokset events-tauluun ja myöhemmin analyysiin.
2. Vaihe 6 — riskianalyysi, "ennen kaatumista" -puskuri + last_state.json,
   **konetuntemus-loki** (machine-insights.md — käyttäjän idea: jatkuvasti
   oppiva optimointitiedosto, jota myös Claude lukee).
3. Vaihe 7 — raportit; Vaihe 8 — ilmoitukset, asetussivu, graafit, kielituki
   fi/en, LICENSE + repo julkiseksi (viimeisenä), paketointi.

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440. Fan #2 =
  AIO-pumppu (~1950 RPM vakio). C:-aseman (970 EVO Plus) ohjainlämpö
  "Temperature #2" käy ~83 °C:ssa — hyvä seurattava Vaihe 4:n rajoilla.
- Ohjelman oma kuorma mitattu: ~0,15 % koko CPU:sta, ~175 MB RAM (Debug-build).
- Tuulettimien nimiä ei saa mistään rajapinnasta tällä emolevyllä (ASUSHW on
  SMBus-variantti, ei sensor-info) — siksi nimilaput; HWiNFO shared memory
  kirjattu jatkokehitysideaksi.
- Kaikki designit: `docs/superpowers/specs/`, suunnitelmat: `docs/superpowers/plans/`.
