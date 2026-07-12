# Ulkopuolisen bugitarkastuksen triage (12.7.2026)

Tarkastaja ajettiin REVIEW-BRIEF.md:n kanssa 12.7.2026. Kaikki löydökset
todennettiin koodista; 24/26 vahvistui. Korjaukset jaettiin koreihin.
**Kaikki kolme koria (A, B, C) on korjattu ja todennettu 12.7.2026.**

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

## Kori C — korjattu 12.7.2026 ✅

1. Aukot graafeihin sammutusjaksojen kohdalle: aikaleimaväli > 3 × mediaani
   → null-katkospiste kaikkiin sarjoihin (ChartHistoryBuilder.GapsAfter).
2. Tuulettimen 5 % -näkyvyysraja lasketaan 5 s riveistä: SQL laskee
   bucketin SpinShare-osuuden (FanSampleValue.SpinShare), builder summaa.
3. Harvennuksen päätepisteet säilyttävät datan päätepisteiden ARVOT
   (aikaleimojen lisäksi); välipisteet ovat yhä bucket-keskiarvoja.
4. Levykoosteet täsmätään nimi + esiintymä -avaimella (SampleAggregator) —
   hotplug kesken jakson ei sekoita eri levyjen lukemia.
5. Aikavälin vaihto haun aikana: hylätty pyyntö haetaan uudelleen heti
   edellisen valmistuttua (RefreshHistoryInBackground finally).
6. DST: graafipisteet UTC:nä, paikallisaika vain akselin labelerissa —
   X-akseli pysyy monotonisena syksyn siirtymässä.
7. Kadonneet sensorit: puu rakennetaan uudelleen myös kun indeksoitu
   sensori puuttuu luennasta (irrotus, sleep/resume).
8. Raportin riski lasketaan samasta tuoreesta datasta kuin raportin
   tapahtumalista (BuildReport kutsuu RiskAnalyzer.Assess itse).
9. Integer-kentät: normalize-pyöristys ennen tallennusta ja näyttöä —
   kenttä näyttää saman arvon jonka sovellus käyttää.
10. Overlayn riviväli sidottu fonttikokoon (LineHeight = FontSize × 1,4) —
    alkuperäinen löydös oli todennäköisesti väärä positiivinen
    (LineStackingStrategy-oletus MaxHeight), mutta sidonta on robustimpi.

## Nuanssit

- GPU-sekoitus (B2) ei vaikuttanut referenssikoneeseen (vain yksi GPU) —
  korjattiin julkaisua varten.

## Julkaisu

v1.0.0 julkaistu 12.7.2026: GPL-3.0, README uudistettu, requireAdministrator,
Inno Setup -installeri (installer/setup.iss) GitHub-releasessa.
