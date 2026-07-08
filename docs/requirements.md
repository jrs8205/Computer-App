# Windows 11 Hardware Monitor - ohjelman aloitusmäärittely

Päivitetty: 8.7.2026

## 1. Ohjelman idea

Tavoitteena on tehdä Windows 11 -tietokoneelle oma ohjelma, joka näyttää reaaliajassa tietokoneen rasitukseen, lämpöihin, virrankulutukseen, tuulettimiin, muistiin, levyihin ja näytönohjaimeen liittyviä tietoja.

Ohjelman ei ole tarkoitus olla vain tavallinen “lämpömittari”, vaan sen tärkein erottuva ominaisuus olisi:

> Ohjelma seuraa koneen tilaa, lokittaa kriittiset hetket ja auttaa arvioimaan, onko jokin komponentti ollut rajoilla tai voinut aiheuttaa kaatumisia.

Esimerkkejä ohjelman käyttötarkoituksista:

- nähdä CPU:n ja GPU:n lämpötilat
- nähdä prosessorin ja näytönohjaimen rasitus
- nähdä muistin käyttö
- nähdä levyjen lämpötilat ja kunto
- nähdä tuulettimien nopeudet
- nähdä virrankulutus, jos saatavilla
- havaita throttlaus eli suorituskyvyn rajoittuminen
- havaita WHEA-, Kernel-Power-, levy- ja näyttöajurivirheitä Windowsin lokista
- tallentaa sensorihistoriaa
- tehdä yhteenveto, oliko kone lähellä kriittisiä rajoja
- auttaa selvittämään jälkikäteen, miksi kone saattoi kaatua

Ohjelma voisi olla samantyyppinen kuin HWiNFO, Libre Hardware Monitor, Open Hardware Monitor, MSI Afterburner tai Fan Control, mutta omalla painotuksella: selkeä lokitus, riskianalyysi ja kaatumisten jälkiselvitys.

---

## 2. Mitä dataa Windows 11 -tietokoneesta voidaan kerätä?

Kaikkea tietoa ei saada yhdestä lähteestä. Dataa täytyy kerätä useista eri paikoista:

1. Windowsin omista rajapinnoista
2. emolevyn sensoripiireistä
3. prosessorin antureista
4. näytönohjaimen ajureista ja valmistajakohtaisista rajapinnoista
5. levyjen SMART- ja NVMe-tiedoista
6. Windows Event Logista
7. mahdollisesti ulkoisista laitteista, kuten älyvirtalähteistä tai älypistorasiasta

---

## 3. Prosessori eli CPU

CPU:sta voidaan yleensä kerätä seuraavia tietoja:

| Tieto | Saatavuus | Huomio |
|---|---|---|
| CPU-käyttö prosentteina | Helppo | Windowsilta |
| Käyttö per ydin / säie | Helppo | Windowsilta |
| Kellotaajuudet | Yleensä saatavilla | Voi vaihdella nopeasti |
| CPU-lämpötila | Usein saatavilla | Vaatii sensorikirjaston |
| CPU Package -lämpö | Usein saatavilla | Tärkeä kokonaislämpö |
| CPU Package Power, W | Usein saatavilla | Intel/AMD, riippuu tuesta |
| CPU-jännite | Riippuu emolevystä | Ei aina tarkka |
| Thermal throttling | Usein havaittavissa | Erittäin tärkeä |
| Power limit throttling | Usein havaittavissa | Näkee rajoittaako tehoraja |
| Ydinkohtaiset lämpötilat | Usein | Riippuu prosessorista |
| Ydinkohtaiset kellot | Usein | Hyödyllinen rasituksessa |

Tärkeitä CPU-havaintoja:

- CPU:n hetkellinen korkea lämpö ei aina tarkoita ongelmaa.
- Kesto on tärkeämpi kuin yksittäinen piikki.
- Jos CPU on pitkään yli 90–95 °C ja kellot laskevat, kyse voi olla jäähdytysongelmasta tai throttlingista.
- Jos WHEA-virheitä tulee samaan aikaan, kyse voi olla myös epävakaudesta.

---

## 4. Muisti eli RAM

RAM-muistista saadaan helposti perusdataa Windowsilta.

| Tieto | Saatavuus | Huomio |
|---|---|---|
| Käytetty RAM | Helppo | Windowsilta |
| Vapaa RAM | Helppo | Windowsilta |
| RAM kokonaismäärä | Helppo | Windowsilta |
| Käyttöprosentti | Helppo | Laskettavissa |
| Pagefile-käyttö | Helppo | Tärkeä, jos RAM loppuu |
| Standby/cache memory | Saatavilla | Hyödyllinen tarkempiin näkymiin |
| Muistin nopeus | Yleensä saatavilla | WMI/SPD-tiedot |
| RAM-lämpötila | Vain joissain muisteissa | Esim. osassa DDR5-muisteja |

