# Asetussivu (Vaihe 8.2) — design

Päivämäärä: 9.7.2026. Hyväksytty käyttäjän kanssa AskUserQuestion-kierroksilla
(sijoitus, laajuus, yläpalkin siivous, tallennustapa, validointiarkkitehtuuri).

## Tavoite

Kaikki asetukset muokattavissa UI:sta ilman settings.jsonin käsin muokkausta.
Samalla siivotaan ahdas yläpalkki: asetukset siirtyvät omalle välilehdelle,
yläpalkkiin jäävät vain nopeat toiminnot.

## UI: uusi Asetukset-välilehti

Kolmas välilehti TabControliin (Dashboard · Kaikki sensorit · **Asetukset**),
ScrollViewer + GroupBox-ryhmät:

1. **Yleiset**: Pienennä trayhin · Käynnistä Windowsin mukana ·
   Hälytysilmoitukset. (Tänne tulee myöhemmin myös kielivalinta fi/en,
   kun resx-kielituki tehdään Vaihe 8:n lopussa — varataan paikka.)
2. **Raja-arvot**: rivit CPU, GPU, GPU hotspot, NVMe-levyt, RAM.
   Kullakin rivillä varoitus- ja kriittinen-kenttä yksikköineen (°C / %).
   Alla nappi **Palauta oletusrajat** (palauttaa vain tämän ryhmän ja
   Kestot-ryhmän arvot ThresholdSettings-oletuksiin ja tallentaa).
3. **Kestot**: WARN-kesto (s) · CRIT-kesto (s) · cooldown (min) ·
   tuuletinpysähdyksen CPU-raja (°C).
4. **Lokitus**: koosteväli (s) · historian säilytys (pv).
5. **Overlay**: kulma (ComboBox) · läpinäkyvyys (Slider) · fonttikoko ·
   mittarivalinnat (CPU/GPU/RAM/Levyt/Tuulettimet).

Yläpalkkiin jäävät: "Overlay työpöydälle", "Siirrä overlayta",
"Luo raportti…", "Vie CSV…". Muut nykyiset yläpalkin kontrollit siirtyvät
välilehdelle (XAML siirtyy, MainViewModelin propertyt säilyvät).

## Core: SettingsValidator (puhdas, TDD)

`Core/Settings/SettingsValidator` — staattinen luokka kuten RiskAnalyzer:

- `ParseNumber(string raw, float min, float max)` → tulos-record:
  onnistuessa arvo, muuten suomenkielinen virheviesti. Parsinta
  fi-kulttuurilla (desimaalipilkku JA -piste kelpaavat), tyhjä/roska →
  "Anna numero", väli → "Sallittu väli on X–Y".
- `ValidateWarnCrit(float warn, float crit)` → virhe jos warn >= crit:
  "Varoitusrajan on oltava pienempi kuin kriittisen rajan".

Sallitut välit:

| Kenttä | Väli |
|---|---|
| Lämpörajat (CPU/GPU/hotspot/NVMe, tuuletinraja) | 20–120 °C |
| RAM-rajat | 10–100 % |
| WARN/CRIT-kestot | 1–600 s |
| Cooldown | 1–60 min |
| Koosteväli | 1–60 s |
| Säilytys | 1–365 pv |
| Overlay-fonttikoko | 8–32 |

## App: SettingsViewModel (ohut liimakerros)

- Uusi `ViewModels/SettingsViewModel`, MainViewModelin lapsi
  (kuten Dashboard/Overlay-VM:t); saa AppSettingsin, tallennus- ja
  ilmoituskutsut (delegaatit) MainViewModelilta.
- Numerokentät: string-propertyt. Setterissä ParseNumber (+ parin
  ristiintarkistus) → kelvollinen arvo kirjoitetaan AppSettingsiin,
  tallennetaan SettingsServicellä ja vaikuttaa heti: ThresholdMonitor
  lukee samaa ThresholdSettings-oliota, overlay päivittyy
  OverlaySettingsChanged-tapahtumasta.
- Virheellinen arvo: kentän virheviesti-property (punainen reunus +
  viesti kentän vieressä), arvoa EI tallenneta. Kenttä säilyttää
  käyttäjän tekstin kunnes se korjataan.
- Palauta oletusrajat: kopioi `new ThresholdSettings()`-arvot nykyiseen
  olioon kenttä kerrallaan (viite ei saa vaihtua — ThresholdMonitor ja
  MainViewModel pitävät samaa viitettä), päivittää kenttien tekstit ja
  tallentaa.
- Checkboxit ja kulma/läpinäkyvyys sidotaan MainViewModelin olemassa
  oleviin propertyihin — ei uutta logiikkaa.

## Testaus ja todennus

- SettingsValidator-yksikkötestit (TDD): pilkku- ja pisteparsinta, tyhjä,
  roska, ali/ylivuoto, warn ≥ crit, rajatapaukset (min/max kelpaavat).
- Ajonaikainen todennus: rajan muutos näkyy tilapaneelissa heti ilman
  uudelleenkäynnistystä; fonttikoko päivittää overlayn heti; arvot
  säilyvät uudelleenkäynnistyksessä; virheellinen syöte näyttää virheen
  eikä muuta settings.jsonia.

## Rajaukset (YAGNI)

- Ei resx-kielitukea vielä (Vaihe 8:n loppu) — mutta uudet tekstit
  kirjoitetaan yhteen paikkaan niin että siirto resx:ään on suoraviivainen.
- Ei asetusten vienti/tuonti-toimintoa.
- Overlayn sijainti (kulma vs raahattu) säilyy nykyisellään — ei uutta
  sijaintilogiikkaa tälle sivulle.
