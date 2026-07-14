# Päivitysominaisuudet — sovelluksen päivitysilmoitus + Ylläpito-välilehti (design)

Päivä: 14.7.2026. Perustuu käyttäjän ideaan: sovellus voisi (a) ilmoittaa
itse uusista versioistaan automaattisesti ja (b) kertoa laitteiston
(emolevy, näytönohjain, levyt) päivityspolut — mistä kunkin laitteen
päivitykset voi tarkistaa. Design hyväksytty 14.7.2026.

## Päätökset (brainstorm 14.7.2026)

- Toteutetaan molemmat, järjestyksessä: ensin osa A (päivitysilmoitus),
  sitten osa B (Ylläpito-välilehti). Sama spec kattaa molemmat.
- Ilmoituksen klikkaus lataa allekirjoitetun setup.exen ja käynnistää
  asennuksen — käyttäjä päättää milloin, mutta yhdellä klikkauksella.
  Dialogissa näytetään releasen muutosteksti ("mitä uutta").
- Laitelinkit ja versiotiedot uudelle Ylläpito-välilehdelle; myös
  sovelluksen oma versio ja manuaalinen "Tarkista päivitykset" sinne.
- Aluevalinta Windowsin kieliasetuksesta (fi → fi-aluesivut), EI
  IP-paikannusta. Valmistajien sivut ohjaavat muutenkin alueelle.
- EI automaattista "uusi BIOS/ajuri saatavilla" -tarkistusta
  valmistajilta: virallisia rajapintoja ei ole ja scraping hajoaisi
  hiljaa. Sovellus näyttää nykyversiot ja linkit — vertailun tekee
  käyttäjä.
- Päivitystarkistus on sovelluksen ensimmäinen verkkokutsu →
  asetuksella pois kytkettävissä, epäonnistuminen aina hiljainen.

## Osa A: sovelluksen päivitysilmoitus

### 1. UpdateChecker + UpdateInfo (Core/Updates, puhdas, TDD)

- `UpdateInfo`-record: Version (esim. "1.0.6"), ReleaseUrl,
  SetupAssetUrl (voi olla null jos liite puuttuu), ReleaseNotes (body).
- `UpdateChecker.ParseLatestRelease(json)`: GitHubin
  `releases/latest`-vastauksesta UpdateInfo; tag "v1.0.6" → "1.0.6";
  setup-liite tunnistetaan nimestä (`HardwareMonitor-Setup-*.exe`);
  kelvoton JSON → null (ei heitä).
- `UpdateChecker.IsNewer(current, latest)`: System.Version-vertailu;
  jäsentymätön versio → false.
- `UpdateChecker.ShouldNotify(latest, current, lastNotified)`: true vain
  kun latest > current JA latest ≠ viimeksi ilmoitettu — sama versio
  ilmoitetaan vain kerran, "Myöhemmin" ei johda jankutukseen.

### 2. UpdateService (App)

- Kutsuu `https://api.github.com/repos/jrs8205/Computer-App/releases/latest`
  HttpClientillä: käynnistyksen jälkeen viivästettynä ~30 s (ei hidasta
  käynnistystä) + 24 h välein. Timeout 10 s. GitHub API vaatii
  User-Agent-otsakkeen (esim. "HardwareMonitor/1.0.6") — ilman sitä 403.
  `releases/latest` ohittaa draftit ja prereleasit itsestään; ilman
  tokenia raja 60 kutsua/h riittää reilusti.
- Epäonnistuminen (ei verkkoa, rate limit, jäsennysvirhe) → debug-lokiin
  rivi, ei ilmoitusta eikä virhedialogia.
- Kun ShouldNotify → tray-ilmoitus (NotificationBuilder) ja
  LastNotifiedVersion talteen asetuksiin.

### 3. Päivitysdialogi + lataus ja asennus (App)

- Tray-ilmoituksen klikkaus (ja manuaalinen tarkistus, kun päivitys
  löytyy) avaa dialogin: uusi versionumero, releasen muutosteksti
  (vieritettävä tekstialue, plain text) ja napit **Asenna nyt** /
  **Myöhemmin**.
- Asenna nyt: lataa setup.exen %TEMP%-hakemistoon, tarkistaa
  Authenticode-allekirjoituksen ja vertaa varmenteen thumbprintin
  vakioon (346D869550F3A7BD54FA947E024341C64F729AF8) — epäkelpo
  allekirjoitus → asennusta ei käynnistetä ja dialogi kertoo syyn.
  Kelvollinen → installeri käynnistetään; Inno sulkee sovelluksen
  siististi Restart Managerilla (CloseApplications, todennettu v1.0.x).
- Myöhemmin: dialogi sulkeutuu; sama versio ei ilmoita uudelleen, mutta
  Ylläpito-välilehden manuaalinen tarkistus näyttää sen aina.

### 4. Asetukset

