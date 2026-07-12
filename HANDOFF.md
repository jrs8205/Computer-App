# HANDOFF — 12.7.2026 istunnon päätös: v1.0.1 julkaistu, repo julkinen

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/review-triage.md` ja `docs/ROADMAP.md`.

## Tilanne yhdellä lauseella

**Projekti on julkaisukunnossa eikä aktiivista työtä ole kesken**: v1.0.1
julkaistu GitHubiin (ensimmäisen katselmoinnin 26 + toisen katselmoinnin
12 löydöstä kaikki korjattu TDD:llä, 196 testiä), repo on JULKINEN,
README kaksikielinen, exet allekirjoitettu, lisenssitekstit paketissa ja
git-historia puhdistettu (vain käyttäjä Contributors-listassa).

## Julkaisun tila

- **Repo**: https://github.com/jrs8205/Computer-App — JULKINEN.
  **main = julkaisuhaara** (sama kärki kuin työhaara
  `claude/windows-11-program-setup-rxuyhn`). About-osio + topicit asetettu.
- **Release**: v1.0.1, liitteenä allekirjoitettu
  `HardwareMonitor-Setup-1.0.1.exe` (self-contained; sisältää LICENSE.txt
  + THIRD-PARTY-NOTICES.md). v1.0.0 jää historiaan.
- **Asennettuna koneella**: `C:\Program Files\Hardware Monitor` (v1.0.1,
  allekirjoitettu, ajossa tray-tilassa; autostart-tehtävä HighestAvailable).
- **README.md = englanti** (julkinen etusivu), **README.fi.md = suomi**,
  ristiinlinkitetty. About-osiossa EI AI-mainintoja (käyttäjän päätös).
- **Autostart-turvasääntö v1.0.1**: korotus vain Program Files -juurten
  alta JA vain jos hakemiston+exen ACL ei salli ei-admin-kirjoitusta;
  suojaamattomasta polusta autostartia ei kytketä lainkaan
  (requireAdministrator tekisi rajoitetusta tehtävästä toimimattoman).

## Commit-käytäntö ja historia (käyttäjän päätös 12.7.2026)

- **Committeihin EI lisätä Co-Authored-By-riviä.** Historia
  uudelleenkirjoitettiin 12.7.2026 kahdesti: 84 traileria pois +
  aloituscommitin tekijäksi jrs8205 — KAIKKI vanhat commit-hashit
  vaihtuivat ja v1.0.0-release luotiin uudelleen. Vanhoissa
  muistiinpanoissa olevat hashit (fbcf0da ym.) eivät päde.
- Pushaa aina työhaara JA main: `git push origin HEAD:claude/windows-11-
  program-setup-rxuyhn HEAD:main`.
- GitHubin Contributors-laatikko voi näyttää "claude"-jäännöstä
  välimuistista — commit-data on varmistetusti puhdas (API: 0 Clauden
  committia, 0 traileria), joten merkintä katoaa itsestään.

## Allekirjoitus

- Itse allekirjoitettu varmenne: **CN=jrs8205 Hardware Monitor**,
  thumbprint `346D869550F3A7BD54FA947E024341C64F729AF8`, voimassa
  12.7.2031, yksityisavain CurrentUser\My (vain tällä koneella); julkinen
  osa luotettu koneen Root + TrustedPublisher -varastoissa.
- **Uuden version julkaisuresepti**:
  1. Nosta versio KAHDESSA paikassa: `HardwareMonitor.App.csproj`
     `<Version>` + `installer/setup.iss` `MyAppVersion`.
  2. `dotnet publish src\HardwareMonitor.App\HardwareMonitor.App.csproj
     -c Release -r win-x64 --self-contained true -o publish`
  3. Allekirjoita `publish\HardwareMonitor.exe`
     (`Set-AuthenticodeSignature`, SHA256, timestamp.digicert.com).
  4. `& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\setup.iss`
  5. Allekirjoita `installer\Output\HardwareMonitor-Setup-x.y.z.exe`.
  6. `gh release create vx.y.z <setup.exe> --target main ...`
- Itse allekirjoitus EI poista SmartScreen-varoitusta muilta — aito
  ratkaisu (jatkokehitysidea): Azure Trusted Signing tai Certum OSS.
- setup.iss: EI AppMutexia (keskeyttäisi hiljaisen asennuksen);
  CloseApplications sulkee ajossa olevan sovelluksen Restart Managerilla
  SIISTISTI (todennettu: CleanShutdown säilyy true).

## Seuraavaksi (ei mitään kesken — ideoita)

1. Aito code signing (Trusted Signing ~10 $/kk tai Certum OSS -varmenne).
2. ROADMAPin jatkokehitysideat (PresentMon/FPS-mittaus ym.).
3. Events-taulussa on yhä vanhoja testihälytyksiä (raja 30 °C) — dataa,
   ei bugi; voi siivota käyttäjän pyynnöstä.

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj  # 184 ✓
.\tools\install.ps1                 # kehittäjän pika-asennus Program Filesiin
.\run.ps1 -AsAdmin                  # kehitysajo (huom: manifest kysyy UAC:n)
```

- `dotnet test` EI buildaa App-projektia. Asennettu exe EI lukitse
  repo-buildia. Sovellus suljetaan trayn kautta — EI Stop-Processia
  (todennuksessa: MinimizeToTray=false + WM_CLOSE, ks. .claude/skills/verify).
- app.manifest on requireAdministrator → myös kehitysajo vaatii korotuksen.
- Autostart-turvasääntö: korotus vain ACL-suojatusta polusta; Downloads-ajo
  kirjoittaa tehtävän ilman korotusta, asennettu ajo palauttaa sen.
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
- Graafit: pisteet UTC:nä, akselin labeler muuntaa paikalliseksi (DST).
- schtasks /TR PowerShellistä: backtick-quote ILMAN kenoviivaa.

## Tiedostosijainnit ajossa

- Asennus: `C:\Program Files\Hardware Monitor\` (+ unins000.exe)
- Data: `%LOCALAPPDATA%\HardwareMonitor\` (settings.json, logs\debug.log
  + debug.old.log, data\history.db, data\last_state.json,
  machine-insights.md)
- Ajastettu tehtävä: `schtasks /Query /TN HardwareMonitor /XML`

## Muut muistiinpanot

- Kone: i9-9900K, RTX 2060, ASUS Z390-F, 64 GB, Win 11, 3440x1440.
  Fan #2 = AIO-pumppu. KAKSI samannimistä 860 EVO:ta.
- Committoimatta TAHALLAAN: REVIEW-BRIEF.md,
  windows-11-hardware-monitor-ohjelman-aloitusmaarittely.md.
- Lisenssi GPL-3.0 (LHM MPL-2.0 + LiveCharts2 MIT mainittu READMEssä).
