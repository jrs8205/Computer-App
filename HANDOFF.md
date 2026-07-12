# HANDOFF — 12.7.2026 istunto (päivitys 3: Kori C + julkaisu v1.0.0)

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/review-triage.md` ja `docs/ROADMAP.md`.

## Tilanne yhdellä lauseella

**v1.0.0 on julkaistu**: kaikki tarkastuksen kolme korjauskoria (A/B/C)
tehty TDD:llä (184 testiä), sovellus on GPL-3.0, README uudistettu,
Inno Setup -installeri rakennettu ja GitHub-release luotu — sovellus
ajaa asennettuna polusta `C:\Program Files\Hardware Monitor` (v1.0.0).

## Julkaisu ja asennus

- **Release**: GitHub-release `v1.0.0`, liitteenä
  `HardwareMonitor-Setup-1.0.0.exe` (Inno Setup, ~56 MB self-contained).
- **Installerin rakennus**: `dotnet publish ... -o publish` +
  `ISCC.exe installer\setup.iss` (ISCC: `%LOCALAPPDATA%\Programs\
  Inno Setup 6\`). Tuloste: `installer/Output/` (gitignoressa).
- Installeri sulkee ajossa olevan sovelluksen SIISTISTI Restart
  Managerilla (todennettu: CleanShutdown säilyi true) ja poisto
  (`unins000.exe`) poistaa myös autostart-tehtävän. Käyttäjädata
  (%LOCALAPPDATA%\HardwareMonitor) säilyy.
- **app.manifest on nyt requireAdministrator** (päätös 12.7.): manuaalinen
  käynnistys kysyy UAC:n, autostart-tehtävä käynnistää ilman kyselyä.
  HUOM: myös kehitysajo (run.ps1 ilman -AsAdmin) nostaa nyt UAC-kyselyn.
- Versio asetetaan `HardwareMonitor.App.csproj`:n `<Version>`-propertyllä
  JA `installer/setup.iss`:n `MyAppVersion`-definellä — päivitä molemmat.

## Tehty 12.7.2026 (kolme rupeamaa)

1. **Triage**: 26 löydöstä todennettu (24 aitoa) → `docs/review-triage.md`.
2. **Kori A** (data ja vakaus): loki-kierto + eräkirjoitus, min-sarakkeet
   + migraatio, SQL-harvennettu graafiluku, LastState-race, CSV-duplikaatit,
   single instance, overlay-itsekorjaus.
3. **Kori B** (julkaisublokkerit): autostart-korotus vain suojatusta
   polusta (ProtectedPaths), GPU-kentät yhdestä laitteesta, Windows-lokin
   kirjanmerkin nollaus + atominen kirjoitus, GPU-fanit pois CPU-säännöstä,
   NaN-hylkäys, kriittinen tekstiväri #FF9E9E, insights-tapahtumat ilman
   sensoridataa, SensorType_Timing.
4. **Kori C** (graafien laatu): aukot sammutusjaksoihin (3× mediaaniväli),
   fan 5 % SpinSharesta, päätepisteiden arvot, levykoosteet nimi+esiintymä,
   aikavälijonon uusintahaku, DST-monotonisuus (UTC-pisteet + paikallinen
   labeler), kadonneet sensorit puusta, raportin tuore riski,
   integer-kenttien normalize, overlayn LineHeight-sidonta.
5. **Julkaisu**: LICENSE (GPL-3.0), README, versio 1.0.0,
   requireAdministrator, installer/setup.iss, GitHub-release.

## Allekirjoitus (12.7.2026)

- Exet allekirjoitetaan itse allekirjoitetulla varmenteella:
  **CN=jrs8205 Hardware Monitor**, thumbprint
  `346D869550F3A7BD54FA947E024341C64F729AF8`, voimassa 12.7.2031,
  yksityisavain käyttäjän CurrentUser\My-varastossa (vain tällä koneella).
  Julkinen osa on viety LocalMachine Root + TrustedPublisher -varastoihin,
  joten omalla koneella allekirjoitus näkyy kelvollisena.
- Uuden version allekirjoitus: `Set-AuthenticodeSignature -FilePath <exe>
  -Certificate (Get-ChildItem Cert:\CurrentUser\My | Where Thumbprint -eq
  '346D…AF8') -HashAlgorithm SHA256 -TimestampServer
  "http://timestamp.digicert.com"` — ENSIN publish\HardwareMonitor.exe,
  SITTEN ISCC-käännös, LOPUKSI setup.exe.
- Itse allekirjoitettu varmenne EI poista SmartScreen-varoitusta muilta —
  aito ratkaisu olisi Azure Trusted Signing (~10 $/kk) tai Certum OSS
  -varmenne; kirjattu jatkokehitysideaksi.
- setup.iss: EI AppMutexia — sen tarkistus keskeyttäisi hiljaisen
  asennuksen; CloseApplications=yes sulkee ajossa olevan sovelluksen
  Restart Managerilla SIISTISTI (todennettu: CleanShutdown säilyy true).

## Commit-käytäntö (käyttäjän päätös 12.7.2026)

- **Committeihin EI lisätä Co-Authored-By-riviä.** Koko historia
  kirjoitettiin uudelleen 12.7.2026 (84 traileria pois + aloituscommitin
  tekijä korjattu) — kaikki commit-hashit vaihtuivat ja v1.0.0-release
  luotiin uudelleen. Vanhoihin hasheihin viittaavat muistiinpanot ovat
  vanhentuneita.
- main = julkaisuhaara; pushaa työhaara + main (`git push origin
  HEAD:työhaara HEAD:main`).

## Seuraavaksi (avoinna)

- Mahdollinen jatkokehitys: ROADMAPin ideat (PresentMon/FPS ym.),
  aito code signing (Trusted Signing / Certum) SmartScreeniä varten.

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj  # 184 ✓
.\tools\install.ps1                 # kehittäjän pika-asennus Program Filesiin
dotnet publish src\HardwareMonitor.App\HardwareMonitor.App.csproj -c Release -r win-x64 --self-contained true -o publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\setup.iss
```

