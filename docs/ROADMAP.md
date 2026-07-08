# Kehityksen eteneminen (ROADMAP)

Perustuu määrittelyn lukuun 32 "Kehitysjärjestys". Tähän merkitään mitä on tehty
ja mitä tehdään seuraavaksi, jotta työtä on helppo jatkaa esimerkiksi illalla.

## ✅ Vaihe 1 — Perusprojekti + proof of concept (VALMIS)

- [x] C# WPF -ratkaisu (`HardwareMonitor.sln`)
- [x] Core-kirjasto + App erillään
- [x] LibreHardwareMonitorLib lisätty
- [x] Näkymä, joka listaa kaikki löydetyt sensorit puuna
- [x] Arvojen päivitys 1 s välein
- [x] Sensorit debug-lokiin
- [x] PowerShell-skriptit (`build.ps1`, `run.ps1`)

**Ensimmäinen onnistumisen mittari (luku 33):** ohjelma löytää CPU:n, GPU:n, levyt,
lämpötilat, kellot, kuormat ja tuulettimet niin hyvin kuin laitteisto sallii.

✅ **Testattu kotikoneella 8.7.2026** (i9-9900K, RTX 2060, ASUS Z390-F, Win 11):
kaikki löytyi — CPU-lämmöt/kellot/kuormat, GPU + hotspot + VRAM, NVMe/SATA-lämmöt
ja SMART, Nuvoton-tuulettimet RPM, Vcore, VRM/Chipset-lämmöt.

**Tärkeä havainto testistä:** Windows 11:n haavoittuvien ajurien estolista blokkaa
WinRing0:n (LibreHardwareMonitorLib 0.9.4) → CPU/emolevysensorit tyhjiä myös adminina.
Korjaus: kirjasto päivitetty **0.9.6**:een + asennettu **PawnIO-ajuri** (pawnio.eu).
Molemmat tarvitaan. Katso README "Vaatimukset".

## ✅ Vaihe 2 — Sensorien ryhmittely ja Dashboard (VALMIS 8.7.2026)

Raakalistasta selkeään näkymään (määrittelyn luvut 24–25):

- [x] `KeyMetricsService` (Core/Metrics): poimii tärkeimmät arvot — CPU
      käyttö/lämpö/kellot/teho, GPU käyttö/lämpö/hotspot/VRAM/teho, RAM %,
      levyjen lämmöt+aktiivisuus, tuulettimet (yksikkötestattu)
- [x] Dashboard-kortit (CPU / GPU / RAM / Levyt / Tuulettimet) omalla välilehdellä
- [x] "Kaikki sensorit" -välilehti säilyttää raakapuun
- [x] `HardwareMonitor.Tests`-projekti (xUnit, 11 testiä)

## ✅ Vaihe 2.5 — Työpöytäoverlay (VALMIS 8.7.2026)

Specissä `docs/superpowers/specs/2026-07-08-overlay-design.md`:

- [x] Läpi-klikattava always-on-top-overlay (WS_EX_TRANSPARENT + NOACTIVATE
      + TOOLWINDOW) — ei näy Alt+Tabissa eikä tehtäväpalkissa
- [x] Näyttää valitut rivit: CPU / GPU+VRAM / RAM / levylämmöt / tuulettimet
- [x] Asetukset: päällä/pois, kulma (4), läpinäkyvyys, rivivalinnat —
      tallentuvat `%LOCALAPPDATA%\HardwareMonitor\settings.json`
- [x] `SettingsService` (Core/Settings) — pohja Vaihe 4:n raja-arvoasetuksille

Rajattu tietoisesti ulos (jatkokehitysideat alla): FPS-mittaus, exclusive
fullscreen -pelituki, monen näytön valinta, raja-arvojen värikoodaus.

## ✅ Vaihe 2.6 — Nimilaput, tray, ikoni, autostart (VALMIS 8.7.2026)

Specissä `docs/superpowers/specs/2026-07-08-fan-labels-tray-icon-design.md`:

- [x] Tuulettimien nimilaput: kaksoisklikkaus Tuulettimet-kortissa, tallennus
      sensorin pysyvällä tunnisteella settings.jsoniin, näkyy myös overlayssa
- [x] Tray-pienennys: pienennä/sulje → ilmaisinalue, mittaus jatkuu taustalla;
      tray-valikko Näytä / Overlay / Lopeta
- [x] Korjaus: overlay ei enää katoa pääikkunan mukana (Owner-kytkös poistettu)
- [x] Vektori-ikoni (Assets/icon.svg + tools/generate-icon.ps1 → app.ico):
      mittarikaari + sykeviiva, exe + tray
- [x] Automaattikäynnistys Windowsin mukana (Task Scheduler /RL HIGHEST —
      käynnistyy adminina ilman UAC-kyselyä)

## ✅ Vaihe 3 — Lokitus (SQLite) (VALMIS 8.7.2026)

Specissä `docs/superpowers/specs/2026-07-08-sqlite-logging-design.md`:

- [x] SQLite-kanta `%LOCALAPPDATA%\HardwareMonitor\data\history.db` (WAL,
      Microsoft.Data.Sqlite — Coren ensimmäinen NuGet-riippuvuus)
- [x] **Koosterivit 5 s välein**: min/avg/max 1 s -lukemista (`SampleAggregator`) —
      sekunnin piikit eivät katoa, kanta pysyy viidesosassa. Väli asetuksissa
      (`Logging.SensorIntervalSeconds`)
