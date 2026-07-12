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

# Toinen katselmointi (12.7.2026, v1.0.0:n jälkeen) — korjattu v1.0.1 ✅

Kaikki 12 löydöstä vahvistuivat ja korjattiin:

1. **[P1] Suojatun polun ACL**: pelkkä polkuetuliite ei riitä (C:\Windows\
   Temp on käyttäjäkirjoitettava) → Windows-juuri pois sallituista,
   ProtectedPaths.HasNonAdminWriteAccess tarkistaa hakemiston ja exen
   todelliset käyttöoikeudet (Everyone/Users/Authenticated Users/
   INTERACTIVE + kirjoitusoikeudet; InheritOnly-ACE:t ohitetaan).
2. **[P1] Kolmansien osapuolten lisenssit**: LICENSE.txt +
   THIRD-PARTY-NOTICES.md (MPL-2.0/MIT/Apache-2.0 -tekstit) kopioituvat
   publishiin csprojista ja asentuvat sovelluksen mukana.
3. **[P2] Rajoitettu tehtävä + requireAdministrator**: suojaamattomasta
   polusta autostartia ei kytketä lainkaan (rajoitettu tehtävä ei voisi
   käynnistää admin-manifestista exeä) — SetEnabled palauttaa false + loki.
4. **[P2] Näyttöpyynnön race**: event luodaan heti mutexin jälkeen ja
   signalointi käyttää luo-tai-avaa-semantiikkaa; kuuntelija käynnistyy
   vasta MainWindow-asetuksen jälkeen (AutoReset säilyttää signaalin).
5. **[P2] Levyjen pysyvä tunniste**: HardwareGroup/DiskMetrics kantavat
   LHM:n laitetunnisteen; SampleAggregator täsmää sillä (nimi+esiintymä
   vain varapolkuna).
6. **[P2] Aidot päätepisteet**: ReadSampleRowsDownsampled palauttaa
   alueen ensimmäisen ja viimeisen RAAKArivin bucket-koosteiden lisäksi.
7. **[P2] Päätepisteen null säilyy**: builderin endpoint-arvo on aina
   päätepisterivin arvo — myös null (ei keksittyä bucket-keskiarvoa).
8. **[P2] Lyhyet aukot**: puuttuvat bucketit tuottavat all-null-rivin
   SQL-tasolla (bucket-indekseistä) — viiva katkeaa luotettavasti myös
   1–2 bucketin aukoissa, joita aikaleimaheuristiikka ei erota.
9. **[P2] Vanhentunut aikaväli**: tulos sovelletaan vain jos RangeHours
   on yhä sama (generation check) + uusintahaku finallyssä.
10. **[P2] Lokin sukupolvi**: System-lokin luontiaika (CreationTime)
    talletetaan metaan; muutos → kirjanmerkki nollataan, vaikka uusi loki
    olisi kasvanut vanhan kirjanmerkin ohi.
11. **[P2] Ei "normaalitasolla" ilman näytteitä**: AllGood-päätelmä vain
    kun SampleCount > 0; muuten havainto-osio jää pois.
12. **[P2] publish-siivous**: install.ps1 tyhjentää publish-hakemiston
    ennen dotnet publishia (vanhat DLL:t eivät jää pakettiin).

# Kolmas katselmointi (12.7.2026, v1.0.2:n jälkeen) — korjattu v1.0.3 ✅

Kaikki 16 löydöstä vahvistuivat ja korjattiin (206 testiä). P1:t:

1. **[P1] ProtectedPaths ACL vain 4 SID:iä**: pelkkä neljän yleisen SID:n
   esto ei tunnistanut suoraa kirjoitusoikeutta käyttäjän omalle SID:lle
   tai mukautetulle ryhmälle → allowlist-periaate: vain admin/SYSTEM/
   TrustedInstaller-kirjoitus sallitaan, kaikki muu tekee polusta turvattoman.
2. **[P1] Vaarallista autostart-tehtävää ei poistettu**: RefreshIfEnabled
   lukee nyt olemassa olevan tehtävän kohdepolun (schtasks /XML) ja poistaa
   tehtävän, jos se osoittaa suojaamattomaan polkuun (vanha versio tai
   muuttunut ACL). Todennettu: Downloads-tehtävä poistettiin + luotiin
   uudelleen Program Filesiin.
3. **[P1] Lokisukupolvi eri transaktiossa**: SetMeta(sukupolvi) tapahtui
   ennen tapahtumia ja palasi ne 0-tapahtumatilanteessa → sukupolvi,
   tapahtumat ja bookmark kirjoitetaan nyt YHTEEN transaktioon
   (InsertEventsWithMeta ottaa monta meta-avainta); 0 tapahtumaa + sukupolven
   muutos → bookmark nollataan ja sukupolvi tallennetaan yhdessä.

P2:t:

4. Single-instance-event luodaan ennen mutex-kilpailua ja pidetään kentässä
   elossa kaikilla instansseilla (objekti ei katoa signaloinnin ja
   sulkemisen välissä). Todennettu: 2. käynnistys näytti 1. ikkunan.
5. AI-raportin rakennus taustasäikeessä (raskaat DB-kyselyt pois UI:lta) +
   koko rakennus lokalisoidun try/catchin sisällä; kopio/tallennus ei
   karkaa WPF-tapahtumasta. Todennettu: kopio 3693 merkkiä.
6. Atominen tiedostonkirjoitus (AtomicFile: temp + File.Move): machine-
   insights.md ja last_state.json eivät korruptoidu kesken kirjoituksen.
7. Graafit: SQL-tulosta ei harvenneta uudelleen (maxPoints ≥ rivimäärä) —
   päätepisteet ja null-katkokset säilyvät.
8. Tuulettimen 5 % -näkyvyysraja painotetaan raakarivien lukumäärillä
   (SpinningRows/KnownRows), ei bucket-osuuksien keskiarvolla.
9. Tuulettimet ryhmitellään pysyvällä tunnisteella (ei nimellä) kaikissa
   poluissa (stats/downsample/CSV/graafit) — samannimiset eivät sulaudu.
10. HistoryDb.Dispose sarjallistetaan _lockin alle (+ _disposed-lippu) —
    yhteyttä ei suljeta aktiivisen taustatehtävän alta.
11. MachineSpecReader käyttää samaa ensisijaisen GPU:n valintaa
    (GpuSelector) kuin mittaukset — raportin GPU-nimi vastaa mittauksia.
12. SettingsService täydentää nulliksi deserialisoituneet sisäoliot
    oletuksilla ({"Logging":null} ei enää kaada käynnistystä).
13. SaveAndOpen erottaa kirjoitus- ja avausvirheen (tiedosto tallentui
    muttei auennut ei enää valehtele "tallennus epäonnistui").
14. setup.iss-resepti dokumentoi publish-kansion tyhjennyksen ennen
    julkaisua (vanhat DLL:t eivät jää installeriin).
