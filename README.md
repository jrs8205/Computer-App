# Hardware Monitor

*(Windows 11 hardware monitor with plain-language logging, risk analysis and
post-crash forensics. UI and docs are in Finnish.)*

Windows 11 -tietokoneen laitteistomonitori, joka lukee reaaliajassa CPU:n,
GPU:n, muistin, levyjen, emolevyn ja tuulettimien tiedot. Erottuva idea:
**selkeä lokitus, riskianalyysi ja kaatumisten jälkiselvitys** — ei pelkkiä
numeroita, vaan tieto siitä, oliko kone oikeasti riskirajoilla, ja
tekoälyavustajalle luettava konetuntemus-loki.

## Ominaisuudet

- **Dashboard**: CPU / GPU / RAM / levyt / tuulettimet värikoodattuina
  kortteina + selkokielinen riskiyhteenveto suosituksineen.
- **Työpöytäoverlay**: läpiklikattava, aina päällimmäisenä; reunuksen väri
  kertoo pahimman tilan yhdellä silmäyksellä; sijainti ja rivit valittavissa.
- **Historia**: 1 s lukemat koostetaan 5 s min/avg/max-riveiksi SQLiteen
  (30 pv), graafit aikaväleillä 1 h – 30 pv.
- **Raja-arvovalvonta**: välitön väritila + tapahtumat vasta yhtäjaksoisen
  ylityksen jälkeen, tray-ilmoitukset, palautumiskirjaukset kestoineen.
- **Windowsin tapahtumaloki**: Kernel-Power 41, WHEA, näyttöajuri- ja
  levyvirheet kerätään samaan tapahtumahistoriaan.
- **Kaatumisselvitys**: yllättäen päättynyt istunto tunnistetaan ja
  viimeisimmät arvot ennen katkoa kirjataan.
- **Raportit**: selkokielinen tekstiraportti ja suomalais-Excel-CSV.
- **machine-insights.md**: jatkuvasti päivittyvä yhteenveto koneen
  normaalitasoista, trendeistä ja tapahtumista — annettavaksi kontekstina
  mille tahansa AI-avustajalle.
- **Kielet**: suomi ja englanti (fi/en). Kaikki UI-tekstit WCAG AAA
  -kontrastilla.

## Asennus

1. Asenna **PawnIO-ajuri**: <https://pawnio.eu/> → `PawnIO_setup.exe`.
   Ilman sitä CPU-lämmöt jäävät tyhjiksi Windows 11:llä (Windowsin
   estolista blokkaa vanhan WinRing0-ajurin; LibreHardwareMonitor 0.9.5+
   käyttää PawnIO:ta).
2. Lataa uusin **HardwareMonitor-Setup-x.y.z.exe** [Releases-sivulta](../../releases)
   ja aja se. Asennus menee Program Filesiin eikä vaadi .NETin asentamista
   (self-contained).
3. Käynnistä "Hardware Monitor" Käynnistä-valikosta. Sovellus vaatii
   järjestelmänvalvojan oikeudet (matalan tason sensorit) — manuaalinen
   käynnistys kysyy UAC-vahvistuksen. Kun kytket asetuksista
   **Käynnistä Windowsin mukana**, sovellus käynnistyy kirjautuessa
   korotettuna ilman kyselyä (Task Scheduler).

Tietosi tallentuvat polkuun `%LOCALAPPDATA%\HardwareMonitor\`
(asetukset, historia, lokit) — ne säilyvät päivityksissä ja poistossa.

## Kehittäjille

Vaatimukset: **Windows 10/11**, **.NET 8 SDK**, PawnIO (ks. yllä).

```powershell
dotnet build HardwareMonitor.sln    # käännös (0 varoitusta)
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj
.\run.ps1 -AsAdmin                  # kehitysajo repo-buildista
.\tools\install.ps1                 # paikallinen asennus Program Filesiin
installer\setup.iss                 # Inno Setup -asennusohjelman määrittely
```

Huomioita:

- `dotnet test` buildaa vain Core + testit — UI-muutosten jälkeen aja
  `dotnet build HardwareMonitor.sln`.
- Turvasääntö: autostart-tehtävä saa korotuksen (`/RL HIGHEST`) vain kun
  exe on ACL-suojatussa polussa (Program Files) — kirjoitettavasta polusta
  ajettuna tehtävä luodaan rajoitettuna.
- Arkkitehtuuri: `src/HardwareMonitor.Core` (kaikki logiikka, ei
  UI-riippuvuuksia, yksikkötestattu) + `src/HardwareMonitor.App`
  (WPF, MVVM ilman kirjastoja) + `src/HardwareMonitor.Tests` (xUnit).

Täysi määrittely: [`docs/requirements.md`](docs/requirements.md) ·
eteneminen: [`docs/ROADMAP.md`](docs/ROADMAP.md) ·
featurekohtaiset specit: `docs/superpowers/specs/`.

## Lisenssi

**GPL-3.0** — katso [LICENSE](LICENSE). Sovellus käyttää
[LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)-
kirjastoa (**MPL-2.0**) sekä [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)-
kirjastoa (**MIT**).
