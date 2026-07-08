# Vaihe 4: Raja-arvot ja varoitukset — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raja-arvovalvonta kestologiikalla: välitön värikoodaus UI:hin, kestoperusteiset tapahtumat events-tauluun cooldownilla.

**Architecture:** Puhdas `ThresholdMonitor`-tilakone (Core/Analysis) saa KeyMetricsin + kellon kerran sekunnissa ja palauttaa välittömät tilat (UI-värit) sekä nostetut tapahtumat. UI värjää korttiarvot ja overlayn reunuksen; MainViewModel kirjaa tapahtumat EventLogServiceen.

**Tech Stack:** ei uusia riippuvuuksia; xUnit keinokellolla.

## Global Constraints

- Kuten aiemmat: net8.0-windows, sealed + file-scoped ns, suomenkieliset kommentit,
  `dotnet build HardwareMonitor.sln` UI-muutosten jälkeen (appi kiinni ensin),
  commit-viestit suomeksi + Co-Authored-By-rivi.
- Värit: Normal #A5D6A7, Warning #FFB74D, Critical #EF5350. Overlay-reunus:
  Normal = Transparent.
- Spec: `docs/superpowers/specs/2026-07-08-thresholds-design.md`.

---

### Task 1: ThresholdSettings (TDD)

**Files:**
- Modify: `src/HardwareMonitor.Core/Settings/AppSettings.cs`
- Test: `src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs` (lisäys)

**Interfaces:**
- Produces: `AppSettings.Thresholds : ThresholdSettings` kentillä (float)
  `CpuWarningTemp=85, CpuCriticalTemp=95, GpuWarningTemp=85, GpuCriticalTemp=95,
  GpuHotspotWarningTemp=95, GpuHotspotCriticalTemp=105, NvmeWarningTemp=70,
  NvmeCriticalTemp=82, RamWarningPercent=85, RamCriticalPercent=95,
  FanStopCpuTemp=80` ja (int) `WarningSustainSeconds=30, CriticalSustainSeconds=10,
  EventCooldownMinutes=5`.

- [ ] **Step 1: Testi** SettingsServiceTestsiin:

```csharp
    [Fact]
    public void Thresholds_OletuksetJaTallennus()
    {
        var service = new SettingsService(_dir);

        AppSettings defaults = service.Load();
        Assert.Equal(85, defaults.Thresholds.CpuWarningTemp);
        Assert.Equal(105, defaults.Thresholds.GpuHotspotCriticalTemp);
        Assert.Equal(70, defaults.Thresholds.NvmeWarningTemp);
        Assert.Equal(30, defaults.Thresholds.WarningSustainSeconds);
        Assert.Equal(5, defaults.Thresholds.EventCooldownMinutes);

        defaults.Thresholds.CpuWarningTemp = 80;
        service.Save(defaults);

        Assert.Equal(80, new SettingsService(_dir).Load().Thresholds.CpuWarningTemp);
    }
```

- [ ] **Step 2: RED** → `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` FAIL.
- [ ] **Step 3: Toteutus** AppSettings.cs:ään (luvun 29 nimet):

```csharp
/// <summary>Varoitus- ja kriittisrajat (määrittelyn luvut 16 ja 29).</summary>
public sealed class ThresholdSettings
{
    public float CpuWarningTemp { get; set; } = 85;
    public float CpuCriticalTemp { get; set; } = 95;
    public float GpuWarningTemp { get; set; } = 85;
    public float GpuCriticalTemp { get; set; } = 95;
    public float GpuHotspotWarningTemp { get; set; } = 95;
    public float GpuHotspotCriticalTemp { get; set; } = 105;
    public float NvmeWarningTemp { get; set; } = 70;
    public float NvmeCriticalTemp { get; set; } = 82;
    public float RamWarningPercent { get; set; } = 85;
    public float RamCriticalPercent { get; set; } = 95;

    /// <summary>Pysähtynyt tuuletin on kriittinen vasta kun CPU on vähintään näin kuuma.</summary>
    public float FanStopCpuTemp { get; set; } = 80;

    /// <summary>Yhtäjaksoinen ylitys sekunneissa ennen WARNING-tapahtumaa (piikki vs. kesto).</summary>
    public int WarningSustainSeconds { get; set; } = 30;

    public int CriticalSustainSeconds { get; set; } = 10;

    /// <summary>Sama sääntö+taso ei kirjaudu uudelleen tätä useammin (luku 26).</summary>
    public int EventCooldownMinutes { get; set; } = 5;
}
```
ja `AppSettings`iin `public ThresholdSettings Thresholds { get; set; } = new();`

