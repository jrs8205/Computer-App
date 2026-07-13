# HANDOFF — 13.7.2026 istunnon päätös: v1.0.5 julkaistu (overlay-korjaus)

Tämä tiedosto kertoo mihin jäätiin ja miten jatketaan. Lue tämä ensin,
sitten `docs/review-triage.md` ja `docs/ROADMAP.md`.

## Tilanne yhdellä lauseella

**Projekti on julkaisukunnossa eikä aktiivista työtä ole kesken**: v1.0.5
julkaistu GitHubiin (käyttäjän raportoima overlayn katoamisbugi korjattu
TDD:llä, 226 testiä), repo JULKINEN, README kaksikielinen, exet
allekirjoitettu, lisenssitekstit paketissa ja git-historia puhdistettu.

## Uusinta (v1.0.5): overlay ei kadonnut — se lakkasi piirtymästä

Käyttäjä raportoi overlayn "katoavan itsestään". Diagnoosi 13.7.2026
ajossa olevasta sovelluksesta: overlayn HWND jäi elämään (visible,
topmost, oikea sijainti EnumWindowsilla todennettu), mutta läpinäkyvän
ikkunan (`AllowsTransparency` → WS_EX_LAYERED) sisältö tyhjeni näytön
unen jälkeen — kuvakaappaus paikalta oli tyhjä. Vanha itsekorjausvahti
(OnOverlayClosed) ei lauennut, koska ikkuna ei sulkeutunut. Lokien
"istunto päättyi yllättäen" -rivit olivat erillinen asia: koneen
uudelleenkäynnistyksiä, ei kaatumisia (Application-lokissa 0 kaatumista).

Korjaus: `Core/Power/OverlayRecoveryPolicy` (puhdas tilakone, 9 testiä):
riskitapahtuma (DisplayOff/Suspend/SessionLock) virittää, ensimmäinen
herätys (DisplayOn/Resume/SessionUnlock) laukaisee uudelleenluonnin —
vain kerran per jakso. `App/PowerSessionEventSource` kuuntelee
WM_POWERBROADCAST-ilmoituksia (RegisterPowerSettingNotification,
GUID_CONSOLE_DISPLAY_STATE; kahva EnsureHandlella, koska tray-tilassa
pääikkunaa ei näytetä) sekä SystemEventsin lepotila-/istuntotapahtumia.
MainWindow ajaa tilakoneen ja uudelleenluonnin aina UI-säikeessä
(Dispatcher.BeginInvoke) hallitulla sulkemispolulla. Todennettu ajossa:
näytön ohjelmallinen sammutus/herätys (SC_MONITORPOWER) → lokirivi
"Overlay luotu uudelleen herätyksen jälkeen (syy: DisplayOn)" +
kuvakaappauksessa elävä overlay.

## JULKAISU VAIN tools/release.ps1:llä (v1.0.4)

Uusi `tools/release.ps1` on ainoa oikea julkaisureitti: tyhjentää publishin,
tarkistaa että csprojin <Version> ja setup.iss:n MyAppVersion täsmäävät,
tarkistaa että jokainen publishin kolmannen osapuolen DLL on
THIRD-PARTY-NOTICES.md:ssä, julkaisee, allekirjoittaa app-exen, kääntää
ISCC:llä ja allekirjoittaa setup.exen. Versio päivitetään yhä KAHTEEN
paikkaan (csproj + setup.iss) — release.ps1 varmistaa täsmäävyyden.
tools/install.ps1 on kehittäjän pika-asennus (poistaa vanhan tehtävän
ennen kopiointia, asentaa tuoreeseen hakemistoon).

## Uusinta (v1.0.4): neljännen katselmoinnin 16 korjausta

