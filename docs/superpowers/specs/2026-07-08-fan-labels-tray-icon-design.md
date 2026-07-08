# Design: Tuulettimien nimilaput + tray-pienennys + sovellusikoni

Päivätty: 8.7.2026. Hyväksytty käyttäjän kanssa. Toteutetaan ennen Vaihe 3:a (SQLite-lokitus).

## Käyttäjän kanssa lukitut päätökset

| Kysymys | Päätös |
|---|---|
| Missä tuulettimet nimetään? | **Suoraan Dashboardin Tuulettimet-kortissa** kaksoisklikkaamalla riviä. Ei erillistä dialogia. |
| Tray-käyttäytyminen | **Pienennä- JA sulje-nappi vievät trayhin** kun asetus päällä (oletus: päällä); mittaus jatkuu taustalla. Oikea lopetus tray-valikon "Lopeta". |
| Ikoni | Claude suunnittelee: tumma pyöristetty neliö, syaani mittarikaari + neula, vihreä sykeviiva. SVG master + generoitu app.ico. |

## 1. Tuulettimien nimilaput

- **Avain = sensorin `Identifier`** (pysyvä, esim. `/lpc/nct6798d/0/fan/2`) — ei näyttönimi,
  koska nimet voivat toistua (vrt. kaksi "Samsung SSD 860 EVO").
- Core: `FanMetrics(Name, Rpm)` → `FanMetrics(Name, Rpm, Identifier)`;
  `KeyMetricsService.CollectFans` välittää tunnisteen.
- Core: `AppSettings.FanLabels : Dictionary<string, string>` (tunniste → oma nimi).
- App: Dashboardin tuuletinrivi muuttuu merkkijonosta `FanRowViewModel`-olioksi:
  `Identifier`, `DisplayName` (oma nimi tai oletus), `Rpm`, `IsEditing`.
  Kaksoisklikkaus → TextBox esiin; Enter tai fokuksen menetys tallentaa;
  tyhjä nimi poistaa nimilapun (palauttaa oletusnimen).
- `MainViewModel.RenameFan(identifier, name)`: päivittää sanakirjan, tallentaa
  `SettingsService`llä; uusi nimi näkyy Dashboardissa ja overlayssa seuraavalla
  1 s -päivityksellä. Overlayn `Update`-metodi saa `AppSettings`-olion (ei enää
  pelkkää `OverlaySettings`ia), jotta nimilaput ovat käytössä.

## 2. Tray-pienennys

- Core: `AppSettings.MinimizeToTray : bool`, oletus `true`.
- App: WinForms `NotifyIcon` (`<UseWindowsForms>true</UseWindowsForms>` App-projektiin;
  EI uusia NuGet-paketteja). Kuvake = sovelluksen app.ico (embedded resource).
- Käyttäytyminen kun asetus päällä:
  - Pienennä-nappi → ikkuna piiloon (`Hide()`), tray-kuvake näkyviin.
  - Sulje-nappi (X) → sama kuin pienennä (ei lopeta; `Closing`-eventissä `e.Cancel`).
  - Tray-valikko: **Näytä** (palauttaa ikkunan; tuplaklikkaus sama), **Overlay**
    (checkable, kytkee overlayn), **Lopeta** (asettaa `_reallyExiting`-lipun ja
    sulkee oikeasti; tray-kuvake dispose).
- Kun asetus pois: pienennä ja sulje toimivat normaalisti (tray-kuvake silti
  näkyvissä sovelluksen ollessa käynnissä — yksinkertaisempi elinkaari).
- Yläpalkkiin CheckBox "Pienennä trayhin" overlay-asetusten viereen.

## 3. Sovellusikoni