- [ ] **Step 4: GREEN + commit** `"Lisää raja-arvoasetukset oletuksineen (TDD)"`

---

### Task 2: ThresholdMonitor-tilakone (TDD)

**Files:**
- Create: `src/HardwareMonitor.Core/Analysis/ThresholdMonitor.cs`
- Test: `src/HardwareMonitor.Tests/Analysis/ThresholdMonitorTests.cs`

**Interfaces:**
- Produces: `enum ThresholdState { Normal, Warning, Critical }`;
  `ThresholdEvent(string Level, string Component, string Sensor, double? Value, double? Threshold, string Message)`;
  `MetricStates(ThresholdState CpuTemp, GpuTemp, GpuHotspot, RamLoad, IReadOnlyList<ThresholdState> Disks, ThresholdState Worst)`;
  `ThresholdResult(MetricStates States, IReadOnlyList<ThresholdEvent> Events)`;
  `ThresholdMonitor(ThresholdSettings).Update(KeyMetrics m, DateTimeOffset now, IReadOnlyDictionary<string,string> fanLabels) -> ThresholdResult`.

- [ ] **Step 1: Testit** (keinokello; Metrics-helperi kuten SampleAggregatorTestsissä,
  laajennettuna gpu-/ram-/fan-parametreilla):

```csharp
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using Xunit;

namespace HardwareMonitor.Tests.Analysis;

public class ThresholdMonitorTests
{
    private static readonly Dictionary<string, string> NoLabels = new();
    private static readonly DateTimeOffset T0 = new(2026, 7, 8, 21, 0, 0, TimeSpan.FromHours(3));

    private static KeyMetrics Metrics(
        float? cpuTemp = null, float? ramLoad = null,
        DiskMetrics[]? disks = null, FanMetrics[]? fans = null) =>
        new(null, cpuTemp, null, null, null, null, null, null, null, null,
            ramLoad, null, null,
            disks ?? Array.Empty<DiskMetrics>(),
            fans ?? Array.Empty<FanMetrics>());

    private static ThresholdMonitor Monitor() => new(new ThresholdSettings());

    [Fact]
    public void LyhytPiikki_VaihtaaTilanHetiMutteiKirjaaTapahtumaa()
    {
        var monitor = Monitor();

        ThresholdResult r1 = monitor.Update(Metrics(cpuTemp: 90), T0, NoLabels);
        Assert.Equal(ThresholdState.Warning, r1.States.CpuTemp);
        Assert.Empty(r1.Events);

        // Piikki ohi 5 s myöhemmin — ei tapahtumia kumpaankaan suuntaan.
        ThresholdResult r2 = monitor.Update(Metrics(cpuTemp: 60), T0.AddSeconds(5), NoLabels);
        Assert.Equal(ThresholdState.Normal, r2.States.CpuTemp);
        Assert.Empty(r2.Events);
    }

    [Fact]
    public void KestavaYlitys_KirjaaWarninginTasanKerran()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();

        for (int s = 0; s <= 60; s++)
        {
            all.AddRange(monitor.Update(Metrics(cpuTemp: 90), T0.AddSeconds(s), NoLabels).Events);
        }

        ThresholdEvent e = Assert.Single(all);
        Assert.Equal("WARNING", e.Level);
        Assert.Equal("CPU", e.Component);
        Assert.Equal(90, e.Value);
        Assert.Equal(85, e.Threshold);
    }

    [Fact]
    public void Eskalaatio_KriittinenKirjautuuWarninginJalkeen()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();

        for (int s = 0; s <= 40; s++) // WARNING nousee 30 s kohdalla
        {
            all.AddRange(monitor.Update(Metrics(cpuTemp: 90), T0.AddSeconds(s), NoLabels).Events);
        }

        for (int s = 41; s <= 60; s++) // arvo kriittiseksi -> CRITICAL 10 s ylityksen jälkeen
        {
            all.AddRange(monitor.Update(Metrics(cpuTemp: 97), T0.AddSeconds(s), NoLabels).Events);
        }

        Assert.Equal(2, all.Count);
        Assert.Equal("WARNING", all[0].Level);
        Assert.Equal("CRITICAL", all[1].Level);
    }

    [Fact]
    public void Palautuminen_KirjaaInfonKestollaJaHuipulla()
    {
        var monitor = Monitor();
        for (int s = 0; s <= 45; s++)
        {
            monitor.Update(Metrics(cpuTemp: s == 20 ? 93 : 90), T0.AddSeconds(s), NoLabels);
        }

        ThresholdResult r = monitor.Update(Metrics(cpuTemp: 60), T0.AddSeconds(46), NoLabels);

        ThresholdEvent e = Assert.Single(r.Events);
        Assert.Equal("INFO", e.Level);
        Assert.Equal(93, e.Value); // huippu
        Assert.Contains("46 s", e.Message); // kesto
        Assert.Equal(ThresholdState.Normal, r.States.CpuTemp);
    }

    [Fact]
    public void Cooldown_EstaaToisenJaksonTapahtuman_SallliiMyohemmin()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();

        void Excursion(DateTimeOffset start)
        {
            for (int s = 0; s <= 35; s++)
            {
                all.AddRange(monitor.Update(Metrics(cpuTemp: 90), start.AddSeconds(s), NoLabels).Events);
            }
            all.AddRange(monitor.Update(Metrics(cpuTemp: 60), start.AddSeconds(36), NoLabels).Events);
        }

        Excursion(T0);                    // WARNING + INFO
        Excursion(T0.AddMinutes(2));      // cooldownin sisällä -> vain INFO? EI: ei nostoa, joten ei myöskään palautumista
        Excursion(T0.AddMinutes(10));     // cooldown ohi -> WARNING + INFO

        Assert.Equal(2, all.Count(e => e.Level == "WARNING"));
        Assert.Equal(2, all.Count(e => e.Level == "INFO"));
    }

    [Fact]
    public void Tuuletin_PyorinytJaPysahtynytKuumana_Kriittinen()
    {
        var monitor = Monitor();
        var all = new List<ThresholdEvent>();
        var spinning = new[] { new FanMetrics("CPU Fan", 900f, "/fan/1") };
        var stopped = new[] { new FanMetrics("CPU Fan", 0f, "/fan/1") };

        monitor.Update(Metrics(cpuTemp: 60, fans: spinning), T0, NoLabels); // rekisteröityy pyörineeksi

        ThresholdState worst = ThresholdState.Normal;
        for (int s = 1; s <= 12; s++)
        {
            ThresholdResult r = monitor.Update(Metrics(cpuTemp: 85, fans: stopped), T0.AddSeconds(s), NoLabels);
            all.AddRange(r.Events);
            worst = r.States.Worst;
        }

        Assert.Equal(ThresholdState.Critical, worst);
        ThresholdEvent e = Assert.Single(all);
        Assert.Equal("CRITICAL", e.Level);
        Assert.Equal("Tuuletin", e.Component);
    }

    [Fact]
    public void Tuuletin_JokaEiKoskaanPyorinyt_EiHalyta()
    {
        var monitor = Monitor();
        var never = new[] { new FanMetrics("Fan #7", 0f, "/fan/7") };

        for (int s = 0; s <= 20; s++)
        {
            ThresholdResult r = monitor.Update(Metrics(cpuTemp: 90, fans: never), T0.AddSeconds(s), NoLabels);
            Assert.DoesNotContain(r.Events, e => e.Component == "Tuuletin");
        }
    }

    [Fact]
    public void LevykohtainenTila_JaWorstKooste()
    {
        var monitor = Monitor();
        var disks = new[]
        {
            new DiskMetrics("SATA", 30f, null),
            new DiskMetrics("NVMe", 85f, null), // yli kriittisen (82)
        };

        ThresholdResult r = monitor.Update(Metrics(disks: disks), T0, NoLabels);

        Assert.Equal(ThresholdState.Normal, r.States.Disks[0]);
        Assert.Equal(ThresholdState.Critical, r.States.Disks[1]);
        Assert.Equal(ThresholdState.Critical, r.States.Worst);
    }
}
```