Tärkeitä RAM-havaintoja:

- RAM 90–95 % pitkään voi aiheuttaa hidastelua.
- Jos RAM loppuu, Windows alkaa käyttää sivutustiedostoa.
- Iso pagefile-kuorma voi aiheuttaa jäätymistä, mutta ei aina varsinaista kaatumista.
- Jos WHEA-virheitä liittyy muistiin, kyse voi olla epävakaasta muistista tai XMP/EXPO-asetuksista.

---

## 5. Näytönohjain eli GPU

GPU:sta voi saada paljon tietoa, etenkin NVIDIA- ja AMD-korteista.

| Tieto | Saatavuus | Huomio |
|---|---|---|
| GPU-käyttö % | Helppo | Windowsilta tai GPU-rajapinnasta |
| VRAM-käyttö | Helppo | Tärkeä peleissä ja AI-kuormissa |
| GPU-lämpötila | Yleensä helppo | Peruslämpö |
| GPU hotspot | Usein saatavilla | Usein tärkeämpi kuin peruslämpö |
| VRAM-lämpötila | Riippuu kortista | Erityisesti GDDR6X-korteissa tärkeä |
| GPU-teho watteina | Usein saatavilla | NVIDIA/AMD |
| GPU-kellot | Yleensä saatavilla | Core ja memory clock |
| GPU-jännite | Usein saatavilla | Valmistajakohtainen |
| Tuulettimet RPM/% | Usein saatavilla | Joissain korteissa 0 RPM idle-tilassa |
| Power limit | Usein saatavilla | Hyödyllinen analyysiin |
| Thermal throttling | Usein havaittavissa | Tärkeä |
| Driver reset / ajurikaatumiset | Windows Event Logista | Erittäin tärkeä kaatumisanalyysissä |

GPU:n rajapintoja:

- NVIDIA: NVML
- AMD: ADLX / ADL
- Intel: Windowsin GPU-laskurit ja Intelin omat rajapinnat
- Yleiskäyttö: LibreHardwareMonitorLib

Tärkeitä GPU-havaintoja:

- GPU:n peruslämpö voi näyttää hyvältä, vaikka hotspot olisi liian korkea.
- GPU hotspot yli 100–105 °C pitkään on huomioitava.
- Näyttöajurin kaatumiset voivat näkyä Windowsin lokissa.
- VRAM:n loppuminen voi aiheuttaa pelien tai sovellusten kaatumisia.
- Jos GPU:n tuulettimet eivät pyöri kuumana, se on kriittinen havainto.

---

## 6. Emolevy ja sensoripiirit

Emolevystä saatava tieto vaihtelee paljon valmistajan ja sensoripiirin mukaan.

Mahdollisia tietoja:

| Tieto | Saatavuus | Huomio |
|---|---|---|
| Emolevyn lämpötila | Usein | Yleinen sensorilukema |
| Chipset-lämpö | Usein | Varsinkin pienissä koteloissa hyödyllinen |
| VRM-lämpötila | Joissain emolevyissä | Tärkeä kovassa CPU-kuormassa |
| CPU socket -lämpö | Joissain | Eri kuin CPU core/package |
| Tuuletinnopeudet RPM | Usein | CPU fan, case fan, pump |
| Pumpun nopeus | Jos liitetty emoon | AIO-vesijäähdytys |
| 12V, 5V ja 3.3V jännitteet | Usein | Voi olla epätarkka |
| VCore / SoC voltage | Usein | Emolevykohtainen |
| Fan curve -tiedot | Vaikea | Valmistajakohtainen |
| RGB-tiedot | Vaikea | Valmistajakohtainen |

Sensoripiirejä voivat olla esimerkiksi:

- Nuvoton
- ITE
- Fintek
- muita valmistajakohtaisia ratkaisuja

Tärkeä huomio:

Emolevyjen sensorit eivät ole täysin standardoituja. Siksi ohjelman kannattaa käyttää valmista kirjastoa, kuten LibreHardwareMonitorLib, eikä yrittää heti itse toteuttaa kaikkien emolevyjen matalan tason sensoritukea.

---

## 7. Levyt: SSD, HDD ja NVMe

Levyistä voidaan saada sekä käyttö- että terveystietoja.

| Tieto | Saatavuus | Huomio |
|---|---|---|
| Levyn käyttöaste | Helppo | Windowsilta |
| Luku-/kirjoitusnopeus | Helppo | Windowsilta |
| Levyjonon pituus | Saatavilla | Hyödyllinen hidasteluanalyysissä |
| Lämpötila | Usein | SMART/NVMe |
| Terveysprosentti | Usein | SSD/NVMe |
| Kirjoitettu data yhteensä | Usein | TBW |
| Power-on hours | Usein | Käyttötunnit |
| Virheet / bad sectors | Usein | SMART |
| NVMe-varoitukset | Usein | Tärkeä |
| Jäljellä oleva käyttöikä | Usein | Valmistajasta riippuva |

