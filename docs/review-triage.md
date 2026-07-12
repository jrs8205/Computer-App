# Ulkopuolisen bugitarkastuksen triage (12.7.2026)

Tarkastaja ajettiin REVIEW-BRIEF.md:n kanssa 12.7.2026. Kaikki löydökset
todennettiin koodista; 24/26 vahvistui. Korjaukset jaettiin koreihin.
**Kori A on korjattu ja todennettu ajossa 12.7.2026** — tämä tiedosto
seuraa jäljellä olevia koreja B ja C.

## Kori A — korjattu 12.7.2026 ✅

1. DebugLogger: eräkirjoitus + 20 MB kierto (debug.old.log); snapshot 60 s välein.
2. HistoryDb: min-sarakkeet skeemaan + ALTER TABLE -migraatio; minit talteen.
3. HistoryDb.ReadSampleRowsDownsampled: 30 pv graafidata harvennetaan SQL:ssä
   (ennen ~518 000 riviä muistiin lukon alla).
4. LastStateService: shutdown-tarkistus ja tallennus saman lukon alle (race pois).
5. CsvExporter: samannimiset levyt omiin sarakkeisiin #1/#2 (kaksi 860 EVO:ta).
6. MainViewModel.Dispose: MarkCleanShutdown erillisessä tryssä — siisti
   sulkeminen merkitään vaikka tapahtumakirjoitus epäonnistuisi.
7. App: single instance (mutex + näyttöpyyntö-event olemassa olevalle ikkunalle).
8. Overlay: itsekorjaus — ulkopuolinen WM_CLOSE havaitaan Closed-käsittelijässä,
   ikkuna luodaan uudelleen ja lokiin kirjataan WARNING (havainto 11.7.2026).

## Kori B — ennen julkaisua (avoinna)

- **[P1] Autostart-korotus kirjoitettavasta polusta** (AutostartService.cs):
  /RL HIGHEST + exe Downloads-kansiossa = UAC-ohitus exen vaihtamalla.
  Korjaus kytkeytyy paketointiin (asennus ACL-suojattuun polkuun); välivaihe:
  SetEnabled kieltäytyy korotuksesta jos polku on käyttäjän kirjoitettavissa.
- **[P1] GPU-kenttien sekoittuminen hybridikoneissa** (KeyMetricsService.cs):
  ??= poimii kentät eri GPU-ryhmistä — valitse ensin yksi ensisijainen GPU.
- **[P1] Windows-lokin kirjanmerkki jumiutuu lokin tyhjennyksessä**
  (SystemEventReader/Collector): jos lokin uusin RecordID < kirjanmerkki,
  nollaa kirjanmerkki.
- **[P2] Collector: tapahtumat + SetMeta samaan transaktioon** (duplikaatit
  jos skannaus keskeytyy).
- **[P2] CPU-fanisääntö laukeaa GPU:n puolipassiivifaneista** (ThresholdMonitor):
  rajaa GPU-tunnisteet (identifier alkaa /gpu) CPU-jäähdytyssäännön ulkopuolelle.
- **[P2] float.TryParse hyväksyy NaN:n** (SettingsValidator): vaadi
  float.IsFinite(value).
- **[P2] Kriittisen tilan tekstiväri #EF5350 on 4,4:1 korttitaustalla #252526**
  (ThresholdStateToBrushConverter): tekstikäyttöön vaaleampi punainen
  (huom. #FF8A80:kin on vain 6,7:1 tällä taustalla — tarvitaan esim. #FFABA3-
  tasoinen; reunuksiin nykyinen käy, ei-teksti). WCAG AAA ≥ 7:1.
- **[P2] Insights ohittaa tapahtumaosiot kun SampleCount == 0**
  (MachineInsightsBuilder): Windows-tapahtumia voi olla ilman sensoridataa —
  ehdollista vain taso- ja trendiosiot.
- **[P2] SensorType_Timing-avain puuttuu** molemmista UiStrings-resursseista
  (LHM 0.9.6: DIMM-sensorit).

## Kori C — graafien laatu ja pikkuviat (avoinna)

- Aukot graafeihin sammutusjaksojen kohdalle (ChartHistoryBuilder: aikaleimaväli
  > esim. 3 × odotettu → null-katkospiste).
- Tuulettimen 5 % -näkyvyysraja lasketaan bucket-keskiarvoista — laske
  raakariveistä ennen harvennusta.
- Aikavälin vaihto haun aikana hukkuu (MainViewModel.RefreshHistoryInBackground:
  merkitse odottavaksi tai vertaa RangeHours soveltaessa).
- DST-siirtymä rikkoo X-akselin monotonisuuden (HistoryViewModel: UTC-pisteet,
  paikallisaika vain akselin labelerissa).
- Harvennuksen päätepisteet: ensimmäisen/viimeisen pisteen arvo on bucketin
  keskiarvo vaikka aikaleima on päätepisteen.
- Levykoosteet täsmätään vain indeksillä (SampleAggregator) — hotplug kesken
  5 s jakson sekoittaa; täsmää nimi+esiintymä.
- Kadonneet sensorit jäävät sensoripuuhun ikuisesti (MainViewModel.UpdateValues:
  havaitse myös puuttuvat tunnisteet → BuildTree).
- Raportin yhteenveto voi olla ~1 min vanhempi kuin sen tapahtumalista
  (BuildReport: laske RiskAnalyzer.Assess uudelleen tuoreista tiedoista).
- Integer-kentät näyttävät 5,6 mutta tallentavat 6 (NumericFieldViewModel:
  näytä toteutunut arvo tai vaadi kokonaisluku).
- Overlayn LineHeight 22 fonttikoolla > 22 — todennäköisesti väärä positiivinen
  (LineStackingStrategy-oletus MaxHeight kasvattaa rivin sisällön mukaan);
  todenna fontilla 32, halutessa sido LineHeight fonttikokoon.

## Väärät positiiviset / nuanssit

- Overlay-LineHeight (yllä) — todennettava, luultavasti harmiton.
- GPU-sekoitus ei vaikuta referenssikoneeseen (vain yksi GPU) — julkaisua varten.