- `dotnet test` EI buildaa App-projektia. Asennettu exe EI lukitse
  repo-buildia. Sovellus suljetaan trayn kautta — EI Stop-Processia
  (todennuksessa: MinimizeToTray=false + WM_CLOSE, ks. .claude/skills/verify).
- schtasks /TR PowerShellistä: backtick-quote ILMAN kenoviivaa.
- Vain yksi instanssi (mutex `Local\HardwareMonitor.SingleInstance`).
- debug.log kiertää 20 MB → debug.old.log; snapshot 60 s välein.
- LHM: "Virtual Memory" ohitetaan RAM-laskennassa; nimissä
  häntävälilyöntejä (Trim); NVMe "Temperature #1/#2" -fallback;
  GPU-kentät vain ensisijaisesta GPU:sta.
- Lokalisointi: avain molempiin resx:iin + accessor käsin; events-taulun
  Component-arvot ovat luokitteluavaimia — EI lokalisoida.
- WCAG AAA (≥ 7:1) UI-teksteihin; reunuksille riittää 3:1.
- WPF: paikallinen arvo sidottuun DP:hen tuhoaa sidonnan; Loaded ei
  laukea näyttämättömälle ikkunalle. ThresholdSettings-viite ei vaihdu.
- Graafit: pisteet UTC:nä, akselin labeler muuntaa paikalliseksi —
  älä palauta LocalDateTime-pisteitä (DST rikkoisi X-akselin).

## Tiedostosijainnit ajossa

- Asennus: `C:\Program Files\Hardware Monitor\` (+ unins000.exe)
- Asetukset/loki/data: `%LOCALAPPDATA%\HardwareMonitor\`
  (settings.json, logs\debug.log(+old), data\history.db, data\last_state.json,
  machine-insights.md)
- Ajastettu tehtävä: `schtasks /Query /TN HardwareMonitor /XML`

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440.
  Fan #2 = AIO-pumppu. KAKSI samannimistä 860 EVO:ta.
- Committoimatta TAHALLAAN: REVIEW-BRIEF.md,
  windows-11-hardware-monitor-ohjelman-aloitusmaarittely.md.
- Events-taulussa vanhoja testihälytyksiä (raja 30 °C) — dataa, ei bugi.