- Master: `src/HardwareMonitor.App/Assets/icon.svg` (käsin kirjoitettu vektorikuva):
  tumma pyöristetty neliö (#1E1E1E, kevyt gradientti), syaani (#4FC3F7)
  puolikaarimittari neuloineen (~70 % asennossa), alla vihreä (#A5D6A7)
  EKG-tyylinen sykeviiva.
- Generointi: `tools/generate-icon.ps1` piirtää saman kuvan GDI+:lla koossa
  16/24/32/48/64/128/256 ja kokoaa PNG-pakatun moniresoluutio-`app.ico`n
  (`src/HardwareMonitor.App/Assets/app.ico`). Sekä skripti että generoitu ico
  commitoidaan.
- Kytkentä: csproj `<ApplicationIcon>` (exe → tehtäväpalkki ja ikkunat) +
  app.ico Resource-itemina NotifyIconia varten
  (`Application.GetResourceStream` → `new Icon(stream)`).

## Virheenkäsittely

- Vioittunut FanLabels-sanakirja tai puuttuvat kentät settings.jsonissa →
  oletukset (nykyinen `SettingsService.Load`-fallback kattaa).
- Nimilappu tunnisteelle, jota ei enää löydy (esim. GPU vaihdettu) → jää
  sanakirjaan haitatta, ei näytetä.
- Tray-kuvake poistetaan (`Dispose`) sekä Lopeta-valinnassa että ikkunan
  oikeassa sulkeutumisessa — ei haamukuvakkeita.
- Lopetus tray-valikosta sulkee myös overlayn ja vapauttaa sensorit (nykyinen
  `Dispose`-ketju).

## Testaus

- Core-yksikkötestit: `FanMetrics.Identifier` mukana poiminnassa;
  `FanLabels`- ja `MinimizeToTray`-asetusten oletukset + tallennus/lataus.
- Käsintestaus ruutukaappauksin: nimen muokkaus kortissa → näkyy overlayssa;
  pienennä/sulje → tray; palautus ja Lopeta; ikoni tehtäväpalkissa ja trayssa.

## 4. Overlay ei saa kadota pienennettäessä (bugikorjaus)

Nykyinen `OverlayWindow` on kytketty pääikkunaan `Owner`-suhteella, jolloin WPF
pienentää/piilottaa sen pääikkunan mukana. Korjaus: **poistetaan Owner-kytkös** —
overlay elää itsenäisesti, pysyy näkyvissä kun pääikkuna menee trayhin, ja
suljetaan eksplisiittisesti pääikkunan `Closed`-käsittelijässä (tämä ketju on
jo olemassa). Päivitystimer (DispatcherTimer) jatkaa normaalisti ikkunan
ollessa piilossa.

## 5. Automaattikäynnistys Windowsin mukana

- Asetus-CheckBox **"Käynnistä Windowsin mukana"** yläpalkkiin.
- Toteutus **Task Schedulerilla** (`schtasks`), EI Run-rekisteriavaimella:
  sensorit vaativat admin-oikeudet, ja vain ajastettu tehtävä asetuksella
  "suorita korkeimmilla oikeuksilla" (`/RL HIGHEST`) käynnistyy kirjautuessa
  adminina ilman UAC-kyselyä.
- Tehtävän nimi `HardwareMonitor`, laukaisin `ONLOGON`, kohde nykyinen exe-polku.
- CheckBoxin tila luetaan käynnistyksessä tehtävän olemassaolosta
  (`schtasks /Query`) — ei erillistä settings-kenttää, joka voisi erkaantua
  todellisuudesta.
- Luonti/poisto vaatii, että sovellus itse on käynnissä adminina; jos komento
  epäonnistuu (ei adminia), kirjataan debug-lokiin ja checkbox palautetaan.
- Huom: tehtävä osoittaa nykyiseen build-polkuun (bin\Debug). Riittää
  kehitysvaiheessa; asennettu sijainti tulee myöhemmin (Vaihe 8 -paketointi).

## Rajaus (YAGNI)

- Ei levyjen/muiden sensorien nimeämistä vielä (helppo laajentaa samalla mallilla).
- Ei "käynnistä pienennettynä" -asetusta vielä.
- Ei balloon-ilmoitusta ensimmäisellä trayhin menolla.
