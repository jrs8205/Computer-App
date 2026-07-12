# HANDOFF — 12.7.2026 istunto

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/review-triage.md` (tarkastuslöydösten tilanne) ja
`docs/ROADMAP.md`.

## Tilanne yhdellä lauseella

**Ulkopuolinen bugitarkastus on ajettu ja triagettu (24/26 vahvistui), ja
Kori A — 8 datan oikeellisuuteen ja vakauteen vaikuttavaa korjausta — on
toteutettu TDD:llä, todennettu ajossa ja pushattu**; seuraavaksi Kori B
(julkaisua blokkaavat) ja Kori C (graafien laatu), sitten LICENSE + release.

## Tehty tässä istunnossa (12.7.2026)

1. **Tarkastuslöydösten triage**: kaikki 26 kohtaa todennettiin koodista.
   Tulokset ja jäljellä olevat työt: `docs/review-triage.md`.
2. **Kori A korjattu** (166 testiä vihreänä, +11 uutta):
   - DebugLogger: eräkirjoitus + 20 MB kierto (loki oli kasvanut 149 MB:iin;
     rivi kerrallaan -kirjoitus venytti mittausväliä 1 s → ~15 s!).
     Snapshot nyt 60 s välein yhtenä appendina.
   - HistoryDb: min-sarakkeet + migraatio (invariantti 6 toteutuu vihdoin);
     `ReadSampleRowsDownsampled` harventaa graafidatan SQL:ssä (30 pv toi
     ennen ~518 000 riviä muistiin HistoryDb-lukon alla → UI saattoi jäätyä).
   - LastStateService-race + Dispose-järjestys: väärät "päättyi yllättäen"
     -tapahtumat kuriin (kaksi erillistä vikaa samassa ketjussa).
   - CsvExporter: kaksi samannimistä 860 EVO:ta eivät enää katoa (#1/#2).
   - Single instance: mutex + näyttöpyyntö-event (App.xaml.cs);
     MainWindow.RestoreFromTray on nyt public.
   - Overlay-itsekorjaus: 11.7. overlay katosi itsestään (ulkopuolinen
     WM_CLOSE) — nyt Closed-käsittelijä luo ikkunan uudelleen ja lokittaa.
3. **Ajonaikainen todennus** (kaikki läpi): rotaatio toimi (149 MB →
   debug.old.log), min-arvot kirjautuvat oikeasti kantaan, migraatio ajoi
   30 pv kannalle, toinen instanssi poistuu itse, overlayn WM_CLOSE →
   uusi ikkuna + WARNING-loki, WM_CLOSE-sulkeminen → CleanShutdown: true.
4. `.claude/skills/verify/SKILL.md`: ajonaikaisen todennuksen resepti
   (mm. FindWindow ei löydä overlayta — käytä EnumWindows; siisti
   sulkeminen ilman trayta MinimizeToTray=false + WM_CLOSE).

## Seuraavaksi

1. **Kori B** (`docs/review-triage.md`): autostart-korotus (kytkeytyy
   paketointiin!), GPU-laitevalinta, Windows-lokin kirjanmerkin nollaus,
   collector-transaktio, GPU-fanisääntö, NaN-validointi, kriittisen tilan
   tekstiväri (AAA), insights-tapahtumat ilman sensoridataa, Timing-avain.
2. **Kori C**: graafien laatuviat (aukot, fan 5 %, DST, päätepisteet ym.).
3. Sitten ROADMAP Vaihe 8:n loput: LICENSE (MIT? LHM on MPL-2.0, mainittava)
   + README-päivitys (kertoo yhä Vaihe 1:stä!) + release + repo julkiseksi
   + paketointi (self-contained; asennus ACL-suojattuun polkuun ratkaisee
   samalla autostart-korotuslöydöksen).

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj  # 166 ✓
.\run.ps1 -AsAdmin                  # TAI tuplaklikkaa "Hardware Monitor.lnk"
```

- `dotnet test` EI buildaa App-projektia; käynnissä oleva exe lukitsee DLL:t
  (testit voi silti ajaa — ne buildaavat vain Coren).
- Sovellus suljetaan trayn kautta (Lopeta) — EI Stop-Processia (kirjaisi
  kaatumisen). Todennuksessa vaihtoehto: MinimizeToTray=false + WM_CLOSE.
- UUTTA: vain yksi instanssi käynnistyy (mutex `Local\HardwareMonitor.
  SingleInstance`); toinen käynnistys aktivoi olemassa olevan ikkunan.
- debug.log kiertää 20 MB:ssä → debug.old.log. Snapshot 60 s välein.
- LHM-gotchat ennallaan: "Virtual Memory" -ryhmä ohitetaan RAM-laskennassa,
  laitenimissä häntävälilyöntejä (Trim), NVMe "Temperature #1/#2" -fallback.
- Lokalisointi: avain molempiin resx:iin, accessor käsin; Component-arvot
  ovat kannan luokitteluavaimia — EI lokalisoida.
- WCAG AAA (≥ 7:1) kaikkiin uusiin UI-teksteihin.
- WPF: paikallinen arvo sidottuun DP:hen tuhoaa sidonnan; Loaded ei laukea
  näyttämättömälle ikkunalle; UIPI estää syötteet korotettuun ikkunaan.
- ThresholdSettings-olion VIITE ei saa vaihtua.

## Tiedostosijainnit ajossa

- Asetukset: `%LOCALAPPDATA%\HardwareMonitor\settings.json`
- Debug-loki: `%LOCALAPPDATA%\HardwareMonitor\logs\debug.log` (+ debug.old.log)
- Historia: `%LOCALAPPDATA%\HardwareMonitor\data\history.db` (+ -wal/-shm)
- Viimeisin tila: `%LOCALAPPDATA%\HardwareMonitor\data\last_state.json`
- Konetuntemus-loki: `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md`
- Ajastettu tehtävä: `schtasks /Query /TN HardwareMonitor /XML`

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440.
  Fan #2 = AIO-pumppu (~1950 RPM). KAKSI samannimistä 860 EVO:ta.
- Committoimatta TAHALLAAN: REVIEW-BRIEF.md ja windows-11-hardware-
  monitor-ohjelman-aloitusmaarittely.md; "Hardware Monitor.lnk" gitignoressa.
- Events-taulussa vanhoja testihälytyksiä (raja 30 °C) — dataa, ei bugi.
- README on VANHENTUNUT — päivitys julkaisuvaiheessa.