- [ ] **Step 2: RED.**
- [ ] **Step 3: Toteutus** `src/HardwareMonitor.Core/Analysis/ThresholdMonitor.cs`:

```csharp
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.Core.Analysis;

/// <summary>Mittarin välitön tila UI-värikoodausta varten.</summary>
public enum ThresholdState
{
    Normal,
    Warning,
    Critical,
}

/// <summary>Nostettu raja-arvotapahtuma (kirjataan events-tauluun).</summary>
public sealed record ThresholdEvent(
    string Level,
    string Component,
    string Sensor,
    double? Value,
    double? Threshold,
    string Message);

/// <summary>Välittömät tilat värikoodausta varten; Disks samassa järjestyksessä kuin KeyMetrics.Disks.</summary>
public sealed record MetricStates(
    ThresholdState CpuTemp,
    ThresholdState GpuTemp,
    ThresholdState GpuHotspot,
    ThresholdState RamLoad,
    IReadOnlyList<ThresholdState> Disks,
    ThresholdState Worst);

public sealed record ThresholdResult(MetricStates States, IReadOnlyList<ThresholdEvent> Events);

/// <summary>
/// Raja-arvovalvonta (määrittelyn luvut 16, 26, 31). Välitön tila muuttuu heti
/// rajan ylittyessä (käyttäjän pitää nähdä kuuma arvo), mutta tapahtuma
/// kirjataan vasta yhtäjaksoisen keston jälkeen — yksittäinen piikki ei ole
/// ongelma. Cooldown estää saman säännön toistuvan kirjautumisen; palautuessa
/// kirjataan INFO, jossa ylityksen kesto ja huippuarvo.
/// Puhdas tilakone: kello annetaan parametrina, joten kaikki on testattavissa.
/// </summary>
public sealed class ThresholdMonitor
{
    private readonly ThresholdSettings _s;
    private readonly Dictionary<string, RuleState> _rules = new();
    private readonly HashSet<string> _fansThatSpun = new();

    private sealed class RuleState
    {
        public DateTimeOffset? ExcursionStart;
        public DateTimeOffset? AboveWarnSince;
        public DateTimeOffset? AboveCritSince;
        public float Peak;
        public ThresholdState RaisedLevel;
        public DateTimeOffset? LastWarningEvent;
        public DateTimeOffset? LastCriticalEvent;
    }

    public ThresholdMonitor(ThresholdSettings settings) => _s = settings;

    public ThresholdResult Update(
        KeyMetrics m, DateTimeOffset now, IReadOnlyDictionary<string, string> fanLabels)
    {
        var events = new List<ThresholdEvent>();

        ThresholdState cpu = Check("cpu_temp", m.CpuPackageTempC,
            _s.CpuWarningTemp, _s.CpuCriticalTemp,
            "CPU", "CPU Package", "CPU-lämpötila", "°C", now, events);
        ThresholdState gpu = Check("gpu_temp", m.GpuTempC,
            _s.GpuWarningTemp, _s.GpuCriticalTemp,
            "GPU", "GPU Core", "GPU-lämpötila", "°C", now, events);
        ThresholdState hotspot = Check("gpu_hotspot", m.GpuHotspotTempC,
            _s.GpuHotspotWarningTemp, _s.GpuHotspotCriticalTemp,
            "GPU", "GPU Hot Spot", "GPU hotspot -lämpötila", "°C", now, events);
        ThresholdState ram = Check("ram_load", m.RamLoadPercent,
            _s.RamWarningPercent, _s.RamCriticalPercent,
            "RAM", "Memory", "RAM-käyttö", "%", now, events);

        var disks = new List<ThresholdState>();
        for (int i = 0; i < m.Disks.Count; i++)
        {
            DiskMetrics disk = m.Disks[i];
            disks.Add(Check($"disk:{i}", disk.TemperatureC,
                _s.NvmeWarningTemp, _s.NvmeCriticalTemp,
                "Levy", disk.Name, $"Levyn {disk.Name} lämpötila", "°C", now, events));
        }

        ThresholdState fans = CheckFans(m, now, fanLabels, events);

        ThresholdState worst = disks.Append(cpu).Append(gpu).Append(hotspot)
            .Append(ram).Append(fans).Max();

        return new ThresholdResult(new MetricStates(cpu, gpu, hotspot, ram, disks, worst), events);
    }

    private ThresholdState Check(
        string key, float? value, float warn, float crit,
        string component, string sensor, string label, string unit,
        DateTimeOffset now, List<ThresholdEvent> events)
    {
        RuleState state = GetRule(key);

        ThresholdState instant = value is { } v
            ? v >= crit ? ThresholdState.Critical
              : v >= warn ? ThresholdState.Warning
              : ThresholdState.Normal
            : ThresholdState.Normal;

        if (instant == ThresholdState.Normal)
        {
            CloseExcursion(state, component, sensor, label, unit, warn, now, events);
            return instant;
        }

        float current = value!.Value;
        state.ExcursionStart ??= now;
        state.Peak = Math.Max(state.Peak, current);
        state.AboveWarnSince = current >= warn ? state.AboveWarnSince ?? now : null;
        state.AboveCritSince = current >= crit ? state.AboveCritSince ?? now : null;

        if (state.AboveCritSince is { } critSince
            && (now - critSince).TotalSeconds >= _s.CriticalSustainSeconds
            && state.RaisedLevel < ThresholdState.Critical
            && CooldownOk(state.LastCriticalEvent, now))
        {
            events.Add(new ThresholdEvent("CRITICAL", component, sensor, current, crit,
                $"{label} ylitti kriittisen rajan: {current:0} {unit} (raja {crit:0} {unit})"));
            state.RaisedLevel = ThresholdState.Critical;
            state.LastCriticalEvent = now;
        }
        else if (state.AboveWarnSince is { } warnSince
            && (now - warnSince).TotalSeconds >= _s.WarningSustainSeconds
            && state.RaisedLevel < ThresholdState.Warning
            && CooldownOk(state.LastWarningEvent, now))
        {
            events.Add(new ThresholdEvent("WARNING", component, sensor, current, warn,
                $"{label} ylitti varoitusrajan: {current:0} {unit} (raja {warn:0} {unit})"));
            state.RaisedLevel = ThresholdState.Warning;
            state.LastWarningEvent = now;
        }

        return instant;
    }

    private ThresholdState CheckFans(
        KeyMetrics m, DateTimeOffset now,
        IReadOnlyDictionary<string, string> fanLabels, List<ThresholdEvent> events)
    {
        ThresholdState worst = ThresholdState.Normal;

        foreach (FanMetrics fan in m.Fans)
        {
            if (fan.Rpm is { } rpm && rpm > 200)
            {
                _fansThatSpun.Add(fan.Identifier);
            }

            string name = fanLabels.TryGetValue(fan.Identifier, out string? label)
                          && label.Length > 0 ? label : fan.Name;
            RuleState state = GetRule($"fan:{fan.Identifier}");

            bool stoppedWhileHot =
                _fansThatSpun.Contains(fan.Identifier)
                && fan.Rpm is { } r && r <= 0
                && m.CpuPackageTempC is { } cpuTemp && cpuTemp >= _s.FanStopCpuTemp;

            if (stoppedWhileHot)
            {
                worst = ThresholdState.Critical;
                state.ExcursionStart ??= now;
                state.AboveCritSince ??= now;
                state.Peak = Math.Max(state.Peak, m.CpuPackageTempC ?? 0);

                if ((now - state.AboveCritSince.Value).TotalSeconds >= _s.CriticalSustainSeconds
                    && state.RaisedLevel < ThresholdState.Critical
                    && CooldownOk(state.LastCriticalEvent, now))
                {
                    events.Add(new ThresholdEvent("CRITICAL", "Tuuletin", name, 0, null,
                        $"Tuuletin {name} on pysähtynyt (0 RPM) CPU:n ollessa {m.CpuPackageTempC:0} °C"));
                    state.RaisedLevel = ThresholdState.Critical;
                    state.LastCriticalEvent = now;
                }
            }
            else if (state.ExcursionStart is not null)
            {
                if (state.RaisedLevel == ThresholdState.Critical)
                {
                    events.Add(new ThresholdEvent("INFO", "Tuuletin", name, fan.Rpm, null,
                        $"Tuuletin {name} pyörii taas"));
                }

                Reset(state);
            }
        }

        return worst;
    }

    private void CloseExcursion(
        RuleState state, string component, string sensor, string label, string unit,
        float warnLimit, DateTimeOffset now, List<ThresholdEvent> events)
    {
        if (state.ExcursionStart is { } start && state.RaisedLevel != ThresholdState.Normal)
        {
            events.Add(new ThresholdEvent("INFO", component, sensor, state.Peak, warnLimit,
                $"{label} palautui normaaliksi (huippu {state.Peak:0} {unit}, kesto {FormatDuration(now - start)})"));
        }

        Reset(state);
    }

    private static void Reset(RuleState state)
    {
        state.ExcursionStart = null;
        state.AboveWarnSince = null;
        state.AboveCritSince = null;
        state.Peak = 0;
        state.RaisedLevel = ThresholdState.Normal;
    }

    private RuleState GetRule(string key)
    {
        if (!_rules.TryGetValue(key, out RuleState? state))
        {
            state = new RuleState();
            _rules[key] = state;
        }

        return state;
    }

    private bool CooldownOk(DateTimeOffset? lastEvent, DateTimeOffset now) =>
        lastEvent is not { } last || (now - last).TotalMinutes >= _s.EventCooldownMinutes;

    private static string FormatDuration(TimeSpan d) =>
        d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes} min {d.Seconds} s" : $"{(int)d.TotalSeconds} s";
}
```