Tärkeitä levyhavaintoja:

- NVMe-levyt voivat kuumentua nopeasti.
- NVMe yli 70 °C on varoitus.
- NVMe 80–85 °C on jo kriittinen tai ainakin vakava huomio.
- SMART-varoitukset kannattaa nostaa selvästi esiin.
- Levyn virheet voivat selittää jäätymisiä, ohjelmien kaatumisia tai Windowsin ongelmia.

---

## 8. Verkko

Verkosta voi kerätä esimerkiksi:

| Tieto | Saatavuus |
|---|---|
| Latausnopeus | Helppo |
| Lähetysnopeus | Helppo |
| Verkkokortin nimi | Helppo |
| IP-osoitteet | Helppo |
| Wi-Fi signaalitaso | Saatavilla |
| Ping | Itse mitattavissa |
| Pakettihävikki | Itse mitattavissa |
| DNS-viive | Itse mitattavissa |

Verkkodata ei yleensä kerro koneen kaatumisesta, mutta se voi auttaa selvittämään nettipätkimistä, pelien lagia tai yhteysongelmia.

---

## 9. Kannettavat tietokoneet ja akku

Läppäreissä voidaan usein saada:

| Tieto | Saatavuus | Huomio |
|---|---|---|
| Akun prosentti | Helppo |
| Lataus/purkaus watteina | Usein |
| Akun kuluminen | Usein |
| Cycle count | Riippuu laitteesta |
| Akun lämpötila | Riippuu laitteesta |
| Laturin tila | Helppo |
| Virransäästötila | Helppo |

Läppäreissä suurin haaste on tuulettimien ja valmistajakohtaisten EC-sensorien lukeminen. Monet valmistajat eivät paljasta kaikkea standardirajapinnoilla.

---

## 10. Mitä ei yleensä saa tarkasti?

Kaikkea ei voi saada ohjelmallisesti varmasti.

| Tieto | Miksi hankala |
|---|---|
| Koko koneen todellinen seinästä otettu kulutus | Vaatii älypistorasian tai mittaavan virtalähteen |
| Jokaisen komponentin tarkka sähkönkulutus | Kaikissa komponenteissa ei ole mittausta |
| Prosessikohtainen todellinen sähkönkulutus | Usein vain arvioitavissa |
| Kaikkien tuulettimien ohjaus | Emolevy- ja valmistajakohtainen |
| Läppärien tuulettimet | Usein EC-ohjaimen takana |
| Kaikki lämpöanturit | Kaikkia ei julkaista käyttöjärjestelmälle |
| RGB-ohjaus | Valmistajakohtainen sekasotku |
| PSU:n sisäiset tiedot | Vain joissain digitaalisissa virtalähteissä |

Ohjelma voi arvioida kokonaiskulutusta esimerkiksi CPU + GPU + levyt + muut, mutta se ei ole sama asia kuin seinästä mitattu todellinen kulutus.

---

## 11. Libre Hardware Monitor

Libre Hardware Monitor on avoimen lähdekoodin ohjelma ja kirjasto.

Sitä kannattaa käyttää ohjelman pohjana, koska se osaa lukea paljon sensoridataa valmiiksi.

Hyödyt:

- avoin lähdekoodi
- tukee paljon eri laitteita
- tarjoaa LibreHardwareMonitorLib-kirjaston
- antaa CPU-, GPU-, levy-, tuuletin-, jännite- ja lämpötilatietoja
- helpottaa MVP-version tekemistä huomattavasti

Lisenssi:

- Libre Hardware Monitor käyttää MPL-2.0-lisenssiä.
- Sitä voi yleensä käyttää myös kaupallisessa ohjelmassa.
- Jos muuttaa itse MPL-lisensoituja tiedostoja, muutokset täytyy yleensä julkaista samalla lisenssillä.
- Oman ohjelman käyttöliittymä ja oma koodi voivat olla erillisiä, kunhan lisenssiehdot huomioidaan.

Tärkeä huomio:

Virallinen lähde kannattaa pitää GitHub-repona, ei satunnaisina lataussivuina.

---

## 12. Admin-oikeudet ja turvallisuus

Perusdataa voi saada ilman admin-oikeuksia:

- CPU-käyttö
- RAM-käyttö
- levykuorma
- verkkokuorma
- prosessit
- osa GPU-kuormasta

Mutta matalan tason sensoridata tarvitsee usein admin-oikeudet:

- CPU-lämpötilat
- emolevyn sensorit
- tuulettimet
- jännitteet
- osa GPU- ja levyantureista

Suositeltu toimintatapa:

1. Ohjelma toimii perustilassa ilman admin-oikeuksia.
2. Jos käyttäjä haluaa laajemmat sensorit, ohjelma pyytää admin-oikeuksia.
3. Ohjelma kertoo selkeästi, miksi oikeuksia tarvitaan.
4. Ohjelma ei tee tuulettimien tai jännitteiden ohjausta ensimmäisessä versiossa.
5. Ohjelma keskittyy ensin vain lukemiseen ja lokitukseen.

Turvallisuussyistä kannattaa välttää vanhoja epäluotettavia kernel-ajureita, jos mahdollista.

---

## 13. Lokitus: ohjelman tärkein ominaisuus

Ohjelmaan kannattaa rakentaa lokitus aivan alusta asti.

Lokituksen tarkoitus:

- tallentaa koneen tilaa rasituksen aikana
- havaita kriittiset lämpö- ja kuormatilanteet
- auttaa selvittämään jälkikäteen, mikä oli pielessä ennen kaatumista
- näyttää käyttäjälle selkeästi, oliko jokin komponentti rajoilla
- erottaa hetkelliset piikit pitkäkestoisista ongelmista

Ohjelmaan kannattaa tehdä kaksi lokia:

1. jatkuva sensoriloki
2. tapahtumaloki

---

## 14. Jatkuva sensoriloki

Jatkuva sensoriloki tallentaa säännöllisin väliajoin mitattavia arvoja.

Suositeltu tallennusväli:

- UI-päivitys: 1 sekunti
- normaali sensoriloki: 5 sekuntia
- kevyt taustaloki: 10–30 sekuntia
- kriittinen tapahtuma: heti

Tallennettavia arvoja:

| Arvo | Miksi |
|---|---|
| CPU-lämpö | Ylikuumenemisen havaitseminen |
| CPU-käyttö | Rasituksen tunnistus |
| CPU-teho W | Virtarajan ja kuorman arviointi |
| CPU-kellot | Throttlingin havaitseminen |
| CPU throttling | Suora varoitus |
| GPU-lämpö | Näytönohjaimen lämpö |
| GPU hotspot | Kriittinen GPU-mittari |
| GPU VRAM-lämpö | Jos saatavilla |
| GPU-käyttö | Rasitus |
| GPU-teho W | Kuorma ja virtapiikit |
| GPU-kellot | Throttlingin havaitseminen |
| GPU-tuulettimet | Jäähdytyksen toiminta |
| RAM-käyttö | Muistin loppuminen |
| Pagefile-käyttö | RAM-paine |
| Levyjen lämpö | SSD/NVMe ylikuumeneminen |
| Levyjen aktiivisuus | Jäätymisen/hidastelun analyysi |
| SMART/NVMe-varoitukset | Levyongelmat |
| Emolevyn lämpö | Kotelon/emon tila |
| VRM-lämpö | CPU-virransyötön tila |
| 12V/5V/3.3V | Virtalähteen mahdollinen ongelma |
| Tuuletinnopeudet | Tuuletinviat |

---

## 15. Tapahtumaloki

Tapahtumaloki tallentaa vain merkittävät tapahtumat.

Esimerkkejä:

```text
2026-07-08 18:31:22 WARNING CPU temperature high: 92°C
2026-07-08 18:31:25 WARNING CPU thermal throttling detected
2026-07-08 18:32:04 CRITICAL GPU hotspot 105°C
2026-07-08 18:34:10 WARNING RAM usage 94%
2026-07-08 18:35:40 ERROR NVMe temperature 82°C
2026-07-08 18:40:12 CRITICAL CPU fan speed 0 RPM while CPU temp 88°C
```

Tapahtumatyypit:

- INFO
- WARNING
- CRITICAL
- ERROR

Tapahtumalokiin kannattaa tallentaa:

- timestamp
- komponentti
- sensorin nimi
- arvo
- raja-arvo
- kesto
- vakavuus
- selkokielinen selitys

Esimerkki JSONL-muodossa:

```json
{"time":"2026-07-08T18:31:22","level":"WARNING","component":"CPU","sensor":"Package Temperature","value":92,"unit":"°C","threshold":90,"message":"CPU temperature high"}
```

---

## 16. Varoitus- ja kriittiset rajat

Rajat pitää antaa käyttäjän muuttaa asetuksista. Oletusrajat voivat olla esimerkiksi:

| Kohde | Varoitus | Kriittinen |
|---|---:|---:|
| CPU lämpö | 85 °C | 95–100 °C |
| GPU lämpö | 80–85 °C | 90–95 °C |
| GPU hotspot | 95 °C | 105–110 °C |
| NVMe SSD | 70 °C | 80–85 °C |
| RAM käyttö | 85 % | 95 % |
| Pagefile käyttö | korkea jatkuvasti | erittäin korkea + hidas levy |
| CPU fan | 0 RPM kuumana | 0 RPM + CPU yli 80 °C |
| GPU fan | 0 RPM kuumana | 0 RPM + GPU yli 80 °C |
| 12V jännite | alle 11.6V | alle 11.4V |
| CPU/GPU throttling | havaittu | jatkuvaa |

