# Design: Vaihe 3 — SQLite-lokitus (sensorihistoria + tapahtumaloki)

Päivätty: 8.7.2026. Hyväksytty käyttäjän kanssa. Perustuu määrittelyn lukuihin
13–15, 21, 29–30 ja ROADMAPin Vaihe 3:een.

## Käyttäjän kanssa lukitut päätökset

| Kysymys | Päätös |
|---|---|
| Lokitusväli | **5 s koosterivit 1 s -lukemista**: rivi sisältää min/avg/max per mittari, joten sekunnin piikit eivät koskaan katoa mutta kanta pysyy viidesosassa (~17 000 riviä/vrk). Väli asetuksissa (`SensorIntervalSeconds`). |
| Tallennus | SQLite (`Microsoft.Data.Sqlite`, projektin ensimmäinen uusi NuGet — määrittelyn luku 21 valitsi SQLiten), WAL-tila. |
| Historia | 30 vrk (`KeepHistoryDays`), siivous käynnistyksessä. |
| Tapahtumat | Samaan kantaan `events`-tauluun (luku 15 kentät). JSONL/CSV-vienti tulee Vaihe 7:ssä. |

## Arkkitehtuuri — kolme yksikköä Coreen

### 1. `SampleAggregator` (Core/Storage) — puhdas, testattava

- `AggregatedSample? Add(KeyMetrics m, DateTimeOffset now)` — kutsutaan kerran
  sekunnissa; palauttaa koosteen joka N:nnellä kutsulla (N = `SensorIntervalSeconds`),
  muuten null. Ei tiedä tietokannasta mitään.
- Kooste: `MetricAggregate(Min, Avg, Max)` mittareille CPU load/temp/power,
  GPU load/temp/hotspot/power, VRAM used, RAM load/used; CPU-kellosta vain max.
  Levyt (`DiskAggregate`: nimi + indeksi, lämpö min/avg/max, aktiivisuus max;
  täsmäys indeksillä+nimellä) ja tuulettimet (`FanAggregate`: tunniste + nimi,
  RPM min/avg/max; täsmäys tunnisteella).
- Null-käsittely: koosteeseen lasketaan vain ei-null-lukemat; jos kaikki null,
  koko aggregaatti on null-arvoinen. Aikaleima = koosteen valmistumishetki.

### 2. `HistoryDb` (Core/Storage) — SQLite-yhteys ja skeema

- Tiedosto `%LOCALAPPDATA%\HardwareMonitor\data\history.db` (hakemisto luodaan);
  konstruktori saa hakemiston parametrina testejä varten (sama malli kuin
  SettingsService/DebugLogger).
- Avauksessa: `PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;` + skeeman luonti
  (`CREATE TABLE IF NOT EXISTS`). Yksi pitkäikäinen yhteys + lukko (kirjoitukset
  sarjallistetaan; kutsuja voi tulla taustasäikeestä).
- Taulut:
  - `samples(id PK, ts INTEGER unix-UTC, cpu_load_avg/max, cpu_temp_avg/max,
    cpu_clock_max, cpu_power_avg/max, gpu_load_avg/max, gpu_temp_avg/max,
    gpu_hotspot_avg/max, gpu_power_avg/max, vram_used_mb_avg/max,
    ram_load_avg/max, ram_used_gb_avg/max)` + indeksi `ts`:lle. REAL-sarakkeet
    NULL-sallivia (puuttuva sensori ei ole virhe).
  - `disk_samples(sample_id FK CASCADE, disk_index, name, temp_avg, temp_max, activity_max)`
  - `fan_samples(sample_id FK CASCADE, identifier, name, rpm_avg, rpm_max)`
  - `events(id PK, ts INTEGER unix-UTC, level TEXT, component TEXT, sensor TEXT NULL,
    value REAL NULL, threshold REAL NULL, message TEXT)`
- Metodit: `InsertSample(AggregatedSample)`, `PurgeOlderThan(DateTimeOffset)`
  (samples cascade + events), `CountSamples()`, `InsertEvent(...)`,
  `ReadRecentEvents(int limit)`.

### 3. `EventLogService` (Core/Storage) — ohut kerros HistoryDb:n päälle

- `Info/Warning/Critical/Error(component, message, sensor?, value?, threshold?)`.
- Vaihe 3 kirjaa: INFO "Sovellus käynnistyi" ja INFO "Sovellus suljettiin siististi".
  Vaihe 4 lisää raja-arvotapahtumat, Vaihe 6 yllättävän sammutuksen tunnistuksen.

## Kytkentä Appiin (MainViewModel)

- `Start()`: avaa HistoryDb (try/catch → jos epäonnistuu, kirjaus debug-lokiin ja
  jatketaan ilman historiaa), `PurgeOlderThan(now - KeepHistoryDays)`,
  INFO-tapahtuma käynnistymisestä.
- `Refresh()`: `_aggregator.Add(metrics, DateTimeOffset.Now)` — kun kooste valmistuu,
  kirjoitus `Task.Run`issa (UI ei töki levyhikan takia); poikkeus → debug-loki,
  lokitus jatkuu seuraavasta koosteesta. Lokitus jatkuu myös trayssa
  (DispatcherTimer ei riipu ikkunan näkyvyydestä).
- `Dispose()`: INFO siististä sammutuksesta, HistoryDb dispose.
- Alapalkin statusriville lisätään tallennettujen koosterivien määrä tältä
  istunnolta ("lokirivejä: N") — käyttäjä näkee lokituksen elävän ja se on
  todennettavissa ruutukaappauksesta.
- `AppSettings.Logging`: `SensorIntervalSeconds = 5`, `KeepHistoryDays = 30`
  (määrittelyn luku 29:n mukaiset nimet).

## Virheenkäsittely

- Kannan avaus epäonnistuu (lukko, levy täynnä, oikeudet) → debug-lokiin,
  sovellus toimii normaalisti ilman historiaa.
- Yksittäinen kirjoitus epäonnistuu → debug-lokiin, ei kaada; seuraava kooste
  yrittää normaalisti.
- Vioittunut kanta: SQLite heittää avauksessa → sama fallback kuin yllä.
  (Automaattinen uudelleenluonti kirjataan jatkokehitykseen, ei v1:een.)

## Testaus (xUnit, väliaikaishakemistot kuten SettingsServiceTestsissä)

- `SampleAggregator`: palauttaa null neljällä ensimmäisellä, koosteen viidennellä;
  min/avg/max oikein; null-lukemat ohitetaan; kaikki null → null; levyt täsmätään
  indeksillä, tuulettimet tunnisteella; laskuri nollautuu koosteen jälkeen.
- `HistoryDb`: InsertSample + CountSamples; PurgeOlderThan poistaa vanhat muttei
  uusia (myös cascade lapsiriveihin); InsertEvent + ReadRecentEvents järjestyksessä.
- `EventLogService`: Info-kutsu päätyy events-tauluun oikealla tasolla.
- Ajonaikainen todennus: sovellus käyntiin, odotus ~15 s, statusrivin
  "lokirivejä" kasvaa ja tiedosto history.db syntyy.

## Rajaus (YAGNI)

- Ei UI-sivua lokien selaamiseen (Vaihe 8), ei vientiä (Vaihe 7), ei raja-arvoja
  (Vaihe 4), ei last_state.jsonia (Vaihe 6), ei konetuntemus-lokia (Vaihe 6).
