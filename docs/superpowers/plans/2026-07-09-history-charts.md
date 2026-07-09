# Historia-graafit (Vaihe 8.3) — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Historia-välilehti, jossa lämpö-, kuorma- ja tuuletingraafit valittavalta aikaväliltä (1 h/24 h/7 pv/30 pv), automaattipäivitys 60 s.

**Architecture:** Puhdas `ChartHistoryBuilder` Coreen (TDD): harvennus bucket-keskiarvolla ~500 pisteeseen, null-aukot säilyvät, levyduplikaattien erottelu, tuulettimien nimilaput ja 0-RPM-suodatus. App: `HistoryViewModel` muuntaa tulokset LiveCharts2-sarjoiksi; MainViewModel hakee datan taustasäikeessä (sama Interlocked-malli kuin muut taustatyöt).

**Tech Stack:** C#/WPF (net8.0-windows), xUnit, **LiveChartsCore.SkiaSharpView.WPF 2.0.5** (uusi NuGet, vakaa).

**Spec:** `docs/superpowers/specs/2026-07-09-history-charts-design.md`

## Global Constraints

- `dotnet test` EI buildaa App-projektia — UI-muutosten jälkeen aja AINA `dotnet build HardwareMonitor.sln`.
- Pysäytä HardwareMonitor.exe ennen buildia (lukitsee DLL:t) — käyttäjä sulkee trayn kautta (Lopeta), EI Stop-Processilla (kirjaisi kaatumisen).
- Kaikki UI-tekstit suomeksi.
- Testikomento: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
- Commit-viestit suomeksi + `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Jos LiveCharts2 2.0.5:n API-nimet poikkeavat suunnitelmasta (esim. DateTimeAxis-konstruktori tai TooltipTextPaint-property), tarkista paketin todellinen API käännösvirheestä ja mukauta — älä vaihda kirjastoa.

---

### Task 1: ChartHistoryBuilder (Core, TDD)

**Files:**
- Create: `src/HardwareMonitor.Core/Charts/ChartHistoryBuilder.cs`
- Test: `src/HardwareMonitor.Tests/Charts/ChartHistoryBuilderTests.cs`

**Interfaces:**
- Consumes: `SampleRow`, `DiskSampleValue(Name, TempAvg, TempMax)`, `FanSampleValue(Name, RpmAvg)` (Core/Storage, olemassa).
- Produces: `ChartPoint(DateTimeOffset Timestamp, double? Value)`;
  `ChartSeries(string Name, IReadOnlyList<ChartPoint> Points)`;
  `ChartHistory(Temperatures, Loads, Fans)` — kukin `IReadOnlyList<ChartSeries>`;
  `ChartHistoryBuilder.Build(IReadOnlyList<SampleRow> rows, int maxPoints, IReadOnlyDictionary<string, string> fanLabelsByRawName)` → `ChartHistory`.
  Task 2 kutsuu Buildia.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

`src/HardwareMonitor.Tests/Charts/ChartHistoryBuilderTests.cs`:

```csharp
using HardwareMonitor.Core.Charts;
using HardwareMonitor.Core.Storage;
using Xunit;

namespace HardwareMonitor.Tests.Charts;

public class ChartHistoryBuilderTests
{
    private static readonly Dictionary<string, string> NoLabels = new();
    private static readonly DateTimeOffset T0 = new(2026, 7, 9, 20, 0, 0, TimeSpan.FromHours(3));

    private static SampleRow Row(
        int offsetSeconds, double? cpuTemp = null, double? cpuLoad = null,
        double? ramLoad = null,
        DiskSampleValue[]? disks = null, FanSampleValue[]? fans = null) =>
        new(T0.AddSeconds(offsetSeconds),
            cpuLoad, null,
            cpuTemp, null,
            null,
            null, null,
            null, null,
            null, null,
            null, null,
            null, null,
            null, null,
            ramLoad, null,
            null, null,
            disks ?? Array.Empty<DiskSampleValue>(),
            fans ?? Array.Empty<FanSampleValue>());