Tärkeä sääntö:

Yksittäinen piikki ei aina ole ongelma. Ohjelman pitää huomioida kesto.

Esimerkkejä:

- CPU 92 °C 2 sekuntia: ei välttämättä ongelma
- CPU 92 °C 15 minuuttia + kellot laskevat: ongelma
- GPU hotspot 105 °C pitkään: selvä varoitus
- RAM 96 % + pagefile kasvaa + ohjelmat jäätyvät: ongelma
- Tuuletin 0 RPM kuumana: kriittinen

---

## 17. “Ennen kaatumista” -puskuri

Ohjelmaan kannattaa tehdä ominaisuus, joka auttaa selvittämään kaatumisia.

Idea:

- ohjelma pitää tallessa viimeiset 10 minuuttia sensoridataa
- viimeisin tila tallennetaan erilliseen tiedostoon, esimerkiksi `last_state.json`
- ohjelma merkitsee normaalin sulkemisen
- seuraavalla käynnistyksellä ohjelma tarkistaa, suljettiinko se normaalisti
- jos ei suljettu, se näyttää ilmoituksen: “edellinen istunto päättyi yllättäen”

Esimerkki käyttäjälle:

```text
Edellinen istunto päättyi yllättäen.

Viimeisin tallennettu tila:
CPU: 97 °C
GPU hotspot: 106 °C
RAM: 94 %
NVMe: 78 °C
WHEA-virheitä havaittu: 2
Näyttöajurivirheitä: 1

Mahdollinen syy:
Kone oli kovassa lämpö- ja muistirasituksessa ennen kaatumista.
```

Tämä olisi erittäin hyödyllinen ominaisuus, koska moni kaatuminen tapahtuu niin, ettei käyttäjä ehdi nähdä reaaliaikaista tilannetta.

---

## 18. Windows Event Login lukeminen

Sensoridatan lisäksi ohjelman kannattaa lukea Windowsin tapahtumalokia.

Tärkeimmät lähteet:

| Lähde | Merkitys |
|---|---|
| Kernel-Power 41 | Kone sammui yllättäen tai kaatui |
| WHEA-Logger | CPU/RAM/PCIe/rautaongelmat |
| Display driver errors | GPU-ajuri kaatui |
| BugCheck | BSOD-tiedot |
| Disk | Levyvirheet |
| Ntfs | Tiedostojärjestelmävirheet |
| storahci | SATA/levyohjainongelmat |
| nvme | NVMe-ongelmat |
| Application Error | Ohjelmien kaatumiset |
| Thermal events | Lämpörajoitukset, jos saatavilla |

Erityisen tärkeät:

### Kernel-Power 41

Tarkoittaa, että kone on sammunut odottamatta. Se ei yksin kerro syytä, mutta kertoo että edellinen sammutus ei ollut normaali.

### WHEA-Logger

Voi viitata rautaongelmaan tai epävakauteen:

- CPU
- RAM
- PCIe
- GPU
- emolevy
- virtalähde
- liian kova ylikellotus
- liian pieni jännite
- epävakaa XMP/EXPO

### Display driver error

Voi viitata:

- GPU-ajurin kaatumiseen
- GPU:n epävakauteen
- liian korkeisiin lämpöihin
- VRAM-ongelmaan
- liian kovaan ylikellotukseen
- pelin/sovelluksen ajuriongelmaan

---

## 19. Riskipisteet ja selkokielinen analyysi

Ohjelman ei pitäisi vain näyttää raakadataa. Sen pitäisi tehdä käyttäjälle selkeä yhteenveto.

Esimerkki hyvästä tilasta:

```text
Koneen tila: Hyvä
Riskitaso: Matala

Huomiot:
- CPU lämpötila kävi korkeimmillaan 82 °C
- GPU hotspot kävi korkeimmillaan 94 °C
- RAM käyttö nousi korkeimmillaan 78 %
- Ei WHEA-virheitä
- Ei yllättäviä sammutuksia
```

Esimerkki varoitustilasta:

```text
Koneen tila: Varoitus
Riskitaso: Kohonnut

Havainnot:
- GPU hotspot oli yli 105 °C yhteensä 18 minuuttia
- Näytönohjaimen kellotaajuus laski rasituksessa
- Windows Event Logissa 2 näyttöajurivirhettä
- Yksi Kernel-Power 41 -tapahtuma viimeisen 24 tunnin aikana
```

Esimerkki kriittisestä tilasta:

```text
Koneen tila: Kriittinen
Riskitaso: Korkea

Havainnot:
- CPU oli yli 95 °C yhteensä 22 minuuttia
- CPU throttling havaittiin useita kertoja
- CPU-tuulettimen nopeus oli 0 RPM, vaikka CPU oli yli 85 °C
- Edellinen istunto päättyi yllättäen

Suositus:
Tarkista CPU-jäähdytys, tuulettimen liitäntä ja lämpötahnat.
```