- [ ] **Step 4: GREEN + commit** `"Lisää ThresholdMonitor-tilakone kestolla ja cooldownilla (TDD)"`

---

### Task 3: UI-värit, tapahtumien kirjaus ja päästä päähän -todennus

**Files:**
- Create: `src/HardwareMonitor.App/ThresholdStateToBrushConverter.cs`
- Modify: `src/HardwareMonitor.App/ViewModels/DashboardViewModel.cs` (tilaproperyt + DiskRowViewModel)
- Modify: `src/HardwareMonitor.App/ViewModels/OverlayViewModel.cs` (BorderBrush)
- Modify: `src/HardwareMonitor.App/OverlayWindow.xaml` (+ .cs: move-moden ClearValue)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (konvertteri + värisidonnat)
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (monitor + kirjaus)
- Modify: `docs/ROADMAP.md`

**Interfaces:**
- Consumes: Task 2:n tyypit; `EventLogService.Info/Warning/Critical` (Vaihe 3).
- Produces: `DashboardViewModel.ApplyStates(MetricStates)` (kutsutaan Updaten jälkeen);
  `DiskRowViewModel { string Text; ThresholdState State }` (INPC);
  `OverlayViewModel.SetWorstState(ThresholdState)`.

- [ ] **Step 1: Konvertteri** (`ThresholdStateToBrushConverter.cs`): IValueConverter,
  Normal → #A5D6A7, Warning → #FFB74D, Critical → #EF5350 (SolidColorBrush, Freeze).
  Parametrilla "border": Normal → Transparent (overlayn reunusta varten).