Ks. `docs/review-triage.md` (neljäs osio). Tärkeimmät: ACL-tarkistus
huomioi nyt omistajan (implisiittinen WRITE_DAC) ja koko asennuspuun
(exe+DLL:t+esivanhemmat, ProtectedPaths.IsInstallTreeSecure); negatiivinen
retention ei enää pyyhi historiaa (SettingsService clamppaa + PurgeOlderThan
-suoja); levyn tunniste kantaan asti (migraatio); DebugLogger taustajonoon
(ei heitä); shutdown odottaa DB-kirjoitukset; kaikki asetusarvot clampataan
latauksessa; täydellinen THIRD-PARTY-NOTICES.

## Uusinta (v1.0.3): kolmannen katselmoinnin 16 korjausta

Ks. `docs/review-triage.md` (kolmas osio). Tärkeimmät: ProtectedPaths
tarkistaa nyt ACL:n allowlist-periaatteella (vain admin/SYSTEM/
TrustedInstaller-kirjoitus sallitaan); autostart poistaa suojaamattomaan
polkuun osoittavan vanhan tehtävän; Windows-lokin sukupolvi+tapahtumat+
bookmark yhteen transaktioon; AI-napit rakentavat taustasäikeessä;
atominen tiedostonkirjoitus (AtomicFile); tuulettimet ryhmitellään
pysyvällä tunnisteella; SettingsService täydentää null-sisäoliot.

## Uusinta (v1.0.2): AI-raportin napit

Konetuntemus-loki (machine-insights.md) oli aiemmin VAIN automaattisesti
levylle kirjoitettava (käynnistys + 30 min välein), ei UI:sta saatavissa
— käyttäjä ei löytänyt mistä sen luo. Lisätty yläpalkkiin "Luo raportti"/
"Vie CSV" -nappien viereen: **"Kopioi AI-raportti"** (leikepöydälle) ja
**"Tallenna AI-raportti…"** (.md, SaveFileDialog + avaus). Molemmat
käyttävät `MainViewModel.BuildMachineInsights()` — sama sisältö kuin
taustakirjoitus, mutta tuoreena napin painalluksella. Lokalisoitu fi/en.
Todennettu ajossa: nappi kopioi 3693 merkkiä tuoretta sisältöä + vahvistus.

## Julkaisun tila

- **Repo**: https://github.com/jrs8205/Computer-App — JULKINEN.
  **main = julkaisuhaara** (sama kärki kuin työhaara
  `claude/windows-11-program-setup-rxuyhn`). About-osio + topicit asetettu.
- **Release**: v1.0.5, liitteenä allekirjoitettu
  `HardwareMonitor-Setup-1.0.5.exe` (self-contained; sisältää LICENSE.txt
  + THIRD-PARTY-NOTICES.md). v1.0.0–1.0.4 jäävät historiaan.
- **Asennettuna koneella**: `C:\Program Files\Hardware Monitor` (v1.0.5,
  allekirjoitettu; autostart-tehtävä HighestAvailable).
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

## Seuraavaksi (ei mitään pakollista kesken — ideoita)

1. **Aito code signing** (Azure Trusted Signing ~10 $/kk tai Certum OSS
   -varmenne) → poistaisi SmartScreen-varoituksen "tuntematon julkaisija".
   TÄMÄ on selkein aiemmissa istunnoissa puhuttu avoin kohta; muuta
   pakollista ei ole. Vaatii tilin/varmenteen hankinnan ja henkilöllisyyden
   varmennuksen (päiviä).
2. ROADMAPin jatkokehitysideat (PresentMon/FPS-mittaus ym.).
3. (valinnainen) Muodollinen lisenssitarkastus ennen laajaa jakelua
   (kirjattu THIRD-PARTY-NOTICES.md:hen).
4. (valinnainen) Events-taulussa on yhä vanhoja testihälytyksiä (raja 30 °C)
   — dataa, ei bugi; voi siivota käyttäjän pyynnöstä.

## Build- ja ajokomennot + sudenkuopat

```powershell
dotnet build HardwareMonitor.sln    # AINA UI-muutosten jälkeen!
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj  # 226 ✓
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
