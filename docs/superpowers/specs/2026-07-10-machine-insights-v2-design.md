# machine-insights.md v2 — "anna tekoälychatille" -tiedosto (design)

Päivä: 10.7.2026. Perustuu käyttäjän alkuperäiseen ideaan (Vaihe 6) ja
9.7. illalla sovittuun jatkoon: nykyinen suppea konetuntemus-loki
laajennetaan tiedostoksi, jonka voi antaa mille tahansa tekoälychatille
koneen kontekstiksi. Design hyväksytty 10.7.2026 aamulla.

## Päätökset (brainstorm 10.7.2026)

- Sisältö: johdanto AI:lle, koneen kokoonpano, trendit ja poikkeamat,
  tapahtumien tiivistelmä — nykyisten taulukoiden ja havaintojen lisäksi.
- Kieli: seuraa UI-kieltä kuten nyt (fi/en; ei omaa asetusta).
- Kokoonpano: automaattinen tunnistus sensoridatasta + käyttäjän omat
  lisätiedot vapaana tekstinä (uusi asetus).
- Trendit: 7 pv vs 30 pv, vain selvät muutokset.
- Arkkitehtuuri: kokoava syöterecord, Build pysyy puhtaana funktiona.

## Generoidun tiedoston rakenne

1. **Otsikko + päivitysaika** (nykyinen).
2. **Johdanto AI:lle**: mikä tiedosto on ja miten sitä käytetään —
   data tulee Hardware Monitor -sovelluksesta (LibreHardwareMonitor,
   1 s lukemat koostettuna 5 s riveiksi, 30 pv historia); keskiarvo =
   tyypillinen taso, huippu = korkein hetkellinen lukema, varoitusraja =
   sovelluksen hälytysraja. Lopuksi ohje: "Käytä tätä kontekstina kun
   käyttäjä kysyy koneen lämpötiloista, suorituskyvystä tai
   vianetsinnästä."
3. **Kokoonpano**: CPU, GPU, emolevy, RAM yhteensä (GB), levyt,
   Windows-versio + käyttäjän lisätiedot jos asetettu. Samannimiset
   levyt ryhmitellään ("2 × Samsung SSD 860 EVO 1TB").
4. **Normaalitasot ja huiput (30 pv)**: nykyiset taulukot sellaisenaan
   (mittarit, levyt, tuulettimet).
5. **Trendit (7 pv vs 30 pv)**: raportoidaan vain selvät muutokset
   keskiarvoissa — lämpötilat (CPU, GPU, hotspot, levyt) ≥ 3 °C,
   CPU-kuorma ja RAM ≥ 10 %-yksikköä; suunta sanotaan (noussut/laskenut).
   Ei muutoksia → "Ei merkittäviä muutoksia." 7 pv dataa ei ole →
   todetaan ettei vertailua voi vielä tehdä. Tuulettimet eivät ole
   mukana trendeissä (RPM riippuu käyttöprofiilista).
6. **Tapahtumat (30 pv)**: nykyiset laskurit + uutena enintään 10
   viimeisintä WARNING/CRITICAL-tapahtumaa listana (aikaleima, taso,
   viesti), uusin ensin.
7. **Havainnot ja optimointiehdotukset** (nykyinen).

## Komponentit

### 1. MachineSpec + MachineSpecReader (Core/Insights, puhdas, TDD)

- `MachineSpec`-record: CpuName, GpuName, MotherboardName, RamTotalGb,
  levynimet (sellaisenaan, ryhmittely on builderin vastuulla),
  OsDescription, UserNotes. Puuttuva tieto → null/tyhjä, renderöidään "—".
- `MachineSpecReader.Read(hardwareGroups, osDescription, userNotes)`:
  poimii nimet HardwareGroup-listasta laitetyypin mukaan. RAM yhteensä
  = Memory Used + Memory Available -sensorien summa pyöristettynä
  kokonaisiin gigatavuihin (63,9 → 64). OS-versio annetaan parametrina,
  jotta luokka pysyy puhtaana (App lukee sen käyttöjärjestelmältä).

### 2. MachineInsightsInput + Build-refaktorointi (Core/Insights, TDD)

- `MachineInsightsInput`-record: Now, Spec, Stats30d, Stats7d, Events,
  Limits. `MachineInsightsBuilder.Build(input)` korvaa nykyisen
  moniparametrisen signatuurin (kutsupaikat ja testit päivitetään).
- Uudet osiot (johdanto, kokoonpano, trendit, tapahtumalista)
  StringBuilder-metodeina nykyiseen tapaan.

### 3. Asetus + UI (App)

- `AppSettings.InsightsNotes` (string, oletus tyhjä) +
  monirivinen tekstikenttä Asetukset → Yleiset -ryhmään
  ("Omat lisätiedot koneesta", vihjeteksti kertoo mihin tieto menee).
  WCAG AAA -kontrasti kuten muissa asetusteksteissä.
- Voimaan seuraavassa insights-kirjoituksessa (käynnistys + 30 min väli).

### 4. MainViewModel-datavirta

- `GetSampleStats` kutsutaan kahdesti: 30 pv ja 7 pv taaksepäin.
- Spec kootaan jo luetuista HardwareGroupeista; OS-kuvaus luetaan
  kerran käynnistyksessä (Windows 11 -nimi + build).

### 5. Lokalisointi

- Uudet avaimet molempiin resx:iin (myös identtiset en-arvot — puuttuva
  avain palautuu neutraaliin fi:hin) + Strings-accessorit käsin.
- Tapahtumalistan viestit ovat kannan dataa — näytetään
  tallennuskielellään, ei lokalisoida.

## Testaus (TDD)

- `MachineSpecReaderTests`: nimet poimitaan oikein, RAM-summa ja
  pyöristys, puuttuva laite → null.
- `MachineInsightsBuilderTests`-laajennukset: johdanto ja kokoonpano
  renderöityvät (ml. samannimisten levyjen ryhmittely "2 × ...");
  lisätiedot mukana vain kun asetettu; trendirivi näkyy
  vain kun kynnys ylittyy (molemmat suunnat); 7 pv datan puute
  käsitellään; tapahtumalistassa enintään 10 uusinta, vain
  WARNING/CRITICAL, uusin ensin. TestCulture kiinnittää fi:n.
- `SettingsValidator`/`SettingsViewModel`-testit InsightsNotes-kentälle
  siltä osin kuin validointia on (vapaa teksti — ei rajoituksia).

## Rajaukset

- Ei omaa kieliasetusta insights-tiedostolle.
- Ei tuulettimia trendeihin, ei FPS-dataa (PresentMon on jatkokehitysidea).
- Tiedoston polku ja kirjoitusrytmi eivät muutu
  (%LOCALAPPDATA%\HardwareMonitor\machine-insights.md; käynnistys + 30 min).