    private static ChartSeries Cpu(ChartHistory h) =>
        h.Temperatures.First(s => s.Name == "CPU");

    [Fact]
    public void AlleMaxPoints_PalautuuSellaisenaan()
    {
        var rows = new[] { Row(0, cpuTemp: 40), Row(5, cpuTemp: 50), Row(10, cpuTemp: 60) };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.Equal(3, cpu.Points.Count);
        Assert.Equal(new double?[] { 40, 50, 60 }, cpu.Points.Select(p => p.Value));
        Assert.Equal(T0, cpu.Points[0].Timestamp);
    }

    [Fact]
    public void Harvennus_RajoittaaPistemaaranJaSailyttaaPaatepisteet()
    {
        SampleRow[] rows = Enumerable.Range(0, 100)
            .Select(i => Row(i * 5, cpuTemp: 50)).ToArray();

        ChartHistory h = ChartHistoryBuilder.Build(rows, 10, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.True(cpu.Points.Count <= 10);
        Assert.Equal(T0, cpu.Points[0].Timestamp);
        Assert.Equal(T0.AddSeconds(99 * 5), cpu.Points[^1].Timestamp);
    }

    [Fact]
    public void Harvennus_LaskeeBucketKeskiarvon()
    {
        var rows = new[]
        {
            Row(0, cpuTemp: 40), Row(5, cpuTemp: 60),
            Row(10, cpuTemp: 10), Row(15, cpuTemp: 30),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 2, NoLabels);

        ChartSeries cpu = Cpu(h);
        Assert.Equal(2, cpu.Points.Count);
        Assert.Equal(50, cpu.Points[0].Value);
        Assert.Equal(20, cpu.Points[1].Value);
    }

    [Fact]
    public void NullAukko_Sailyy()
    {
        var rows = new[] { Row(0, cpuTemp: 40), Row(5), Row(10, cpuTemp: 60) };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        Assert.Null(Cpu(h).Points[1].Value);
    }

    [Fact]
    public void SamannimisetLevyt_ErotellaanJarjestysnumerolla()
    {
        // Käyttäjän koneessa on oikeasti kaksi samannimistä 860 EVO:ta.
        var rows = new[]
        {
            Row(0, disks: new[]
            {
                new DiskSampleValue("Samsung SSD 860 EVO 1TB ", 30, 31),
                new DiskSampleValue("Samsung SSD 860 EVO 1TB ", 35, 36),
            }),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries d1 = h.Temperatures.First(s => s.Name == "Samsung SSD 860 EVO 1TB #1");
        ChartSeries d2 = h.Temperatures.First(s => s.Name == "Samsung SSD 860 EVO 1TB #2");
        Assert.Equal(30, d1.Points[0].Value);
        Assert.Equal(35, d2.Points[0].Value);
    }

    [Fact]
    public void PuuttuvaLevy_TayttyyNullilla()
    {
        var rows = new[]
        {
            Row(0, disks: new[] { new DiskSampleValue("NVMe", 55, 56) }),
            Row(5),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries disk = h.Temperatures.First(s => s.Name == "NVMe");
        Assert.Equal(55, disk.Points[0].Value);
        Assert.Null(disk.Points[1].Value);
    }

    [Fact]
    public void Tuuletin_SaaNimilapun()
    {
        var rows = new[] { Row(0, fans: new[] { new FanSampleValue("Fan #2", 1950) }) };
        var labels = new Dictionary<string, string> { ["Fan #2"] = "AIO-pumppu" };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, labels);

        ChartSeries fan = Assert.Single(h.Fans);
        Assert.Equal("AIO-pumppu", fan.Name);
        Assert.Equal(1950, fan.Points[0].Value);
    }

    [Fact]
    public void NollaTuuletin_Suodattuu()
    {
        var rows = new[]
        {
            Row(0, fans: new[] { new FanSampleValue("Fan #5", 0), new FanSampleValue("Fan #1", 600) }),
            Row(5, fans: new[] { new FanSampleValue("Fan #5", null), new FanSampleValue("Fan #1", 620) }),
        };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        ChartSeries fan = Assert.Single(h.Fans);
        Assert.Equal("Fan #1", fan.Name);
    }

    [Fact]
    public void Kuormasarjat_Loytyvat()
    {
        var rows = new[] { Row(0, cpuLoad: 10, ramLoad: 20) };

        ChartHistory h = ChartHistoryBuilder.Build(rows, 500, NoLabels);

        Assert.Equal(10, h.Loads.First(s => s.Name == "CPU").Points[0].Value);
        Assert.Equal(20, h.Loads.First(s => s.Name == "RAM").Points[0].Value);
        Assert.Null(h.Loads.First(s => s.Name == "GPU").Points[0].Value);
    }
}
```

- [ ] **Step 2: Aja testit — varmista RED**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
Expected: FAIL, käännösvirhe CS0246: `ChartHistoryBuilder`/`ChartHistory` puuttuu.

- [ ] **Step 3: Minimitoteutus**

`src/HardwareMonitor.Core/Charts/ChartHistoryBuilder.cs`:

```csharp
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Charts;

/// <summary>Yksi piste graafissa; null-arvo = aukko viivassa (ei nollaa).</summary>
public sealed record ChartPoint(DateTimeOffset Timestamp, double? Value);

public sealed record ChartSeries(string Name, IReadOnlyList<ChartPoint> Points);

public sealed record ChartHistory(
    IReadOnlyList<ChartSeries> Temperatures,
    IReadOnlyList<ChartSeries> Loads,
    IReadOnlyList<ChartSeries> Fans);

/// <summary>
/// Kokoaa historiagraafien sarjat koosteriveistä (spec
/// docs/superpowers/specs/2026-07-09-history-charts-design.md). Puhdas:
/// harvennus bucket-keskiarvolla, null-aukot säilyvät, samannimiset levyt
/// erotellaan järjestysnumerolla, tuulettimet saavat nimilaput ja aina
/// nollassa olevat tuulettimet suodatetaan pois.
/// </summary>
public static class ChartHistoryBuilder
{
    public static ChartHistory Build(
        IReadOnlyList<SampleRow> rows, int maxPoints,
        IReadOnlyDictionary<string, string> fanLabelsByRawName)
    {
        IReadOnlyList<IReadOnlyList<SampleRow>> buckets = Bucketize(rows, maxPoints);
        DateTimeOffset[] stamps = Timestamps(buckets, rows);

        var temps = new List<ChartSeries>
        {
            Series("CPU", buckets, stamps, r => r.CpuTempAvg),
            Series("GPU", buckets, stamps, r => r.GpuTempAvg),
            Series("GPU hotspot", buckets, stamps, r => r.GpuHotspotAvg),
        };
        foreach ((string name, int occurrence, string display) in DiskKeys(rows))
        {
            temps.Add(Series(display, buckets, stamps,
                r => Nth(r.Disks, name, occurrence)?.TempAvg));
        }

        var loads = new List<ChartSeries>
        {
            Series("CPU", buckets, stamps, r => r.CpuLoadAvg),
            Series("GPU", buckets, stamps, r => r.GpuLoadAvg),
            Series("RAM", buckets, stamps, r => r.RamLoadAvg),
        };

        var fans = new List<ChartSeries>();
        foreach (string name in rows.SelectMany(r => r.Fans.Select(f => f.Name)).Distinct())
        {
            string display =
                fanLabelsByRawName.TryGetValue(name, out string? label) && label.Length > 0
                    ? label : name;
            ChartSeries series = Series(display, buckets, stamps,
                r => r.Fans.FirstOrDefault(f => f.Name == name)?.RpmAvg);
            if (series.Points.Any(p => p.Value is > 0))
            {
                fans.Add(series);
            }
        }

        return new ChartHistory(temps, loads, fans);
    }

    private static IReadOnlyList<IReadOnlyList<SampleRow>> Bucketize(
        IReadOnlyList<SampleRow> rows, int maxPoints)
    {
        if (rows.Count <= maxPoints)
        {
            return rows.Select(r => (IReadOnlyList<SampleRow>)new[] { r }).ToList();
        }

        var buckets = new List<IReadOnlyList<SampleRow>>();
        int size = (int)Math.Ceiling(rows.Count / (double)maxPoints);
        for (int start = 0; start < rows.Count; start += size)
        {
            buckets.Add(rows.Skip(start).Take(size).ToList());
        }

        return buckets;
    }

    /// <summary>Lohkon aikaleima on keskimmäisen rivin; ensimmäinen ja viimeinen lohko saavat datan päätepisteiden aikaleimat.</summary>
    private static DateTimeOffset[] Timestamps(
        IReadOnlyList<IReadOnlyList<SampleRow>> buckets, IReadOnlyList<SampleRow> rows)
    {
        var stamps = new DateTimeOffset[buckets.Count];
        for (int i = 0; i < buckets.Count; i++)
        {
            stamps[i] = buckets[i][buckets[i].Count / 2].Timestamp;
        }

        if (buckets.Count > 0)
        {
            stamps[0] = rows[0].Timestamp;
            stamps[^1] = rows[^1].Timestamp;
        }

        return stamps;
    }

    private static ChartSeries Series(
        string name, IReadOnlyList<IReadOnlyList<SampleRow>> buckets,
        DateTimeOffset[] stamps, Func<SampleRow, double?> select)
    {
        var points = new ChartPoint[buckets.Count];
        for (int i = 0; i < buckets.Count; i++)
        {
            double[] values = buckets[i].Select(select)
                .Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            points[i] = new ChartPoint(stamps[i],
                values.Length > 0 ? values.Average() : null);
        }

        return new ChartSeries(name, points);
    }

    private static DiskSampleValue? Nth(
        IReadOnlyList<DiskSampleValue> disks, string name, int occurrence)
    {
        int seen = 0;
        foreach (DiskSampleValue disk in disks)
        {
            if (disk.Name == name && seen++ == occurrence)
            {
                return disk;
            }
        }

        return null;
    }

    /// <summary>Levyavaimet: (nimi, monesko samanniminen); näyttönimi trimmattuna, duplikaateille " #n".</summary>
    private static IEnumerable<(string Name, int Occurrence, string Display)> DiskKeys(
        IReadOnlyList<SampleRow> rows)
    {
        var counts = new Dictionary<string, int>();
        foreach (SampleRow row in rows)
        {
            var inRow = new Dictionary<string, int>();
            foreach (DiskSampleValue disk in row.Disks)
            {
                inRow[disk.Name] = inRow.TryGetValue(disk.Name, out int n) ? n + 1 : 1;
            }

            foreach ((string name, int count) in inRow)
            {
                counts[name] = Math.Max(counts.TryGetValue(name, out int c) ? c : 0, count);
            }
        }

        foreach ((string name, int count) in counts)
        {
            for (int i = 0; i < count; i++)
            {
                yield return (name, i,
                    count > 1 ? $"{name.Trim()} #{i + 1}" : name.Trim());
            }
        }
    }
}
```

- [ ] **Step 4: Aja testit — varmista GREEN**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
Expected: PASS, 125 testiä (116 + 9), 0 varoitusta.

- [ ] **Step 5: Commit**

```powershell
git add src/HardwareMonitor.Core/Charts/ChartHistoryBuilder.cs src/HardwareMonitor.Tests/Charts/ChartHistoryBuilderTests.cs
git commit -m @'
Lisää ChartHistoryBuilder historia-graafeja varten (TDD)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 2: LiveCharts2-paketti + HistoryViewModel + MainViewModel-kytkentä

**Files:**
- Modify: `src/HardwareMonitor.App/HardwareMonitor.App.csproj` (NuGet)
- Create: `src/HardwareMonitor.App/ViewModels/HistoryViewModel.cs`
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `ChartHistoryBuilder.Build` (Task 1); MainViewModelin `_historyDb`, `_latestMetrics`, `_settings.FanLabels`, `_logger`, `_tickCount` (olemassa).
- Produces: `MainViewModel.History` (HistoryViewModel);
  `HistoryViewModel.RangeIndex` (int, ComboBox-sidonta), `.RangeHours`,
  `.TempSeries/.LoadSeries/.FanSeries` (ISeries[]),
  `.TempXAxes/.LoadXAxes/.FanXAxes/.TempYAxes/.LoadYAxes/.FanYAxes` (Axis[]),
  `.LegendPaint/.TooltipTextPaint/.TooltipBackgroundPaint`,
  `.Apply(ChartHistory, int)`, event `RefreshRequested`.
  Task 3:n XAML sitoo näihin.

- [ ] **Step 1: Lisää NuGet-paketti**

Run: `dotnet add src/HardwareMonitor.App/HardwareMonitor.App.csproj package LiveChartsCore.SkiaSharpView.WPF --version 2.0.5`
Expected: paketti lisätty csprojiin, restore onnistuu.

- [ ] **Step 2: Luo HistoryViewModel**

`src/HardwareMonitor.App/ViewModels/HistoryViewModel.cs`:

```csharp
using System.ComponentModel;
using HardwareMonitor.Core.Charts;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Historia-välilehden graafit (Vaihe 8.3). Data tulee puhtaalta
/// ChartHistoryBuilderilta; tämä luokka omistaa vain LiveCharts2-
/// esitysmuodon (sarjat, akselit, värit).
/// </summary>
public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private static readonly int[] RangeHoursByIndex = { 1, 24, 168, 720 };

    private static readonly SKColor[] Palette =
    {
        new(0x4F, 0xC3, 0xF7), // syaani (CPU)
        new(0x81, 0xC7, 0x84), // vihreä (GPU)
        new(0xFF, 0xB7, 0x4D), // oranssi (hotspot/RAM)
        new(0xBA, 0x68, 0xC8), // violetti
        new(0xF0, 0x62, 0x92), // pinkki
        new(0xFF, 0xD5, 0x4F), // keltainen
        new(0x4D, 0xB6, 0xAC), // teal
        new(0x90, 0xA4, 0xAE), // harmaa
    };

    private int _rangeIndex = 1; // 24 h

    /// <summary>Nostetaan kun aikaväli vaihtuu — MainViewModel hakee datan.</summary>
    public event Action? RefreshRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int RangeIndex
    {
        get => _rangeIndex;
        set
        {
            if (value == _rangeIndex || value < 0 || value >= RangeHoursByIndex.Length)
            {
                return;
            }

            _rangeIndex = value;
            OnChanged(nameof(RangeIndex));
            RefreshRequested?.Invoke();
        }
    }

    public int RangeHours => RangeHoursByIndex[_rangeIndex];

    public ISeries[] TempSeries { get; private set; } = Array.Empty<ISeries>();

    public ISeries[] LoadSeries { get; private set; } = Array.Empty<ISeries>();

    public ISeries[] FanSeries { get; private set; } = Array.Empty<ISeries>();

    public Axis[] TempXAxes { get; private set; } = new[] { TimeAxis(24) };

    public Axis[] LoadXAxes { get; private set; } = new[] { TimeAxis(24) };

    public Axis[] FanXAxes { get; private set; } = new[] { TimeAxis(24) };

    public Axis[] TempYAxes { get; } = new[] { ValueAxis(null, null) };

    public Axis[] LoadYAxes { get; } = new[] { ValueAxis(0, 100) };

    public Axis[] FanYAxes { get; } = new[] { ValueAxis(0, null) };

    public SolidColorPaint LegendPaint { get; } = new(SKColors.LightGray);

    public SolidColorPaint TooltipTextPaint { get; } = new(SKColors.LightGray);

    public SolidColorPaint TooltipBackgroundPaint { get; } = new(new SKColor(0x25, 0x25, 0x26));

    /// <summary>Kutsutaan UI-säikeessä kun uusi data on haettu.</summary>
    public void Apply(ChartHistory history, int rangeHours)
    {
        TempSeries = ToSeries(history.Temperatures);
        LoadSeries = ToSeries(history.Loads);
        FanSeries = ToSeries(history.Fans);
        TempXAxes = new[] { TimeAxis(rangeHours) };
        LoadXAxes = new[] { TimeAxis(rangeHours) };
        FanXAxes = new[] { TimeAxis(rangeHours) };
        OnChanged(nameof(TempSeries));
        OnChanged(nameof(LoadSeries));
        OnChanged(nameof(FanSeries));
        OnChanged(nameof(TempXAxes));
        OnChanged(nameof(LoadXAxes));
        OnChanged(nameof(FanXAxes));
    }

    private static ISeries[] ToSeries(IReadOnlyList<ChartSeries> series) =>
        series.Select((s, i) => (ISeries)new LineSeries<DateTimePoint>
        {
            Name = s.Name,
            Values = s.Points
                .Select(p => new DateTimePoint(p.Timestamp.LocalDateTime, p.Value))
                .ToArray(),
            GeometrySize = 0,
            LineSmoothness = 0,
            Fill = null,
            Stroke = new SolidColorPaint(Palette[i % Palette.Length]) { StrokeThickness = 1.5f },
        }).ToArray();

    private static Axis TimeAxis(int rangeHours)
    {
        (TimeSpan unit, string format) = rangeHours switch
        {
            <= 1 => (TimeSpan.FromMinutes(10), "HH.mm"),
            <= 24 => (TimeSpan.FromHours(3), "HH.mm"),
            <= 168 => (TimeSpan.FromDays(1), "d.M."),
            _ => (TimeSpan.FromDays(5), "d.M."),
        };

        return new DateTimeAxis(unit, d => d.ToString(format))
        {
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            TextSize = 11,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x3F, 0x3F, 0x46))
            {
                StrokeThickness = 1,
            },
        };
    }

    private static Axis ValueAxis(double? min, double? max) => new()
    {
        MinLimit = min,
        MaxLimit = max,
        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
        TextSize = 11,
        SeparatorsPaint = new SolidColorPaint(new SKColor(0x3F, 0x3F, 0x46))
        {
            StrokeThickness = 1,
        },
    };

    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 3: Kytke MainViewModeliin**

`src/HardwareMonitor.App/ViewModels/MainViewModel.cs`:

1. Using-riveihin: `using HardwareMonitor.Core.Charts;`
2. Kenttä muiden Interlocked-lippujen viereen (`private int _insightsWriteRunning;` jälkeen):

```csharp
    private int _historyRefreshRunning;
```

3. Ctoriin `SettingsPage = ...`-rivin jälkeen:

```csharp
        History.RefreshRequested += RefreshHistoryInBackground;
```

4. Property `SettingsPage`-propertyn jälkeen:

```csharp
    /// <summary>Historia-välilehden graafit (Vaihe 8.3).</summary>
    public HistoryViewModel History { get; } = new();
```

5. Start()-metodissa rivin `ScanWindowsEventsInBackground();` (DB-alustuksen
   jälkeinen kutsu, ~rivi 310) perään:

```csharp
                RefreshHistoryInBackground();
```

6. Refresh()-metodiin `if (_tickCount % 1800 == 60) { ... }` -lohkon jälkeen:

```csharp
            // Historiagraafien data minuutin välein (offset ettei osu muihin töihin).
            if (_tickCount % 60 == 45)
            {
                RefreshHistoryInBackground();
            }
```

7. Uudet metodit (esim. `RefreshAnalysisCachesInBackground`-metodin jälkeen):

```csharp
    /// <summary>Hakee historiagraafien datan taustalla; päällekkäisyys estetty.</summary>
    private void RefreshHistoryInBackground()
    {
        if (_historyDb is not { } db
            || Interlocked.Exchange(ref _historyRefreshRunning, 1) == 1)
        {
            return;
        }

        int hours = History.RangeHours;
        Task.Run(() =>
        {
            try
            {
                IReadOnlyList<SampleRow> rows =
                    db.ReadSampleRows(DateTimeOffset.Now.AddHours(-hours));
                ChartHistory history =
                    ChartHistoryBuilder.Build(rows, 500, BuildFanLabelMap());
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                    () => History.Apply(history, hours));
            }
            catch (Exception ex)
            {
                _logger.Log($"VIRHE historiagraafien haussa: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _historyRefreshRunning, 0);
            }
        });
    }

    /// <summary>Tuulettimen raakanimi → käyttäjän nimilappu (graafien selitteisiin).</summary>
    private Dictionary<string, string> BuildFanLabelMap()
    {
        var map = new Dictionary<string, string>();
        if (_latestMetrics is { } m)
        {
            foreach (FanMetrics fan in m.Fans)
            {
                if (_settings.FanLabels.TryGetValue(fan.Identifier, out string? label)
                    && label.Length > 0)
                {
                    map[fan.Name] = label;
                }
            }
        }

        return map;
    }
```

- [ ] **Step 4: Buildaa**

Run: `dotnet build HardwareMonitor.sln --nologo`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 5: Commit**

```powershell
git add src/HardwareMonitor.App/HardwareMonitor.App.csproj src/HardwareMonitor.App/ViewModels/HistoryViewModel.cs src/HardwareMonitor.App/ViewModels/MainViewModel.cs
git commit -m @'
Lisää LiveCharts2 ja HistoryViewModel (Vaihe 8.3)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 3: Historia-välilehti (XAML)

**Files:**
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (xmlns + uusi TabItem Asetukset-välilehden jälkeen, ennen `</TabControl>`)

**Interfaces:**
- Consumes: `History.*`-propertyt (Task 2).
- Produces: valmis UI.

- [ ] **Step 1: Lisää lvc-nimiavaruus**

`MainWindow.xaml`, Window-elementin attribuutteihin (`xmlns:vm=...`-rivin jälkeen):

```xml
        xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF"
```

- [ ] **Step 2: Lisää Historia-TabItem**

`MainWindow.xaml`, Asetukset-TabItemin sulkevan `</TabItem>`-rivin jälkeen
(ennen `</TabControl>`):

```xml
            <TabItem Header="Historia">
                <DockPanel>
                    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="16,12,16,4">
                        <TextBlock Text="Aikaväli" Foreground="#BDBDBD" VerticalAlignment="Center"
                                   Margin="0,0,8,0" />
                        <ComboBox SelectedIndex="{Binding History.RangeIndex}" Width="90"
                                  VerticalAlignment="Center">
                            <ComboBoxItem Content="1 h" />
                            <ComboBoxItem Content="24 h" />
                            <ComboBoxItem Content="7 pv" />
                            <ComboBoxItem Content="30 pv" />
                        </ComboBox>
                        <TextBlock Text="Graafit päivittyvät automaattisesti minuutin välein"
                                   Foreground="#9E9E9E" FontStyle="Italic"
                                   VerticalAlignment="Center" Margin="16,0,0,0" />
                    </StackPanel>
                    <ScrollViewer VerticalScrollBarVisibility="Auto">
                        <StackPanel Margin="16,4,16,16">
                            <TextBlock Text="Lämpötilat (°C)" FontSize="15" FontWeight="Bold"
                                       Foreground="#4FC3F7" Margin="0,8,0,4" />
                            <lvc:CartesianChart Series="{Binding History.TempSeries}"
                                                XAxes="{Binding History.TempXAxes}"
                                                YAxes="{Binding History.TempYAxes}"
                                                LegendPosition="Right"
                                                LegendTextPaint="{Binding History.LegendPaint}"
                                                TooltipTextPaint="{Binding History.TooltipTextPaint}"
                                                TooltipBackgroundPaint="{Binding History.TooltipBackgroundPaint}"
                                                Height="280" />
                            <TextBlock Text="Kuormat (%)" FontSize="15" FontWeight="Bold"
                                       Foreground="#4FC3F7" Margin="0,16,0,4" />
                            <lvc:CartesianChart Series="{Binding History.LoadSeries}"
                                                XAxes="{Binding History.LoadXAxes}"
                                                YAxes="{Binding History.LoadYAxes}"
                                                LegendPosition="Right"
                                                LegendTextPaint="{Binding History.LegendPaint}"
                                                TooltipTextPaint="{Binding History.TooltipTextPaint}"
                                                TooltipBackgroundPaint="{Binding History.TooltipBackgroundPaint}"
                                                Height="240" />
                            <TextBlock Text="Tuulettimet (RPM)" FontSize="15" FontWeight="Bold"
                                       Foreground="#4FC3F7" Margin="0,16,0,4" />
                            <lvc:CartesianChart Series="{Binding History.FanSeries}"
                                                XAxes="{Binding History.FanXAxes}"
                                                YAxes="{Binding History.FanYAxes}"
                                                LegendPosition="Right"
                                                LegendTextPaint="{Binding History.LegendPaint}"
                                                TooltipTextPaint="{Binding History.TooltipTextPaint}"
                                                TooltipBackgroundPaint="{Binding History.TooltipBackgroundPaint}"
                                                Height="240" />
                        </StackPanel>
                    </ScrollViewer>
                </DockPanel>
            </TabItem>
```

- [ ] **Step 3: Buildaa ja aja testit**

Run: `dotnet build HardwareMonitor.sln --nologo`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).
Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
Expected: PASS, 125 testiä.

- [ ] **Step 4: Commit**

```powershell
git add src/HardwareMonitor.App/MainWindow.xaml
git commit -m @'
Lisää Historia-välilehti graafeineen (Vaihe 8.3)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 4: Ajonaikainen todennus + HANDOFF + push

**Files:**
- Modify: `HANDOFF.md` (kohta 3 tehdyksi)

- [ ] **Step 1: Käynnistä ja todenna**

Varmista ettei HardwareMonitor.exe ole ajossa, sitten `.\run.ps1 -AsAdmin`
(käyttäjä hyväksyy UAC:n). Käyttäjä klikkaa (UIPI estää Clauden syötteet
korotettuun ikkunaan), Claude todentaa kuvakaappauksin + debug.logista:

1. Historia-välilehti: kolme graafia oikealla datalla 24 h -välillä
   (CPU/GPU/hotspot/levyt; kuormat; tuulettimet nimilapuilla, mukana
   "AIO-pumppu", 0-RPM-tuulettimet poissa).
2. Kaksi 860 EVO:ta näkyvät erillisinä sarjoina (#1 ja #2).
3. Aikavälin vaihto 1 h → data ja X-akselin muotoilu päivittyvät.
4. 30 pv → pisteitä on harvennettu (viiva pysyy sulavana), aukot näkyvät
   katkoksina (yöt kun kone oli sammuksissa).
5. Automaattipäivitys: jätä välilehti auki ~2 min → viiva jatkuu oikealta.
6. Tooltip toimii ja on luettava tummalla taustalla.

- [ ] **Step 2: Päivitä HANDOFF.md**

Merkitse "Seuraavat askeleet" -listan kohta 3 (Graafit) tehdyksi samaan
tapaan kuin kohdat 1–2: toteutuksen ydin (ChartHistoryBuilder TDD,
HistoryViewModel, LiveCharts2 2.0.5, Historia-välilehti) ja todennus.

- [ ] **Step 3: Commitoi ja pushaa**

```powershell
git add HANDOFF.md
git commit -m @'
Päivitä HANDOFF: historia-graafit valmiit

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
git push
```
