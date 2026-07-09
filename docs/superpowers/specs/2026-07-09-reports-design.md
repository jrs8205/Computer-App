# Vaihe 7 — Raportit ja CSV-vienti (design)

Päivä: 9.7.2026. Perustuu määrittelyn lukuihin 20 ja 21 sekä käyttäjän
toiveeseen 9.7.2026: **vientien pitää olla selkokielisiä — ihmisen
ymmärrettävissä, ei pelkkää konedataa.**

## Osat

### 1. ReportBuilder (Core/Reports, puhdas, TDD)

"Luo raportti" tuottaa Markdown/TXT-raportin, joka luetaan Notepadissa
sellaisenaan — siksi **ei pipe-taulukoita**, vaan otsikot ja selkeät
lauseet. Jokainen luku selittää itse itsensä.

Rakenne:

1. **Otsikko + johdanto**: mikä tämä raportti on ja miten sitä luetaan.
2. **Yhteenveto**: Koneen tila + riskitaso (RiskAnalyzer) + selitys mitä
   tasot tarkoittavat, havainnot ja suositus.
3. **Arvot juuri nyt**: jokainen mittari muodossa
   "CPU-lämpötila: 45 °C — kunnossa (varoitusraja 85 °C)".
4. **Viimeiset 24 tuntia**: huiput selitettyinä
   ("korkein lukema 58 °C, selvästi alle varoitusrajan 85 °C").
5. **Viimeiset 30 päivää**: normaalitasot ("kone on tyypillisesti ...").
6. **Tapahtumat 24 h**: varoitukset/kriittiset aikoineen (viestit ovat jo
   suomeksi), tai "ei tapahtumia — hyvä merkki".
7. **Sanasto**: WHEA, Kernel-Power 41, hotspot, TDR ym. yhdellä lauseella.

Syötteet: RiskAssessment, KeyMetrics + MetricStates (nyt-arvot),
SampleStats 24 h + 30 pv, EventRow-lista 24 h, ThresholdSettings.

### 2. CsvExporter (Core/Reports, puhdas, TDD)

Vienti Exceliin (luku 21). Ihmisluettavuus CSV:ssä = **suomenkieliset
sarakeotsikot yksiköineen** ja **suomalaisen Excelin muoto**: erotin `;`,
desimaalipilkku (kulttuuri annetaan parametrina). Rivit = 5 s koosterivit
24 h ajalta; levy- ja tuuletinsarakkeet pivotoidaan nimillä
("Levy Samsung 970 EVO Plus lämpö °C (max)").

### 3. HistoryDb.ReadSampleRows(since) (TDD)

Palauttaa koosterivit lapsiriveineen CSV:tä varten (SampleRow +
DiskSampleValue/FanSampleValue nimillä).

### 4. UI

Yläpalkkiin napit **"Luo raportti…"** ja **"Vie CSV…"**: SaveFileDialog
(oletusnimi "Jarjestelmaraportti-2026-07-09.txt" / "Sensorihistoria-24h-….csv"),
tallennuksen jälkeen tiedosto avataan oletusohjelmassa. Raportin oletus-
muoto .txt (aukeaa Notepadiin), vaihtoehtona .md.

## Rajaukset

- JSON- ja PDF-vienti myöhemmin (luku 20 "myöhemmin").
- Ylitysten kestolaskenta ("RAM yli 90 %: 12 min") jätetään myöhemmäksi —
  raportissa käytetään tapahtumamääriä ja huippuja.
- CSV kattaa 24 h (kiinteä); aikavälin valinta tulee asetussivun myötä.
