# Design: Dashboard (Vaihe 2) + Työpöytäoverlay (Vaihe 2.5)

Päivätty: 8.7.2026. Hyväksytty käyttäjän kanssa käydyssä suunnittelukeskustelussa.

## Tavoite

Kaksi toisiaan tukevaa ominaisuutta:

1. **Dashboard (Vaihe 2, määrittelyn luvut 24–25):** pääikkunaan selkeät kortit
   (CPU / GPU / RAM / Levyt / Tuulettimet) raakasensoripuun rinnalle.
2. **Overlay (Vaihe 2.5, määrittelyn luku 27):** pieni puoliläpinäkyvä,
   aina päällimmäisenä pysyvä näkymä, joka näyttää tärkeimmät arvot muiden
   sovellusten päällä eikä katoa sovellusta vaihdettaessa.

## Käyttäjän kanssa lukitut päätökset

| Kysymys | Päätös |
|---|---|
| Missä overlayn pitää näkyä? | **Vain työpöytäkäyttö** (+ toimii borderless-ikkunoissa). Ei exclusive fullscreen -pelituki­vaatimusta. |
| FPS-mittaus? | **Ei ensimmäiseen versioon.** Kirjataan jatkokehitysideaksi (PresentMon/ETW). |
| Vuorovaikutus | **Läpi-klikattava** (`WS_EX_TRANSPARENT` + `WS_EX_NOACTIVATE`), ohjaus pääohjelman asetuksista. |
| Järjestys | **Dashboard ensin, overlay heti perään.** Yhteinen pohjatyö (`KeyMetricsService`) tehdään kerran. |
| Toteutustapa | **WPF-läpinäkyvä ikkuna samassa prosessissa** (vaihtoehto A). DirectX-injektio (C) hylätty tarpeettomana ja riskialttiina; matalan tason Direct2D (B) hylätty ylimitoitettuna 1 Hz -päivitykselle. |

## Arkkitehtuuri

### HardwareMonitor.Core (uutta)

- **`KeyMetrics`** (malli): tärkeimmät arvot valmiiksi poimittuina.
  - CPU: käyttö-%, package-lämpö °C, max-kello MHz, teho W (jos saatavilla)
  - GPU: käyttö-%, lämpö °C, hotspot °C, VRAM käytetty/yhteensä MB, teho W
  - RAM: käyttö-%, käytetty GB, yhteensä GB
  - Levyt: lista (nimi, lämpö °C, aktiivisuus-%)
  - Tuulettimet: lista (nimi, RPM)
  - Kaikki arvot nullable — puuttuva sensori ei ole virhe.
- **`KeyMetricsService`**: `Extract(IReadOnlyList<HardwareGroup>) -> KeyMetrics`.
  Poiminta sensorityypin ja nimen perusteella (esim. `Temperature` + "CPU Package",
  hotspot: `Temperature` + "GPU Hot Spot"). Ei heitä poikkeuksia puuttuvasta datasta.
- **`AppSettings` + lataus/tallennus** (`%LOCALAPPDATA%\HardwareMonitor\settings.json`):
  kevyt pohja, joka laajenee Vaihe 4:ssä raja-arvoilla. Overlay-osio:
  `enabled`, `corner` (TopLeft/TopRight/BottomLeft/BottomRight), `marginPx`,
  `opacity` (0.3–1.0), `fontSize`, rivivalinnat (`showCpu`, `showGpu`, `showRam`,
  `showDisks`, `showFans`). Tallennus heti muutoksesta, lataus käynnistyksessä.
  Vioittunut tiedosto → oletukset käyttöön + merkintä debug-lokiin.

### HardwareMonitor.App (uutta)

- **Dashboard**: pääikkunaan `TabControl`, jossa välilehdet **"Dashboard"**
  (oletuksena auki, korttinäkymä CPU / GPU / RAM / Levyt / Tuulettimet) ja
  **"Kaikki sensorit"** (nykyinen sensoripuu). Kortit sitovat `KeyMetrics`-arvot.
- **`OverlayWindow`**: reunaton (`WindowStyle=None`), läpinäkyvä tausta
  (`AllowsTransparency=True`), `Topmost=True`, `ShowInTaskbar=False`.
  Ikkunakahvan luonnin jälkeen asetetaan `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE |
  WS_EX_TOOLWINDOW` (läpi-klikattava, ei fokusta, ei Alt+Tabia).
  Tumma pyöristetty puoliläpinäkyvä tausta, kompaktit rivit:
  `CPU 22 %  42 °C`, `GPU 6 %  49 °C  hot 62 °C`, `RAM 19 %  12,3 GB`, `NVMe 62 °C`.
- **`OverlayViewModel`**: näyttää `KeyMetrics`-arvoista asetusten mukaan valitut rivit.
- Pääikkunaan **Overlay-asetusryhmä**: päällä/pois, kulma, läpinäkyvyys, rivivalinnat.

### Tietovirta

`MainViewModel` lukee sensorit 1 s välein (nykyinen mekanismi) →
`KeyMetricsService.Extract(groups)` → sama `KeyMetrics`-olio Dashboardille ja
overlaylle. Overlay ei lue sensoreita itse: ei tuplakuormaa, arvot synkassa.

## Virheenkäsittely

- Puuttuva sensori → arvo `null` → rivi näyttää "—" tai piilotetaan.
- Overlayn päivitysvirhe ei kaada sovellusta: try/catch + `DebugLogger`-merkintä.
- Näytön resoluution/DPI:n muutos → overlay asemoituu uudelleen valittuun kulmaan
  (`SystemEvents.DisplaySettingsChanged`).
- Sovelluksen sulkeutuessa overlay sulkeutuu mukana eikä estä Windowsin sammutusta.
- V1 asemoi ensisijaiselle näytölle; monen näytön valinta on jatkokehitystä.

## Testaus

- **Uusi `HardwareMonitor.Tests`-projekti (xUnit)** — määrittelyn luvun 23 mukaisesti.
  `KeyMetricsService`-yksikkötestit synteettisillä `HardwareGroup`-puilla:
  - poimii CPU package -lämmön ja CPU Total -kuorman
  - valitsee GPU hotspotin kun se on olemassa, muuten null
  - käsittelee puuttuvat sensorit (kaikki null, ei poikkeusta)
  - usean levyn kone → kaikki levyt listassa
  - `AppSettings`: oletukset, tallennus/lataus, vioittunut JSON → oletukset.
- **Käsintestaus overlaylle**: klikkaus menee läpi alla olevaan sovellukseen; pysyy
  päällimmäisenä sovellusvaihdoissa; ei näy Alt+Tabissa; läpinäkyvyys ja kulma toimivat.

## Rajattu ulos (kirjataan jatkokehitysideoihin)

- FPS-mittaus (PresentMon/ETW) — oma työvaiheensa myöhemmin.
- Exclusive fullscreen -pelien päälle piirtäminen (DirectX-injektio).
- Monen näytön tuki overlayn sijainnille.
- Raja-arvojen värikoodaus overlayssa (tulee luontevasti Vaihe 4:n mukana).

## Työjärjestys

1. `HardwareMonitor.Tests`-projekti + `KeyMetrics`/`KeyMetricsService` (TDD)
2. `AppSettings` (TDD)
3. Dashboard-kortit pääikkunaan
4. `OverlayWindow` + `OverlayViewModel` + asetusryhmä pääikkunaan
5. ROADMAP-päivitys (Vaihe 2 valmis, Vaihe 2.5 valmis, FPS jatkokehitysideaksi)
