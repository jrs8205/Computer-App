# Historia-graafit (Vaihe 8.3) — design

Päivämäärä: 9.7.2026. Hyväksytty käyttäjän kanssa AskUserQuestion-kierroksilla
(sisältö: lämmöt + kuormat + tuulettimet; aikavälit 1 h/24 h/7 pv/30 pv;
automaattipäivitys 60 s; puhdas builder Coreen).

## Tavoite

Sensorihistorian visualisointi: käyttäjä näkee yhdellä silmäyksellä koneen
lämpö-, kuorma- ja tuuletintrendit valitulta aikaväliltä.

## UI: uusi Historia-välilehti

Neljäs välilehti (Dashboard · Kaikki sensorit · Asetukset · **Historia**):

- Ylhäällä aikavälivalinta: napit/RadioButtonit **1 h / 24 h / 7 pv / 30 pv**,
  oletus 24 h.
- Kolme graafia allekkain (ScrollViewer):
  1. **Lämpötilat (°C)**: CPU, GPU, GPU hotspot + yksi sarja per levy
     (nimet trimmataan — LHM jättää loppuvälilyönnin).
  2. **Kuormat (%)**: CPU, GPU, RAM.
  3. **Tuulettimet (RPM)**: nimilaput (esim. "AIO-pumppu") mapataan
     raakanimistä; tuulettimet joiden kaikki arvot ovat 0/null piilotetaan.
- Legenda ja tooltip; tumma teema (taustat #1E1E1E/#252526, vaaleat tekstit).
- Päivitys: heti aikavälin vaihtuessa + automaattisesti 60 s välein.
  Datan haku taustasäikeessä (Task.Run + Interlocked-päällekkäisyyssuoja,
  sama malli kuin muut MainViewModelin taustatyöt).

Kirjasto: **LiveCharts2** (`LiveChartsCore.SkiaSharpView.WPF`, versio
kiinnitetään csprojiin). Huom: paketti on rc-vaiheessa — jos se osoittautuu
WPF:ssä ongelmalliseksi, varasuunnitelma on ScottPlot 5 (spec ei muutu,
vain App-kerroksen sarjamuunnos).

## Core: ChartHistoryBuilder (puhdas, TDD)

`Core/Charts/ChartHistoryBuilder` + DTO:t:

```
ChartPoint(DateTimeOffset Timestamp, double? Value)
ChartSeries(string Name, IReadOnlyList<ChartPoint> Points)
ChartHistory(
    IReadOnlyList<ChartSeries> Temperatures,
    IReadOnlyList<ChartSeries> Loads,
    IReadOnlyList<ChartSeries> Fans)
```

`ChartHistoryBuilder.Build(IReadOnlyList<SampleRow> rows, int maxPoints,
IReadOnlyDictionary<string, string> fanLabelsByRawName)` → `ChartHistory`.

Säännöt:

- **Sarjat**: Temperatures = "CPU" (CpuTempAvg), "GPU" (GpuTempAvg),
  "GPU hotspot" (GpuHotspotAvg) + levyt (DiskSampleValue.TempAvg,
  nimi trimmattuna). Loads = "CPU" (CpuLoadAvg), "GPU" (GpuLoadAvg),
  "RAM" (RamLoadAvg). Fans = FanSampleValue.RpmAvg per tuuletin.
- **Harvennus**: jos rivejä > maxPoints, jaetaan aikajärjestyksessä
  tasakokoisiin lohkoihin (bucket) ja lohkon arvo = ei-null-arvojen
  keskiarvo (null jos lohkossa ei arvoja); aikaleima = lohkon keskimmäisen
  rivin aikaleima. Ensimmäinen ja viimeinen rivi säilyvät päätepisteinä.
  Jos rivejä ≤ maxPoints, käytetään sellaisenaan.
- **Null-aukot säilyvät** (esim. kone sammuksissa) — ei nollia, jotta
  viivaan tulee katkos.
- **Tuulettimet**: nimi mapataan `fanLabelsByRawName`-sanakirjalla
  (raakanimi → nimilappu; puuttuva avain → raakanimi). Tuuletin jonka
  kaikki arvot ovat null tai 0 jätetään kokonaan pois.
- **Levysarjojen unioni**: sarja luodaan jokaiselle levynimelle joka
  esiintyy missä tahansa rivissä; riviltä puuttuva levy → null.

## App: HistoryViewModel + XAML

- `ViewModels/HistoryViewModel`: valittu aikaväli (enum/tunnit),
  `Refresh(IReadOnlyList<SampleRow>)`-tulosten muunto LiveCharts2
  `ISeries`-kokoelmiksi (LineSeries, GeometrySize=0, viivat ohuet) ja
  X-akselin DateTime-muotoilu (1 h/24 h → "HH.mm", 7/30 pv → "d.M.").
- Nimilappu-map rakennetaan MainViewModelissa nykyisistä mittauksista:
  FanMetrics.Identifier → raakanimi ja settings.FanLabels.Identifier →
  nimilappu ⇒ raakanimi → nimilappu.
- MainViewModel: haku `_historyDb.ReadSampleRows(now - väli)` taustalla
  aikavälin vaihtuessa + tick % 60 -kohdassa kun Historia-data on käytössä;
  tulos Dispatcheriin ja HistoryViewModelille.
- maxPoints = 500.

## Testaus ja todennus

- ChartHistoryBuilder-yksikkötestit: pistemäärä ≤ maxPoints + päätepisteet
  säilyvät; bucket-keskiarvo lasketaan oikein; null-aukko säilyy; levyunioni
  toimii kun levy puuttuu osasta rivejä; tuulettimen nimilappu mapataan;
  aina-0-tuuletin suodattuu; alle maxPoints -data palautuu sellaisenaan.
- Ajossa: kolme graafia oikealla datalla (24 h), aikavälin vaihto päivittää,
  automaattipäivitys 60 s, tumma teema luettava, tooltipit toimivat.

## Rajaukset (YAGNI)

- Ei zoom/pan-toimintoa v1:een (LiveCharts2 tukisi — lisättävissä myöhemmin).
- Ei Max-arvojen erillisiä sarjoja (huiput näkyvät riittävästi 500 pisteellä);
  kirjataan jatkokehitysideaksi ("huippukäyrä").
- Ei tapahtumamerkintöjä graafiin (WARNING/CRITICAL-viivoja) v1:een.
- Tehot (W), kellot (MHz) ja VRAM jäävät pois — data on kannassa jos
  myöhemmin halutaan.