Riskipisteiden laskeminen voisi perustua esimerkiksi:

- lämpötilan vakavuuteen
- rajan ylityksen kestoon
- throttlingiin
- tuuletinongelmiin
- WHEA-virheisiin
- Kernel-Power-tapahtumiin
- levyvirheisiin
- GPU-ajurikaatumisiin
- muistin loppumiseen

---

## 20. Raporttiominaisuus

Ohjelmaan kannattaa tehdä nappi:

> Luo raportti

Raportti voisi sisältää:

```text
Järjestelmäraportti 8.7.2026

Yhteenveto:
Koneen tila: Varoitus
Riskitaso: Kohonnut

Maksimiarvot:
CPU max: 93 °C
GPU max: 84 °C
GPU hotspot max: 101 °C
RAM max: 91 %
NVMe max: 76 °C

Varoitukset:
- CPU yli 90 °C: 4 kertaa
- RAM yli 90 %: 12 minuuttia
- NVMe yli 70 °C: 8 minuuttia
- WHEA-virheitä: 0
- Yllättäviä sammutuksia: 0

Arvio:
Kone on ollut lähellä lämpörajaa, mutta selviä rautavirheitä ei löytynyt.
```

Raportin vientimuodot:

- TXT
- Markdown
- CSV
- JSON
- myöhemmin PDF

---

## 21. Tallennusmuodot

Suositeltu tallennustapa:

| Data | Tallennusmuoto | Miksi |
|---|---|---|
| Sensorihistoria | SQLite | Hyvä pitkäaikaiseen dataan |
| Tapahtumat | JSONL tai SQLite | Helppo käsitellä |
| Debug-loki | TXT | Helppo lukea |
| Vienti Exceliin | CSV | Yhteensopiva |
| Raportti | Markdown/TXT | Helppo jakaa |
| Asetukset | JSON | Yksinkertainen |

Paras yhdistelmä:

- SQLite sensorihistorialle
- JSON tai SQLite tapahtumille
- JSON asetuksille
- Markdown/TXT raporteille
- CSV vientiin

---

## 22. Suositeltu tekninen toteutus

Sopiva tekninen pino:

| Osa | Suositus |
|---|---|
| Kieli | C# |
| Käyttöliittymä | WPF tai WinUI 3 |
| Sensorit | LibreHardwareMonitorLib |
| Windows-data | PerformanceCounter, WMI/CIM, PDH |
| Event Log | System.Diagnostics.Eventing.Reader |
| GPU-lisätuki | NVML NVIDIAlle |
| Tietokanta | SQLite |
| Graafit | LiveCharts2 |
| Taustapalvelu | myöhemmin Windows Service |
| Tray icon | WPF/WinUI tray-ratkaisu |
| Asetukset | JSON |
| Raportit | Markdown/TXT/CSV |

Ensimmäiseen versioon WPF voi olla helpompi ja nopeampi kuin WinUI 3. WinUI 3 näyttää modernimmalta, mutta voi tuoda enemmän projektirakenteen ja paketoinnin monimutkaisuutta.

---

## 23. Mahdollinen ohjelman rakenne

Projektin rakenne voisi olla esimerkiksi:

```text
HardwareMonitorApp/
  src/
    HardwareMonitor.App/
      Views/
      ViewModels/
      Services/
      Models/
      App.xaml
      MainWindow.xaml

    HardwareMonitor.Core/
      Sensors/
      Logging/
      Analysis/
      EventLog/
      Reports/
      Settings/
      Models/

    HardwareMonitor.Tests/
      SensorTests/
      AnalysisTests/
      LoggingTests/

  docs/
    requirements.md
    architecture.md
    sensor-list.md
    thresholds.md

  data/
    logs/
    reports/
```

Tärkeät osat:

### SensorService

Vastaa sensorien lukemisesta.

### LoggingService

Tallentaa jatkuvan sensorilokin.

### EventLogService

Lukee Windowsin tapahtumalokia.

### RiskAnalyzer

Laskee riskitason ja tekee selkokielisiä havaintoja.

### ReportService

Luo raportin.

### SettingsService

Tallentaa käyttäjän asetukset ja raja-arvot.

### NotificationService

Näyttää ilmoituksia kriittisistä tapahtumista.

---

## 24. MVP eli ensimmäinen versio

Ensimmäisessä versiossa ei kannata yrittää tehdä kaikkea.

Hyvä MVP:

1. C# WPF/WinUI-ohjelma
2. LibreHardwareMonitorLib mukaan
3. Näytä reaaliajassa:
   - CPU käyttö
   - CPU lämpö
   - CPU kellot
   - GPU käyttö
   - GPU lämpö
   - GPU hotspot, jos saatavilla
   - RAM käyttö
   - levyjen lämpötilat
   - tuulettimet, jos saatavilla