- `AppSettings.Updates`: `CheckAutomatically` (bool, oletus true),
  `LastNotifiedVersion` (string, oletus tyhjä). SettingsService
  täydentää null-sisäolion nykyiseen tapaan.
- Asetukset-välilehdelle valinta "Tarkista päivitykset automaattisesti".

## Osa B: Ylläpito-välilehti

### 5. DeviceVersionReader (App, WMI)

- Lukee kerran välilehden avauksessa (taustasäikeessä):
  `Win32_BIOS` (SMBIOSBIOSVersion + ReleaseDate), `Win32_VideoController`
  (DriverVersion + DriverDate, vain ensisijainen GPU),
  `Win32_DiskDrive` (Model + FirmwareRevision). Puuttuva arvo → null,
  UI näyttää "—". WMI-poikkeukset eivät kaada näkymää.

### 6. VendorLinkResolver (Core/Maintenance, puhdas, TDD)

- Syöte: MachineSpecin laitenimet + kaksikirjaiminen kieli (esim. "fi").
- Tuloste: rivilista (laitetyyppi, mallinimi, URL tai null).
- Linkkimallit: nimi alkaa/sisältää "ASUS" → `https://www.asus.com/{kieli}/support/`
  (tuntematon kieli → globaali ilman kielipolkua); "NVIDIA" →
  `https://www.nvidia.com/{kieli}-{kieli}/drivers/` (fallback
  `https://www.nvidia.com/Download/index.aspx`); "Samsung SSD" →
  `https://semiconductor.samsung.com/consumer-storage/support/tools/`
  (globaali). Tuntematon valmistaja → rivi ilman linkkiä.
- Syvälinkkejä mallisivuille EI rakenneta (hauraita) — linkki vie
  valmistajan tukisivulle ja mallinimen voi kopioida napista hakua varten.

### 7. NvidiaDriverVersion (Core/Maintenance, puhdas, TDD)

- WMI:n raakaversio "32.0.15.4680" → markkinointiversio "546.80"
  (kahden viimeisen kentän numerot yhteen, viisi viimeistä merkkiä →
  xxx.xx). Kelvoton syöte → null, UI näyttää silloin raakaversion.
  Ilman tätä WMI-versiota ei voi verrata NVIDIAn sivun numeroon.

### 8. UI: Ylläpito-välilehti (App)

- Uusi välilehti nykyisten rinnalle (Dashboard, Kaikki sensorit,
  Asetukset, Historia). Laiteriveissä: laitetyyppi, mallinimi, nykyinen versio
  (BIOS/ajuri/firmware + päiväys jos saatavilla), linkki valmistajan
  tukisivulle (avautuu selaimeen) ja kopioi-nappi (mallinimi
  leikepöydälle).
- Alaosassa sovellusosio: nykyinen versio, "Tarkista päivitykset"
  -nappi (tarkistus taustasäikeessä; tulos: "Uusin versio käytössä" /
  päivitysdialogi / "Tarkistus epäonnistui"), viimeisimmän tarkistuksen
  aika.
- Lokalisointi fi/en (avaimet molempiin resx:iin + accessorit käsin),
  tekstit WCAG AAA (≥ 7:1). Levyrivit LHM-identifierin mukaan kuten
  muuallakin; WMI-levyt yhdistetään mallinimellä, samannimiset levyt
  numeroidaan (#1/#2) kuten graafeissa.

## Testaus

- `UpdateCheckerTests` (TDD): JSON-jäsennys (kelvollinen vastaus,
  puuttuva setup-liite, kelvoton JSON), versiovertailu (v-etuliite,
  yhtä suuret, jäsentymätön), ShouldNotify (uusi versio kyllä; sama
  kuin lastNotified ei; vanhempi ei).
- `VendorLinkResolverTests` (TDD): ASUS/NVIDIA/Samsung-mallit,
  kielipolku fi vs. tuntematon kieli → globaali, tuntematon valmistaja
  → ei linkkiä, häntävälilyönnit trimmataan.
- `NvidiaDriverVersionTests` (TDD): mappaus, lyhyt/kelvoton syöte → null.
- App-osat (HTTP, WMI, dialogi, lataus+allekirjoitustarkistus)
  todennetaan ajossa verify-skillin mukaisesti; latauksen
  allekirjoitustarkistus todennetaan myös tahallaan väärällä
  tiedostolla.

## Rajaukset

- Ei automaattista valmistajapäivitysten tarkistusta (BIOS/ajurit) —
  vain nykyversiot + linkit.
- Ei IP-paikannusta; alue Windowsin kieliasetuksesta.
- Ei täysautomaattista hiljaista asennusta — asennus käynnistyy vain
  käyttäjän klikkauksesta.
- Verkkoyhteydet vain GitHubiin (api.github.com + release-liitteen
  lataus); kytkettävissä pois asetuksesta.
- CPU:lle ei omaa linkkiriviä (mikrokoodi päivittyy BIOSin mukana);
  emolevyn tukisivu kattaa myös piirisarja-ajurit.
