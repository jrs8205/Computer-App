# Ulkopuolisen bugitarkastuksen triage (12.7.2026)

Tarkastaja ajettiin REVIEW-BRIEF.md:n kanssa 12.7.2026. Kaikki löydökset
todennettiin koodista; 24/26 vahvistui. Korjaukset jaettiin koreihin.
**Korit A ja B on korjattu ja todennettu ajossa 12.7.2026** — jäljellä
on vain Kori C (graafien laatu ja pikkuviat).

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

## Kori B — korjattu 12.7.2026 ✅

1. **Autostart-korotus**: /RL HIGHEST vain ACL-suojatusta polusta
   (ProtectedPaths.IsUnderAny: Program Files / x86 / Windows); muuten
   tehtävä luodaan rajoitettuna ja debug-lokiin kirjataan selitys.
   Lisäksi tools/install.ps1 asentaa sovelluksen Program Filesiin
   (self-contained publish, autostart-tehtävän päivitys, Käynnistä-valikon
   pikakuvake admin-lipulla) — asennettuna korotus toimii turvallisesti.
2. **GPU-laitevalinta**: KeyMetricsService valitsee yhden ensisijaisen
   GPU-ryhmän (Nvidia/AMD ennen Inteliä, sitten sensorimäärä) ja poimii
   kaikki kentät siitä — ei enää sekoitusta hybridikoneissa.
3. **Kirjanmerkin nollaus**: IWindowsEventSource.ReadNewestRecordId;
   jos lokin uusin RecordID < kirjanmerkki (loki tyhjennetty), luku
   alkaa alusta.
4. **Atominen kirjoitus**: HistoryDb.InsertEventsWithMeta — tapahtumat ja
   kirjanmerkki samassa transaktiossa, ei duplikaatteja keskeytyksissä.
5. **GPU-fanit pois CPU-säännöstä**: identifier joka sisältää "gpu" ei
   laukaise pysähtyi-kuumana-sääntöä (semi-passive on normaalia).
6. **NaN hylätään**: SettingsValidator vaatii float.IsFinite.
7. **Kriittisen tilan tekstiväri**: tekstinä #FF9E9E (7,8:1 taustalla
   #252526, WCAG AAA), reunuksissa edelleen #EF5350 (ei-teksti).
8. **Insights ilman sensoridataa**: vain taso- ja trendiosiot ehdollistetaan
   SampleCountilla — tapahtumaosiot kirjoitetaan aina.
9. **SensorType_Timing** lisätty molempiin UiStrings-resursseihin
   (fi "Ajoitus", en "Timing").

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
