# Vaihe 6 — Riskianalyysi, last_state.json ja konetuntemus-loki (design)

Päivä: 9.7.2026. Perustuu määrittelyn lukuihin 17, 19 ja 31 sekä käyttäjän
UI-toiveeseen 9.7.2026: väritilat ja arvot selitteineen näkyviin sovellukseen,
ja overlayn reunus vihreäksi aina kun kaikki on kunnossa.

## Osat

### 1. RiskAnalyzer (Core/Analysis, puhdas logiikka, TDD)

Sisään: nykyiset mittaritilat + arvot (MetricStates + KeyMetrics),
viimeisen 24 h tapahtumat (events-taulusta), 24 h maksimit (samples-taulusta)
ja tieto kaatuiko edellinen istunto.

Ulos `RiskAssessment`: kokonaistila (Hyvä/Varoitus/Kriittinen), riskitaso
(Matala/Kohonnut/Korkea), pistemäärä, selkokieliset havainnot (luku 19:n
esimerkkien tyyliin) ja suositus kun tila ei ole hyvä.

Pisteytys (luku 19 "riskipisteiden laskeminen"):

| Lähde | Pisteet |
|---|---|
| Mittari varoitustilassa nyt | 3 / mittari |
| Mittari kriittisessä tilassa nyt | 8 / mittari |
| Raja-arvotapahtuma 24 h (WARNING / CRITICAL) | 2 / 6 |
| WHEA 24 h (korjattu / vakava) | 6 / 15 |
| Kernel-Power 41 tai BSOD 24 h | 15 |
| Odottamaton sammutus (EventLog 6008) 24 h | 6 |
| GPU-ajurivirhe 24 h | 6 |
| Windowsin levyvirhe 24 h | 10 |
| Edellinen istunto päättyi yllättäen | 10 |

Tasot: 0–4 = Hyvä/Matala, 5–14 = Varoitus/Kohonnut, ≥15 = Kriittinen/Korkea.
Suositus valitaan suurimman pistelähteen mukaan (lämpö → jäähdytys,
WHEA → rauta/kellotus, levy → varmuuskopiot + SMART, kaatuminen → yleisohje).

### 2. LastStateService (Core/Analysis, TDD) — luku 17

- `last_state.json` (%LOCALAPPDATA%\HardwareMonitor\data\): viimeisin tila
  (CPU/GPU/hotspot/RAM/levyt) + aikaleima + `cleanShutdown`-lippu.
- Kirjoitus ~5 s välein ajon aikana lipulla `false`; siisti sulkeminen
  kirjoittaa `true`. Käynnistyksessä luetaan edellinen: jos lippu on `false`,
  edellinen istunto päättyi yllättäen → WARNING-tapahtuma events-tauluun
  viimeisimmillä arvoilla + havainto analyysiin.
- "Viimeiset 10 min sensoridataa" on jo SQLite-historiassa (5 s koosterivit,
  WAL committaa jatkuvasti) — erillistä puskuria ei tarvita.

### 3. MachineInsightsWriter (Core/Insights, TDD) — käyttäjän idea

- Generoi `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md`:
  normaalitasot (30 pv keskiarvot), huiput, tapahtumamäärät, havaitut
  kuviot ja konkreettiset optimointiehdotukset. Sekä ihmisen että Claude-
  istuntojen luettavissa.
- Markdownin rakentaminen on puhdas funktio (testataan sisältö);
  kirjoitus käynnistyksessä + 30 min välein taustalla.

### 4. HistoryDb-laajennukset (TDD)

- `ReadEventsSince(ts)` — tapahtumat analyysille.
- `GetSampleStats(ts)` — avg/max-koosteet samples/disk_samples/fan_samples-
  tauluista (CPU-lämpö, GPU, hotspot, RAM, levyt nimittäin, tuulettimet).

### 5. UI (App)

- **Dashboardin tilapaneeli** (ylin rivi): värillinen tilapiste +
  "Koneen tila: X · Riskitaso: Y", alla havainnot arvoineen ja rajoineen
  ("CPU-lämpötila 87 °C ylittää varoitusrajan (85 °C)") ja **väriselite**:
  vihreä = kunnossa · oranssi = varoitusraja ylittynyt · punainen =
  kriittinen raja ylittynyt.
- **Overlayn reunus aina näkyvissä**: vihreä kun kaikki kunnossa (ennen
  läpinäkyvä), oranssi/punainen kuten ennen, syaani siirtotilassa.
- Analyysi lasketaan 5 s välein nykytiloista; tapahtuma- ja
  historiakoosteet päivitetään taustalla 60 s välein.

## Rajaukset

- Ilmoitukset (toast) ja raportit kuuluvat Vaihe 7/8:aan.
- Asetussivu rajoille tulee Vaihe 8:ssa; rajat näkyvät nyt selitteissä.