4. Lisää SQLite-lokitus 5 sekunnin välein
5. Lisää tapahtumaloki
6. Lisää varoitusrajat:
   - CPU lämpö
   - GPU lämpö
   - GPU hotspot
   - RAM käyttö
   - NVMe lämpö
   - fan 0 RPM kuumana
7. Lisää käynnistyksessä tarkistus:
   - suljettiinko ohjelma normaalisti
   - näkyykö Kernel-Power 41
   - näkyykö WHEA-virheitä
8. Lisää “Luo raportti” -toiminto Markdown/TXT-muodossa

MVP:n ulkopuolelle kannattaa jättää:

- tuulettimien ohjaus
- RGB-ohjaus
- ylikellotus
- jännitteiden muuttaminen
- automaattinen säätö
- monimutkainen overlay
- pilvisynkronointi

Ensimmäinen versio lukee, näyttää, lokittaa ja analysoi. Se ei muuta koneen asetuksia.

---

## 25. Käyttöliittymäidea

Päänäkymä:

- koneen tila
- riskitaso
- CPU-kortti
- GPU-kortti
- RAM-kortti
- levyt-kortti
- tuulettimet-kortti
- tapahtumat-kortti

Sivut:

1. Dashboard
2. CPU
3. GPU
4. RAM
5. Levyt
6. Emolevy / tuulettimet
7. Lokit
8. Windows-tapahtumat
9. Raportit
10. Asetukset

Dashboard voisi näyttää esimerkiksi:

```text
Koneen tila: Hyvä
Riskitaso: Matala

CPU: 54 °C / 18 %
GPU: 61 °C / 34 %
RAM: 42 %
NVMe: 48 °C

Ei kriittisiä tapahtumia viimeisen 24 tunnin aikana.
```

---

## 26. Hälytykset ja ilmoitukset

Ohjelma voisi antaa ilmoituksia:

- CPU liian kuuma
- GPU liian kuuma
- GPU hotspot liian kuuma
- NVMe liian kuuma
- RAM lähes täynnä
- tuuletin pysähtynyt kuumana
- WHEA-virhe havaittu
- näyttöajuri kaatui
- kone sammui edellisellä kerralla yllättäen

Ilmoituksissa pitää välttää turhaa spämmäämistä.

Hyvä sääntö:

- samaa varoitusta ei näytetä uudelleen esimerkiksi 5 minuuttiin
- kriittinen varoitus näytetään heti
- tapahtuma tallennetaan aina lokiin

---

## 27. Jatkokehitysideat

Kun perusversio toimii, voidaan lisätä:

### Graafit

- viimeiset 10 minuuttia
- viimeinen tunti
- viimeiset 24 tuntia
- viimeiset 7 päivää

### Overlay

- pieni always-on-top ikkuna
- pelin päälle FPS/CPU/GPU/RAM
- tämä on vaikeampi, kannattaa jättää myöhemmäksi

### Automaattinen raportti kaatumisen jälkeen

- seuraavalla käynnistyksellä ohjelma tekee raportin automaattisesti
- raportti kertoo viimeiset sensorit ennen yllättävää sammutusta

### Syvempi GPU-tuki

- NVIDIA NVML
- AMD ADLX
- Intel-tuki

### Tuulettimien ohjaus

Tämä kannattaa tehdä vasta myöhemmin, koska se on riskialttiimpi ominaisuus.

### Älypistorasia / virtamittari

Jos halutaan todellinen seinästä mitattu kulutus, ohjelma voisi tukea esimerkiksi älypistorasioita tai digitaalisia virtalähteitä.

---

## 28. Tärkeät suunnitteluperiaatteet

1. Lue ensin, älä ohjaa.
2. Tee ohjelmasta turvallinen.
3. Älä vaadi admin-oikeuksia kaikkeen.
4. Näytä perusdata ilman adminia.
5. Pyydä admin vain laajempia sensoreita varten.
6. Lokita kriittiset tapahtumat selkeästi.
7. Kerro käyttäjälle selkokielellä, mitä havaittiin.
8. Erottele piikit ja pitkäkestoiset ongelmat.
9. Tee raportti helposti jaettavaksi.
10. Älä tallenna dataa liian tiheästi turhaan.
11. Anna käyttäjän muuttaa raja-arvoja.
12. Älä tee automaattisia säätöjä ensimmäisessä versiossa.

---

## 29. Esimerkkiasetukset