- [ ] **Step 2: DashboardViewModel**: `ThresholdState`-properyt CpuTempState,
  GpuTempState, GpuHotspotState, RamLoadState (INPC, sama Set-kaava);
  `Disks`-kokoelma `ObservableCollection<DiskRowViewModel>`iksi (Text+State,
  synkkaus paikallaan kuten SyncRows); `ApplyStates(MetricStates states)` päivittää
  kaikki. XAML: CardValue-arvoille `Foreground="{Binding CpuTempState, Converter=...}"`
  (CpuTemp/GpuTemp/GpuHotspot/RamLoad + levyrivit).
- [ ] **Step 3: Overlay**: OverlayViewModelille `Brush BorderBrush` (INPC) +
  `SetWorstState`; OverlayWindow.xaml: `BorderBrush="{Binding BorderBrush}"`;
  move-mode palauttaa sidonnan `Panel.ClearValue(Border.BorderBrushProperty)`
  eikä aseta Transparentia.
- [ ] **Step 4: MainViewModel**: kenttä `_thresholdMonitor = new ThresholdMonitor(_settings.Thresholds)`
  (ctor); Refreshissä metricsin jälkeen:

```csharp
            ThresholdResult thresholds = _thresholdMonitor.Update(metrics, DateTimeOffset.Now, _settings.FanLabels);
            Dashboard.ApplyStates(thresholds.States);
            Overlay.SetWorstState(thresholds.States.Worst);

            foreach (ThresholdEvent alert in thresholds.Events)
            {
                _logger.Log($"[{alert.Level}] {alert.Message}");
                switch (alert.Level)
                {
                    case "CRITICAL":
                        _events?.Critical(alert.Component, alert.Message, alert.Sensor, alert.Value, alert.Threshold);
                        break;
                    case "WARNING":
                        _events?.Warning(alert.Component, alert.Message, alert.Sensor, alert.Value, alert.Threshold);
                        break;
                    default:
                        _events?.Info(alert.Component, alert.Message, alert.Sensor, alert.Value, alert.Threshold);
                        break;
                }
            }
```

- [ ] **Step 5: Buildaa + kaikki testit.**
- [ ] **Step 6: Päästä päähän -todennus**: laske settings.jsonissa CpuWarningTemp=30
  (appi kiinni ensin), käynnistä, odota >30 s → CPU-lämpö oranssina kortissa,
  overlayn reunus oranssi, debug-lokissa `[WARNING] CPU-lämpötila ylitti...`;
  palauta raja 85:een ja käynnistä uudelleen. Ruutukaappaus todisteeksi.
- [ ] **Step 7: ROADMAP + commit** `"Kytke raja-arvovalvonta UI-väreihin ja tapahtumalokiin (Vaihe 4)"`
