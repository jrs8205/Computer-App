# HANDOFF — 12.7.2026 istunto (päivitys 2: Kori B + asennus)

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/review-triage.md` (tarkastuslöydösten tilanne) ja
`docs/ROADMAP.md`.

## Tilanne yhdellä lauseella

**Tarkastuksen Korit A ja B on korjattu TDD:llä ja todennettu ajossa
(180 testiä), JA sovellus on nyt asennettu oikeana sovelluksena
`C:\Program Files\Hardware Monitor` -polkuun** (tools/install.ps1;
autostart-tehtävä osoittaa asennettuun exeen korotettuna) — jäljellä
Kori C (graafien laatu) ja sitten LICENSE + README + release + repo
julkiseksi.

## SOVELLUS AJAA NYT ASENNETUSTA POLUSTA

- Asennus: `C:\Program Files\Hardware Monitor\HardwareMonitor.exe`
  (self-contained win-x64 Release; ei riipu Downloads-kansion buildeista).
- Ajastettu tehtävä `HardwareMonitor` käynnistää sen kirjautuessa
  korotettuna (`--tray`); Käynnistä-valikossa pikakuvake admin-lipulla.
- Uudelleenasennus koodimuutosten jälkeen: sulje sovellus (tray →
  Lopeta) ja aja korotettuna `.\tools\install.ps1`. Poisto: `-Uninstall`.
- Kehitysbuildia (Downloads) voi yhä ajaa run.ps1:llä — mutta jos
  autostart on päällä, Downloads-ajo kirjoittaa tehtävän ILMAN korotusta
  (turvasääntö) ja asennetun exen seuraava ajo palauttaa korotuksen.
  Repojuuren "Hardware Monitor.lnk" osoittaa yhä debug-buildiin.

## Tehty tässä istunnossa (12.7.2026)

1. **Tarkastuslöydösten triage** (kaikki 26 todennettu): `docs/review-triage.md`.
2. **Kori A** (8 korjausta): loki-kierto 20 MB + eräkirjoitus (loki oli
   149 MB ja per-rivi-kirjoitus venytti 1 s tickin ~15 s:iin!),
   min-sarakkeet + migraatio, SQL-harvennettu graafiluku
   (ReadSampleRowsDownsampled), LastState-race, Dispose-järjestys,
   CSV-levyduplikaatit #1/#2, single instance -mutex, overlayn
   itsekorjaus (11.7. katoaminen = ulkopuolinen WM_CLOSE).
3. **Kori B** (9 korjausta): autostart-korotus vain suojatusta polusta
   (Core/Security/ProtectedPaths + AutostartService-lokitus), GPU-kentät
   yhdestä ensisijaisesta laitteesta, Windows-lokin kirjanmerkin nollaus
   tyhjennyksen jälkeen (ReadNewestRecordId), tapahtumat+kirjanmerkki
   samassa transaktiossa (InsertEventsWithMeta), GPU-fanit pois
   CPU-fanisäännöstä, NaN-hylkäys, kriittisen tekstin väri #FF9E9E
   (7,8:1 AAA; reunukset yhä #EF5350), insights-tapahtumat ilman
   sensoridataa, SensorType_Timing molempiin resursseihin.
4. **Asennus** (käyttäjän pyyntö): `tools/install.ps1` — publish +
   kopiointi Program Filesiin + tehtäväpäivitys + pikakuvake.
   GOTCHA: schtasks /TR PowerShellistä EI saa käyttää \`"-escapointia
   kenoviivalla (literaali \" menee schtasksille) — pelkkä backtick-quote.
5. **Ajonaikaiset todennukset**: Downloads-ajo pudotti tehtävän
   rajoitetuksi + lokiselitys; asennettu ajo palautti HighestAvailable;
   siistit sulkemiset (CleanShutdown: true); ei VIRHE-rivejä.

## Seuraavaksi

1. **Kori C** (`docs/review-triage.md`): graafien aukot, fan 5 % raaka-
   datasta, aikavälijono, DST, päätepisteet, integer-näyttö, levykoosteet
   nimellä, kadonneet sensorit puusta, raportin tuore riski, overlayn
   LineHeight-todennus (todennäköinen väärä positiivinen).
2. **ROADMAP Vaihe 8:n loput**: LICENSE (MIT? LHM on MPL-2.0, mainittava),
   README-päivitys (kertoo yhä Vaihe 1:stä!), release + repo julkiseksi.
   Julkaisupaketiksi harkitse zip publish-kansiosta tai oikea installeri
   (Inno Setup) — install.ps1 on kehittäjän työkalu, ei loppukäyttäjän.

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj  # 180 ✓
.\tools\install.ps1                 # julkaisu + asennus (korotettu shell)
.\run.ps1 -AsAdmin                  # kehitysajo Downloads-buildista
```

- `dotnet test` EI buildaa App-projektia; käynnissä oleva exe lukitsee
  omat DLL:nsä — asennettu exe EI lukitse repo-buildia, joten build
  onnistuu vaikka asennettu sovellus ajaa. Asennus vaatii sulkemisen.
- Sovellus suljetaan trayn kautta (Lopeta) — EI Stop-Processia.
  Todennuksessa: MinimizeToTray=false + WM_CLOSE (ks. .claude/skills/verify).
- Vain yksi instanssi (mutex); toinen käynnistys aktivoi ikkunan.
- debug.log kiertää 20 MB:ssä → debug.old.log; snapshot 60 s välein.
- LHM-gotchat: "Virtual Memory" ohitetaan RAM-laskennassa, nimissä
  häntävälilyöntejä (Trim), NVMe "Temperature #1/#2" -fallback,
  GPU-kentät vain ensisijaisesta GPU:sta.
- Lokalisointi: avain molempiin resx:iin + accessor käsin; events-taulun
  Component-arvot ovat luokitteluavaimia — EI lokalisoida.
- WCAG AAA (≥ 7:1) kaikkiin UI-TEKSTEIHIN; reunuksille riittää 3:1.
- WPF: paikallinen arvo sidottuun DP:hen tuhoaa sidonnan; Loaded ei
  laukea näyttämättömälle ikkunalle; UIPI estää syötteet korotettuun
  ikkunaan. ThresholdSettings-olion VIITE ei saa vaihtua.

## Tiedostosijainnit ajossa

- Asennus: `C:\Program Files\Hardware Monitor\`
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
  monitor-ohjelman-aloitusmaarittely.md; "Hardware Monitor.lnk" ja
  publish/-kansio gitignoreen (publish/ lisättävä jos ei vielä ole!).
- Events-taulussa vanhoja testihälytyksiä (raja 30 °C) — dataa, ei bugi.
- README on VANHENTUNUT — päivitys julkaisuvaiheessa.
