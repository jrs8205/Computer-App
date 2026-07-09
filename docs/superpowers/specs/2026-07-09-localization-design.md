# fi/en-kielituki (Vaihe 8, viimeistely) — design

Päivämäärä: 9.7.2026. Hyväksytty käyttäjän kanssa AskUserQuestion-kierroksilla
(laajuus: koko sovellus; vaihto uudelleenkäynnistyksellä; oletus automaattinen).

## Tavoite

Koko sovellus käytettävissä suomeksi ja englanniksi: UI, raportit,
CSV-otsikot, riskianalyysin havainnot, tapahtumaviestit, tray-ilmoitukset
ja machine-insights. Tärkeä repon julkistusta varten.

## Mekanismi: resx, neutraali = suomi, vaihto uudelleenkäynnistyksellä

- `src/HardwareMonitor.Core/Localization/Strings.resx` (+ `Strings.en.resx`):
  Coren tuottamat tekstit (~120 kpl) — ThresholdMonitor-viestit,
  RiskAnalyzer-havainnot/suositukset, ReportBuilder, CsvExporter-otsikot,
  MachineInsightsBuilder, WindowsEventClassifier, NotificationBuilder,
  SettingsValidator-virheet.
- `src/HardwareMonitor.App/Localization/UiStrings.resx` (+ `UiStrings.en.resx`):
  XAML- ja VM-tekstit (~95 kpl) — välilehdet, ryhmät, checkboxit, napit,
  tooltipit, tray-valikko, dialogit, Status-rivit, asetusrivien nimikkeet.
- Molempiin assemblyihin `[assembly: NeutralResourcesLanguage("fi")]` —
  nykyiset suomenkieliset tekstit siirtyvät neutraaliin resurssiin
  SELLAISINAAN (ei tekstimuutoksia samalla), englanti on satelliitti.
- Generointi: PublicResXFileCodeGenerator → vahvasti tyypitetyt staattiset
  propertyt; XAML: `{x:Static loc:UiStrings.Avain}`; parametrilliset viestit
  format-stringeinä (`string.Format(Strings.Avain, ...)`).

## Kielen valinta ja voimaantulo

- `AppSettings.Language`: `""` = automaattinen (oletus), `"fi"`, `"en"`.
- `App.OnStartup` lukee asetukset ENNEN ikkunoiden luontia ja asettaa
  `CultureInfo.DefaultThreadCurrentUICulture` (kattaa taustasäikeet, joissa
  raportit/analyysit/insights syntyvät). Automaattinen: fi jos
  `CultureInfo.InstalledUICulture` on suomi, muuten en.
- `CurrentCulture` EI muutu: numero-, päivämäärä- ja CSV-muotoilut pysyvät
  ennallaan (fi-Excel-CSV säilyy; vain otsikkotekstit kääntyvät).
- Kielen vaihto vaikuttaa vasta uudelleenkäynnistyksen jälkeen — sama
  huomautusmalli kuin koostevälillä (kursivoitu Note-teksti).

## UI: kielivalitsin

Asetukset → Yleiset: ComboBox "Kieli / Language":
Automaattinen · Suomi · English (indeksit 0/1/2 ↔ ""/fi/en).
SettingsViewModelissa `LanguageIndex`-property (tallennus heti kuten muutkin).

## Testaus

- Nykyiset 126 testiä säilyvät: testiprojektiin kulttuurikiinnitys
  (fi-FI UI-kulttuuri xUnitin käynnistyessä), jotta suomenkieliset
  assertiot eivät riipu ajokoneen kielestä.
- Uudet testit: en-UI-kulttuurissa (a) ThresholdMonitor tuottaa
  englanninkielisen viestin, (b) ReportBuilderin otsikko on englanniksi,
  (c) CsvExporterin otsikkorivi on englanniksi mutta desimaalipilkku
  säilyy CurrentCulturen mukaisena, (d) kielenvalinnan resoluutio
  (""/fi/en → oikea kulttuuri).
- Ajossa: käynnistys molemmilla kielillä — UI, tilapaneeli, raportti,
  CSV, ilmoitus ja machine-insights oikealla kielellä; Automaattinen
  antaa suomen tällä koneella.

## Rajaukset

- Kantaan tallennetut vanhat tapahtumaviestit ovat dataa — näytetään
  tallennuskielellään (ei käännetä jälkikäteen).
- Käyttäjän omat tuulettimien nimilaput eivät käänny.
- HANDOFF, specit ja muu dokumentaatio pysyvät suomeksi.
- Ei live-kielenvaihtoa (vaatisi binding-pohjaisen infran) — kirjattu
  jatkokehitysideaksi.
- Käännökset amerikanenglanniksi; kääntäjänä Claude, käyttäjä katselmoi
  ajonaikaisessa todennuksessa.
