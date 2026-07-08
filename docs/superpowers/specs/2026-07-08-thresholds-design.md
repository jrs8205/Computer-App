# Design: Vaihe 4 — Raja-arvot ja varoitukset

Päivätty: 8.7.2026. Hyväksytty käyttäjän kanssa. Perustuu määrittelyn lukuihin
16 (rajat ja kesto), 26 (spämmäyksen esto), 29 (asetusnimet) ja 31 (säännöt).

## Lukitut päätökset

| Asia | Päätös |
|---|---|
| Oletusrajat | Luvun 16 mukaan: CPU 85/95 °C, GPU 85/95 °C, hotspot 95/105 °C, NVMe/levyt 70/82 °C, RAM 85/95 %. |
| Kesto | Tapahtuma vasta yhtäjaksoisen ylityksen jälkeen: WARNING 30 s, CRITICAL 10 s. Väritila UI:hin heti. |
| Cooldown | Sama sääntö+taso ei kirjaudu uudelleen 5 minuuttiin; eskalaatio WARNING→CRITICAL ohittaa cooldownin. |
| UI | Kortteissa arvon väri (vihreä/oranssi/punainen: #A5D6A7/#FFB74D/#EF5350), myös levyriveille. Overlayssa paneelin reunus pahimman tilan mukaan. |
| Tapahtumat | Olemassa olevaan events-tauluun; palautuessa INFO, jonka viestissä ylityksen kesto ja huippuarvo. |
| Tuuletinsääntö | Istunnossa pyörinyt (>200 RPM) tuuletin 0 RPM:ssä CPU:n ollessa ≥ 80 °C 10 s ajan → CRITICAL. Ei väärähälytyksiä semi-passiivisista/kytkemättömistä. |
| Rajojen muokkaus | settings.json-tiedostosta (`Thresholds`-osio); graafinen asetussivu Vaihe 8. |

## Arkkitehtuuri

### `ThresholdSettings` (Core/Settings, luvun 29 nimet)

`CpuWarningTemp=85, CpuCriticalTemp=95, GpuWarningTemp=85, GpuCriticalTemp=95,
GpuHotspotWarningTemp=95, GpuHotspotCriticalTemp=105, NvmeWarningTemp=70,
NvmeCriticalTemp=82, RamWarningPercent=85, RamCriticalPercent=95,
WarningSustainSeconds=30, CriticalSustainSeconds=10, EventCooldownMinutes=5,
FanStopCpuTemp=80`. `AppSettings.Thresholds`-osioon.

### `ThresholdMonitor` (Core/Analysis) — puhdas tilakone

- `ThresholdResult Update(KeyMetrics m, DateTimeOffset now)` kerran sekunnissa.
- `ThresholdResult(MetricStates States, IReadOnlyList<ThresholdEvent> Events)`.
- `MetricStates`: välitön tila (Normal/Warning/Critical) mittareille CpuTemp,
  GpuTemp, GpuHotspot, RamLoad ja per levy (sama järjestys kuin KeyMetrics.Disks),
  sekä `Worst` (sisältää myös tuuletinsäännön tilan).
- Sääntöavain: "cpu_temp", "gpu_temp", "gpu_hotspot", "ram_load",
  "disk:{index}", "fan:{identifier}". Per avain seurataan: milloin arvo ylitti
  varoitus-/kriittisrajan yhtäjaksoisesti (`aboveWarnSince`/`aboveCritSince`),
  ylitysjakson huippuarvo, jaksossa jo nostettu taso ja viimeisin
  tapahtuma-aika per taso (cooldown).
- Logiikka per tick: null-arvo = normaali. Ylitys käynnistää jakson; CRITICAL
  nousee kun `now - aboveCritSince >= CriticalSustainSeconds`, WARNING kun
  `now - aboveWarnSince >= WarningSustainSeconds` — kumpikin korkeintaan kerran
  jaksossa ja cooldownin salliessa (eskalaatio samassa jaksossa aina sallittu).
  Arvon palatessa varoitusrajan alle: jos jaksossa nostettiin tapahtuma, kirjataan
  INFO "palautui normaaliksi (huippu X, kesto Y)" ja jakso nollataan.
- Tuuletinsääntö: fani jonka RPM on istunnossa käynyt yli 200 → "on pyörinyt";
  jos se on 0 RPM JA CpuPackageTempC ≥ FanStopCpuTemp yhtäjaksoisesti
  CriticalSustainSeconds → CRITICAL; palautuminen (RPM > 0) → INFO jos nostettu.
- Tapahtumaviestit suomeksi, esim. "CPU-lämpötila ylitti varoitusrajan: 87 °C
  (raja 85 °C)"; recovery "CPU-lämpötila palautui normaaliksi (huippu 96 °C,
  kesto 2 min 13 s)".

### UI

- `DashboardViewModel`: tilaproperyt (`CpuTempState`, `GpuTempState`,
  `GpuHotspotState`, `RamLoadState`) ja levyrivit muutetaan
  `DiskRowViewModel(Text, State)` -olioiksi. XAML värjää arvot
  `ThresholdState`→Brush-konvertterilla.
- `OverlayViewModel.WorstState` → OverlayWindow'n Border-reunus:
  Normal = huomaamaton (#33FFFFFF), Warning = #FFB74D, Critical = #EF5350
  (paksuus 2).
- `MainViewModel.Refresh()`: monitorin tulos → Dashboard-tilat, overlay-reunus,
  tapahtumat `EventLogService`en (WARNING/CRITICAL/INFO) ja debug-lokiin.

## Virheenkäsittely

- Puuttuva sensori (null) ei koskaan laukaise sääntöä; kesken jakson katoava
  sensori sulkee jakson (recovery-INFO jos tapahtuma ehdittiin nostaa).
- Events-kirjoitusvirhe ei kaada — sama try/catch-polku kuin historialla.
- Käyttäjän asettamat nurinkuriset rajat (warn > crit) toimivat silti:
  kumpikin raja arvioidaan itsenäisesti.

## Testaus

`ThresholdMonitor` keinotekoisella kellolla: (1) piikki alle keston → ei
tapahtumaa mutta tila muuttuu heti; (2) 30 s ylitys → täsmälleen yksi WARNING;
(3) eskalaatio CRITICALiin 10 s kriittisen ylityksen jälkeen; (4) palautuminen
→ INFO jossa kesto ja huippu, tila Normal; (5) cooldown estää toisen jakson
tapahtuman 5 min sisällä, sallii sen jälkeen; (6) tuuletinsääntö: pyörinyt
tuuletin 0 RPM + CPU 85 °C 10 s → CRITICAL; ei koskaan pyörinyt → ei mitään;
(7) levykohtainen tila ja Worst-kooste. Päästä päähän: rajat väliaikaisesti
alas settings.jsonissa (CPU-varoitus 30 °C) → värit + events-rivit oikeasti,
sitten rajat takaisin.

## Rajaus (YAGNI)

Ei työpöytäilmoituksia (Vaihe 8), ei throttling-tunnistusta (LHM ei tarjoa
suoraan), ei 12V-kiskosääntöä (ei KeyMetricsissä), ei asetussivua (Vaihe 8),
ei riskipisteytystä (Vaihe 6).