```json
{
  "logging": {
    "sensorIntervalSeconds": 5,
    "uiRefreshSeconds": 1,
    "keepHistoryDays": 30
  },
  "thresholds": {
    "cpuWarningTemp": 85,
    "cpuCriticalTemp": 95,
    "gpuWarningTemp": 85,
    "gpuCriticalTemp": 95,
    "gpuHotspotWarningTemp": 95,
    "gpuHotspotCriticalTemp": 105,
    "nvmeWarningTemp": 70,
    "nvmeCriticalTemp": 82,
    "ramWarningPercent": 85,
    "ramCriticalPercent": 95
  },
  "alerts": {
    "showDesktopNotifications": true,
    "cooldownMinutes": 5
  }
}
```

---

## 30. Esimerkki sensoririvistä tietokannassa

```text
timestamp: 2026-07-08T18:30:00
cpu_usage_percent: 78
cpu_temp_c: 89
cpu_package_power_w: 112
cpu_clock_mhz: 4850
gpu_usage_percent: 96
gpu_temp_c: 83
gpu_hotspot_c: 101
gpu_power_w: 275
ram_usage_percent: 81
nvme_temp_c: 68
risk_level: warning
```

---

## 31. Esimerkki analyysisäännöistä

```text
Jos CPU lämpötila on yli 90 °C yli 10 minuuttia:
  -> WARNING: CPU has been running hot for a long time.

Jos CPU lämpötila on yli 95 °C ja kellotaajuus laskee:
  -> CRITICAL: CPU thermal throttling likely.

Jos GPU hotspot on yli 105 °C yli 5 minuuttia:
  -> CRITICAL: GPU hotspot temperature is too high.

Jos RAM käyttö on yli 95 % ja pagefile kasvaa:
  -> WARNING: System memory pressure is high.

Jos CPU fan on 0 RPM ja CPU yli 80 °C:
  -> CRITICAL: CPU fan may have stopped.

Jos Kernel-Power 41 löytyy edellisen istunnon jälkeen:
  -> WARNING: Previous shutdown was unexpected.

Jos WHEA-Logger virheitä löytyy:
  -> WARNING/CRITICAL depending on count and type.
```

---

## 32. Kehitysjärjestys

Suositeltu eteneminen:

### Vaihe 1: Perusprojekti

- Luo C# WPF- tai WinUI-projekti.
- Lisää LibreHardwareMonitorLib.
- Tee ensimmäinen näkymä, jossa näkyy sensorilista.

### Vaihe 2: Sensorien ryhmittely

- CPU
- GPU
- RAM
- levyt
- emolevy
- tuulettimet

### Vaihe 3: Lokitus

- SQLite-tietokanta
- sensoririvit 5 sekunnin välein
- tapahtumaloki

### Vaihe 4: Raja-arvot

- CPU/GPU/RAM/NVMe
- käyttäjän asetukset
- varoitukset

### Vaihe 5: Windows Event Log

- Kernel-Power
- WHEA-Logger
- Display driver
- Disk/Ntfs/NVMe
- BugCheck

### Vaihe 6: Riskianalyysi

- pisteytys
- selkokielinen yhteenveto
- kriittisten hetkien lista

### Vaihe 7: Raportointi

- Markdown/TXT-raportti
- CSV-vienti
- myöhemmin PDF

### Vaihe 8: Viimeistely

- tray icon
- automaattinen käynnistys Windowsin mukana
- ilmoitukset
- asetussivu
- graafit

---

## 33. Ensimmäinen konkreettinen tehtävä kehittäjälle

Tee ensin pieni proof of concept:

- C# desktop-ohjelma
- LibreHardwareMonitorLib
- näytä kaikki löydetyt sensorit listana
- päivitä arvoja 1 sekunnin välein
- tulosta sensorit myös debug-lokiin

Kun tämä toimii, vasta sen jälkeen rakennetaan hienompi käyttöliittymä ja lokitus.

Ensimmäinen onnistumisen mittari:

> Ohjelma löytää koneesta CPU:n, GPU:n, levyt, lämpötilat, kellot, kuormat ja tuulettimet niin hyvin kuin laitteisto sallii.

---

## 34. Yhteenveto

Ohjelma on täysin realistinen tehdä.

Suurimmat tekniset haasteet:

- emolevyjen erilaiset sensoripiirit
- admin-oikeuksien tarve
- GPU-valmistajakohtaiset rajapinnat
- tuulettimien ohjaus, jos se joskus lisätään
- kaatumisten syiden tulkinta ilman varmoja johtopäätöksiä

Kannattava aloitus:

1. käytä LibreHardwareMonitorLib-kirjastoa
2. rakenna ensin lukeminen ja näyttäminen
3. lisää lokitus alusta asti
4. lisää raja-arvot ja tapahtumaloki
5. lisää Windows Event Login tarkistus
6. tee selkeä raportti, joka kertoo oliko kone rajoilla

Ohjelman tärkein idea:

> Ei pelkästään näytetä numeroita, vaan kerrotaan käyttäjälle, oliko kone oikeasti riskirajoilla ja mikä saattoi selittää kaatumisen tai epävakauden.