- [x] Taulut: samples + disk_samples + fan_samples (cascade) + events
- [x] Tapahtumaloki (`EventLogService`, luku 15 kentät) — nyt sovelluksen
      elinkaari; Vaihe 4 lisää raja-arvotapahtumat samaan tauluun
- [x] Retention 30 vrk (`Logging.KeepHistoryDays`), siivous käynnistyksessä
- [x] Statusrivillä istunnon lokirivien määrä

## ✅ Vaihe 4 — Raja-arvot ja varoitukset (VALMIS 8.7.2026)

Specissä `docs/superpowers/specs/2026-07-08-thresholds-design.md`:

- [x] Oletusrajat (luku 16) `Thresholds`-asetusosioon (luvun 29 nimet);
      muokkaus settings.jsonista, asetussivu tulee Vaihe 8:ssa
- [x] Kesto huomioon: väritila UI:hin heti, tapahtuma vasta yhtäjaksoisen
      ylityksen jälkeen (WARNING 30 s, CRITICAL 10 s); palautuessa INFO
      kestolla ja huippuarvolla; cooldown 5 min (luku 26)
- [x] Varoitus/kriittinen-tilat UI:hin: korttiarvot ja levyrivit värjätään
      (vihreä/oranssi/punainen), overlayn reunus pahimman tilan mukaan
- [x] Tuuletinsääntö: istunnossa pyörinyt tuuletin 0 RPM + CPU ≥ 80 °C → CRITICAL
- [x] Tapahtumat events-tauluun (EventLogService) ja debug-lokiin
- [x] Todennettu päästä päähän lasketulla rajalla (WARNING-tapahtuma,
      oranssi korttiarvo ja overlay-reunus)

## ⏭️ Vaihe 5 — Windows Event Log

- [ ] Kernel-Power 41, WHEA-Logger, näyttöajurivirheet, levy/NTFS/NVMe, BugCheck
      (`System.Diagnostics.Eventing.Reader`, luvut 18 ja 32)

## ⏭️ Vaihe 6 — Riskianalyysi

- [ ] Pisteytys ja selkokielinen yhteenveto (luvut 19 ja 31)
- [ ] "Ennen kaatumista" -puskuri + `last_state.json` (luku 17)
- [ ] **Konetuntemus-loki** (käyttäjän idea 8.7.2026): jatkuvasti päivittyvä
      `machine-insights.md`, johon analyysi kirjaa havainnot ja opit koneesta —
      piikit (mikä sensori, kuinka korkea, kuinka kauan, mihin kellonaikaan),
      toistuvat kuviot (esim. "NVMe-ohjainlämpö ylittää 80 °C pitkissä
      kirjoituksissa"), normaalitasot per sensori ja konkreettiset
      optimointiehdotukset. Tiedosto on sekä ihmisen että tekoälyn (Claude)
      luettavissa: tulevissa istunnoissa Claude voi lukea sen ja auttaa
      optimoinnissa suoraan datan pohjalta. Pohjautuu Vaihe 3:n
      SQLite-historiaan ja tapahtumalokiin.

## ⏭️ Vaihe 7 — Raportointi

- [ ] "Luo raportti" Markdown/TXT (luku 20), CSV-vienti

## ⏭️ Vaihe 8 — Viimeistely

- [ ] Ilmoitukset, asetussivu, graafit (LiveCharts2) — tray icon ja autostart
      tehtiin jo Vaihe 2.6:ssa
- [ ] **Kielituki fi/en**: UI-tekstit resx-resursseihin, kielivalinta asetuksiin
      (nyt kaikki tekstit kovakoodattua suomea)
- [ ] **Julkaisukuntoon**: LICENSE-tiedosto (oma koodi esim. MIT; LibreHardwareMonitorLib
      on MPL-2.0 — mainittava READMEssä), englanninkielinen README-osio, repo julkiseksi
- [ ] Paketointi/asennin (self-contained julkaisu, autostart-polun päivitys
      asennettuun sijaintiin)

---

## Tekninen pino (luku 22)

| Osa | Valinta |
|---|---|
| Kieli | C# |
| UI | WPF (net8.0-windows) |
| Sensorit | LibreHardwareMonitorLib 0.9.6 + PawnIO-ajuri |
| Event Log | System.Diagnostics.Eventing.Reader |
| Tietokanta | SQLite (tulossa vaiheessa 3) |
| Graafit | LiveCharts2 (tulossa vaiheessa 8) |

## MVP:n ulkopuolella (luku 24)

Tuulettimien ohjaus, RGB, ylikellotus, jännitteiden muuttaminen, automaattinen säätö,
pilvisynkronointi. **Ensimmäinen versio vain lukee, näyttää, lokittaa ja analysoi.**

## Jatkokehitysideat (luku 27 + overlay-spec)

- FPS-mittaus overlayhin (PresentMon/ETW) — oma työvaiheensa
- Overlay exclusive fullscreen -pelien päälle (vaatisi DirectX-injektion)
- Monen näytön valinta overlayn sijainnille
- Raja-arvojen värikoodaus overlayhin ja Dashboardiin (Vaihe 4:n yhteydessä)
- Tuulettimien nimeäminen käyttäjän omilla nimillä (esim. "Fan #2" → "AIO-pumppu"),
  koska LibreHardwareMonitor ei tunne tämän emolevyn kanavajärjestystä nimeltä
