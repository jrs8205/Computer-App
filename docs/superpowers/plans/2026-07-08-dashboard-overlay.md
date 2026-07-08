# Dashboard (Vaihe 2) + Työpöytäoverlay (Vaihe 2.5) — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pääikkunaan Dashboard-korttinäkymä ja työpöydälle läpi-klikattava always-on-top-overlay, jotka molemmat saavat datansa uudesta `KeyMetricsService`-palvelusta.

**Architecture:** `KeyMetricsService` (Core) poimii raakasensoripuusta tärkeimmät arvot `KeyMetrics`-olioksi kerran sekunnissa `MainViewModel.Refresh()`-syklissä. Dashboard ja overlay ovat saman datan kaksi näkymää. Asetukset tallentuvat JSON-tiedostoon (`SettingsService`, Core).

**Tech Stack:** .NET 8 (net8.0-windows), WPF, MVVM ilman kirjastoja, System.Text.Json, xUnit (uusi testiprojekti). Spec: `docs/superpowers/specs/2026-07-08-overlay-design.md`.

## Global Constraints

- TargetFramework kaikissa projekteissa: `net8.0-windows`; `Nullable` ja `ImplicitUsings` päällä, `LangVersion latest`.
- Koodityyli: file-scoped namespace, `sealed`-luokat, suomenkieliset XML-doc-kommentit (kuten olemassa oleva koodi).
- Ei uusia NuGet-riippuvuuksia App/Core-projekteihin (System.Text.Json kuuluu .NET 8:aan). Testiprojektiin: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`.
- Commit-viestit suomeksi, loppuun rivi: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Testikomento repon juuressa: `dotnet test HardwareMonitor.sln`.
- Kaikki nullable-arvot: puuttuva sensori EI ole virhe — arvo on `null` ja UI näyttää "—".

---

### Task 1: Testiprojekti + KeyMetrics-malli + CPU/RAM-poiminta (TDD)

**Files:**
- Create: `src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj`
- Create: `src/HardwareMonitor.Tests/Metrics/KeyMetricsServiceTests.cs`
- Create: `src/HardwareMonitor.Core/Metrics/KeyMetrics.cs`
- Create: `src/HardwareMonitor.Core/Metrics/KeyMetricsService.cs`
- Modify: `HardwareMonitor.sln` (lisää testiprojekti: `dotnet sln add`)

**Interfaces:**
- Consumes: `HardwareGroup(Name, HardwareType, Sensors, SubHardware)`, `SensorReading(HardwareName, HardwareType, SensorName, SensorType, Value, Unit, Identifier)` (Core/Sensors).
- Produces: `KeyMetrics`-record ja `static KeyMetrics KeyMetricsService.Extract(IReadOnlyList<HardwareGroup> groups)` — Taskit 2, 4 ja 5 käyttävät näitä.

- [ ] **Step 1: Luo testiprojekti ja lisää se ratkaisuun**

`src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\HardwareMonitor.Core\HardwareMonitor.Core.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet sln HardwareMonitor.sln add src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj`

- [ ] **Step 2: Kirjoita epäonnistuvat testit (CPU, RAM, tyhjä syöte)**

`src/HardwareMonitor.Tests/Metrics/KeyMetricsServiceTests.cs`:

```csharp
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Sensors;

namespace HardwareMonitor.Tests.Metrics;

public class KeyMetricsServiceTests
{
    private static SensorReading Reading(
        string hwName, string hwType, string sensorName, string sensorType, float? value) =>
        new(hwName, hwType, sensorName, sensorType, value, "",
            $"/{hwType}/{hwName}/{sensorType}/{sensorName}".ToLowerInvariant());

    private static HardwareGroup Group(
        string name, string type, SensorReading[] sensors, HardwareGroup[]? subs = null) =>
        new(name, type, sensors, subs ?? Array.Empty<HardwareGroup>());

    [Fact]
    public void Extract_TyhjaLista_PalauttaaNullArvotEikaHeitaPoikkeusta()
    {
        KeyMetrics m = KeyMetricsService.Extract(Array.Empty<HardwareGroup>());

        Assert.Null(m.CpuLoadPercent);
        Assert.Null(m.CpuPackageTempC);
        Assert.Null(m.RamLoadPercent);
        Assert.Empty(m.Disks);
        Assert.Empty(m.Fans);
    }

    [Fact]
    public void Extract_PoimiiCpuArvot()
    {
        var cpu = Group("Intel Core i9-9900K", "Cpu", new[]
        {
            Reading("i9", "Cpu", "CPU Total", "Load", 42f),
            Reading("i9", "Cpu", "CPU Core #1 Thread #1", "Load", 80f),
            Reading("i9", "Cpu", "CPU Package", "Temperature", 65f),
            Reading("i9", "Cpu", "CPU Core #1", "Clock", 4700f),
            Reading("i9", "Cpu", "CPU Core #2", "Clock", 4600f),
            Reading("i9", "Cpu", "CPU Package", "Power", 112f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { cpu });

        Assert.Equal(42f, m.CpuLoadPercent);
        Assert.Equal(65f, m.CpuPackageTempC);
        Assert.Equal(4700f, m.CpuMaxClockMhz);
        Assert.Equal(112f, m.CpuPackagePowerW);
    }

    [Fact]
    public void Extract_CpuIlmanPackageLampoa_KayttaaCoreMaxia()
    {
        var cpu = Group("CPU", "Cpu", new[]
        {
            Reading("cpu", "Cpu", "Core Max", "Temperature", 71f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { cpu });

        Assert.Equal(71f, m.CpuPackageTempC);
    }

    [Fact]
    public void Extract_PoimiiRamArvot()
    {
        var ram = Group("Generic Memory", "Memory", new[]
        {
            Reading("ram", "Memory", "Memory", "Load", 19.1f),
            Reading("ram", "Memory", "Memory Used", "Data", 12.2f),
            Reading("ram", "Memory", "Memory Available", "Data", 51.7f),
            Reading("ram", "Memory", "Virtual Memory", "Load", 99f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { ram });

        Assert.Equal(19.1f, m.RamLoadPercent);
        Assert.Equal(12.2f, m.RamUsedGb);
        Assert.Equal(51.7f, m.RamAvailableGb);
    }
}
```

- [ ] **Step 3: Aja testit ja varmista että ne EPÄONNISTUVAT käännösvirheeseen**

Run: `dotnet test HardwareMonitor.sln`
Expected: FAIL — `KeyMetrics`/`KeyMetricsService` ei ole olemassa (CS0246).

- [ ] **Step 4: Toteuta KeyMetrics-malli**

`src/HardwareMonitor.Core/Metrics/KeyMetrics.cs`:

```csharp
namespace HardwareMonitor.Core.Metrics;

/// <summary>Yhden levyn tärkeimmät arvot (nimi, lämpö, aktiivisuus).</summary>
public sealed record DiskMetrics(string Name, float? TemperatureC, float? ActivityPercent);

/// <summary>Yhden tuulettimen nimi ja kierrosnopeus.</summary>
public sealed record FanMetrics(string Name, float? Rpm);

/// <summary>
/// Tärkeimmät arvot valmiiksi poimittuina (specin Vaihe 2 / 2.5). Kaikki arvot
/// ovat nullable: puuttuva sensori ei ole virhe, vaan UI näyttää "—".
/// Dashboard ja overlay käyttävät molemmat tätä samaa oliota.
/// </summary>
public sealed record KeyMetrics(
    float? CpuLoadPercent,
    float? CpuPackageTempC,
    float? CpuMaxClockMhz,
    float? CpuPackagePowerW,
    float? GpuLoadPercent,
    float? GpuTempC,
    float? GpuHotspotTempC,
    float? GpuMemoryUsedMb,
    float? GpuMemoryTotalMb,
    float? GpuPowerW,
    float? RamLoadPercent,
    float? RamUsedGb,
    float? RamAvailableGb,
    IReadOnlyList<DiskMetrics> Disks,
    IReadOnlyList<FanMetrics> Fans);
```

- [ ] **Step 5: Toteuta KeyMetricsService (CPU + RAM; GPU/levyt/tuulettimet tulevat Task 2:ssa)**

`src/HardwareMonitor.Core/Metrics/KeyMetricsService.cs`:

```csharp
using HardwareMonitor.Core.Sensors;

namespace HardwareMonitor.Core.Metrics;

/// <summary>
/// Poimii raakasensoripuusta (HardwareGroup-lista) tärkeimmät arvot KeyMetrics-olioksi.
/// Poiminta perustuu sensorityyppiin ja LibreHardwareMonitorin vakiintuneisiin
/// sensorinimiin (esim. "CPU Package", "GPU Hot Spot"). Puhdas funktio: ei tilaa,
/// ei poikkeuksia puuttuvasta datasta.
/// </summary>
public static class KeyMetricsService
{
    public static KeyMetrics Extract(IReadOnlyList<HardwareGroup> groups)
    {
        float? cpuLoad = null, cpuPackageTemp = null, cpuCoreMaxTemp = null,
               cpuClock = null, cpuPower = null;
        float? ramLoad = null, ramUsed = null, ramAvailable = null;

        foreach (HardwareGroup group in groups)
        {
            switch (group.HardwareType)
            {
                case "Cpu":
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Load" when s.SensorName == "CPU Total":
                                cpuLoad = s.Value;
                                break;
                            case "Temperature" when s.SensorName == "CPU Package":
                                cpuPackageTemp = s.Value;
                                break;
                            case "Temperature" when s.SensorName == "Core Max":
                                cpuCoreMaxTemp = s.Value;
                                break;
                            case "Clock" when s.SensorName.StartsWith("CPU Core", StringComparison.Ordinal):
                                if (s.Value is { } clock && (cpuClock is not { } max || clock > max))
                                {
                                    cpuClock = clock;
                                }
                                break;
                            case "Power" when s.SensorName is "CPU Package" or "Package":
                                cpuPower = s.Value;
                                break;
                        }
                    }
                    break;

                case "Memory":
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Load" when s.SensorName == "Memory":
                                ramLoad = s.Value;
                                break;
                            case "Data" when s.SensorName == "Memory Used":
                                ramUsed = s.Value;
                                break;
                            case "Data" when s.SensorName == "Memory Available":
                                ramAvailable = s.Value;
                                break;
                        }
                    }
                    break;
            }
        }

        return new KeyMetrics(
            CpuLoadPercent: cpuLoad,
            CpuPackageTempC: cpuPackageTemp ?? cpuCoreMaxTemp,
            CpuMaxClockMhz: cpuClock,
            CpuPackagePowerW: cpuPower,
            GpuLoadPercent: null,
            GpuTempC: null,
            GpuHotspotTempC: null,
            GpuMemoryUsedMb: null,
            GpuMemoryTotalMb: null,
            GpuPowerW: null,
            RamLoadPercent: ramLoad,
            RamUsedGb: ramUsed,
            RamAvailableGb: ramAvailable,
            Disks: Array.Empty<DiskMetrics>(),
            Fans: Array.Empty<FanMetrics>());
    }
}
```

- [ ] **Step 6: Aja testit ja varmista että ne MENEVÄT LÄPI**

Run: `dotnet test HardwareMonitor.sln`
Expected: PASS (4 testiä).

- [ ] **Step 7: Commit**

```bash
git add HardwareMonitor.sln src/HardwareMonitor.Tests src/HardwareMonitor.Core/Metrics
git commit -m "Lisää testiprojekti ja KeyMetricsService: CPU- ja RAM-poiminta (TDD)"
```

---

### Task 2: KeyMetricsService — GPU, levyt ja tuulettimet (TDD)

**Files:**
- Modify: `src/HardwareMonitor.Tests/Metrics/KeyMetricsServiceTests.cs` (lisää testit)
- Modify: `src/HardwareMonitor.Core/Metrics/KeyMetricsService.cs` (laajenna Extract)

**Interfaces:**
- Consumes: Task 1:n `KeyMetrics`, `KeyMetricsService.Extract`.
- Produces: täysi `Extract`-toteutus — GPU-arvot, `Disks`- ja `Fans`-listat täytettyinä.

- [ ] **Step 1: Lisää epäonnistuvat testit (GPU, levyt, tuulettimet)**

Lisää `KeyMetricsServiceTests.cs`-luokkaan:

```csharp
    [Fact]
    public void Extract_PoimiiGpuArvotHotspotinKanssa()
    {
        var gpu = Group("NVIDIA GeForce RTX 2060", "GpuNvidia", new[]
        {
            Reading("gpu", "GpuNvidia", "GPU Core", "Load", 33f),
            Reading("gpu", "GpuNvidia", "GPU Core", "Temperature", 49f),
            Reading("gpu", "GpuNvidia", "GPU Hot Spot", "Temperature", 62f),
            Reading("gpu", "GpuNvidia", "GPU Memory Used", "SmallData", 975f),
            Reading("gpu", "GpuNvidia", "GPU Memory Total", "SmallData", 6144f),
            Reading("gpu", "GpuNvidia", "GPU Package", "Power", 17.7f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { gpu });

        Assert.Equal(33f, m.GpuLoadPercent);
        Assert.Equal(49f, m.GpuTempC);
        Assert.Equal(62f, m.GpuHotspotTempC);
        Assert.Equal(975f, m.GpuMemoryUsedMb);
        Assert.Equal(6144f, m.GpuMemoryTotalMb);
        Assert.Equal(17.7f, m.GpuPowerW);
    }

    [Fact]
    public void Extract_GpuIlmanHotspotia_HotspotOnNull()
    {
        var gpu = Group("Intel UHD", "GpuIntel", new[]
        {
            Reading("gpu", "GpuIntel", "GPU Core", "Temperature", 40f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { gpu });

        Assert.Equal(40f, m.GpuTempC);
        Assert.Null(m.GpuHotspotTempC);
    }

    [Fact]
    public void Extract_UseaLevy_KaikkiListassaJaAktiivisuusOnReadWriteMaksimi()
    {
        var ssd1 = Group("Samsung SSD 860 EVO 1TB", "Storage", new[]
        {
            Reading("ssd1", "Storage", "Temperature", "Temperature", 28f),
            Reading("ssd1", "Storage", "Read Activity", "Load", 5f),
            Reading("ssd1", "Storage", "Write Activity", "Load", 12f),
            Reading("ssd1", "Storage", "Total Activity", "Load", 100f),
        });
        var ssd2 = Group("Samsung SSD 970 EVO Plus 1TB", "Storage", new[]
        {
            Reading("ssd2", "Storage", "Temperature", "Temperature", 62f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { ssd1, ssd2 });

        Assert.Equal(2, m.Disks.Count);
        Assert.Equal("Samsung SSD 860 EVO 1TB", m.Disks[0].Name);
        Assert.Equal(28f, m.Disks[0].TemperatureC);
        Assert.Equal(12f, m.Disks[0].ActivityPercent); // max(read, write), EI "Total Activity"
        Assert.Equal(62f, m.Disks[1].TemperatureC);
        Assert.Null(m.Disks[1].ActivityPercent);
    }

    [Fact]
    public void Extract_KeraaTuulettimetMyosAlalaitteista()
    {
        var superIo = Group("Nuvoton NCT6798D", "SuperIO", new[]
        {
            Reading("io", "SuperIO", "Fan #1", "Fan", 579f),
            Reading("io", "SuperIO", "Fan #2", "Fan", 1942f),
        });
        var motherboard = Group("ASUS Z390-F", "Motherboard",
            Array.Empty<SensorReading>(), new[] { superIo });
        var gpu = Group("RTX 2060", "GpuNvidia", new[]
        {
            Reading("gpu", "GpuNvidia", "GPU Fan 1", "Fan", 0f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { motherboard, gpu });

        Assert.Equal(3, m.Fans.Count);
        Assert.Contains(m.Fans, f => f.Name == "Fan #2" && f.Rpm == 1942f);
        Assert.Contains(m.Fans, f => f.Name == "GPU Fan 1" && f.Rpm == 0f);
    }
```

- [ ] **Step 2: Aja testit ja varmista että uudet EPÄONNISTUVAT**

Run: `dotnet test HardwareMonitor.sln`
Expected: 4 uutta testiä FAIL (GPU-arvot ja listat null/tyhjiä), vanhat 4 PASS.

- [ ] **Step 3: Laajenna Extract-toteutus**

Korvaa `KeyMetricsService.cs`:n `Extract`-metodin runko kokonaan (CPU/Memory-caset säilyvät ennallaan, lisätään Gpu*/Storage-caset, tuuletinkeräys ja lopullinen return):

```csharp
    public static KeyMetrics Extract(IReadOnlyList<HardwareGroup> groups)
    {
        float? cpuLoad = null, cpuPackageTemp = null, cpuCoreMaxTemp = null,
               cpuClock = null, cpuPower = null;
        float? gpuLoad = null, gpuTemp = null, gpuHotspot = null,
               vramUsed = null, vramTotal = null, gpuPower = null;
        float? ramLoad = null, ramUsed = null, ramAvailable = null;
        var disks = new List<DiskMetrics>();
        var fans = new List<FanMetrics>();

        foreach (HardwareGroup group in groups)
        {
            switch (group.HardwareType)
            {
                case "Cpu":
                    // (Task 1:n silmukka ennallaan)
                    break;

                case "Memory":
                    // (Task 1:n silmukka ennallaan)
                    break;

                case "GpuNvidia" or "GpuAmd" or "GpuIntel":
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Load" when s.SensorName == "GPU Core":
                                gpuLoad ??= s.Value;
                                break;
                            case "Temperature" when s.SensorName == "GPU Core":
                                gpuTemp ??= s.Value;
                                break;
                            case "Temperature" when s.SensorName == "GPU Hot Spot":
                                gpuHotspot ??= s.Value;
                                break;
                            case "SmallData" when s.SensorName == "GPU Memory Used":
                                vramUsed ??= s.Value;
                                break;
                            case "SmallData" when s.SensorName == "GPU Memory Total":
                                vramTotal ??= s.Value;
                                break;
                            case "Power" when s.SensorName == "GPU Package":
                                gpuPower ??= s.Value;
                                break;
                        }
                    }
                    break;

                case "Storage":
                    float? diskTemp = null, readActivity = null, writeActivity = null;
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Temperature" when s.SensorName == "Temperature":
                                diskTemp = s.Value;
                                break;
                            case "Load" when s.SensorName == "Read Activity":
                                readActivity = s.Value;
                                break;
                            case "Load" when s.SensorName == "Write Activity":
                                writeActivity = s.Value;
                                break;
                        }
                    }

                    // "Total Activity" näyttää joillain levyillä aina 100 % — käytetään
                    // read/write-maksimia, joka kuvaa todellista aktiivisuutta.
                    float? activity = (readActivity, writeActivity) switch
                    {
                        ({ } r, { } w) => Math.Max(r, w),
                        ({ } r, null) => r,
                        (null, { } w) => w,
                        _ => null,
                    };
                    disks.Add(new DiskMetrics(group.Name, diskTemp, activity));
                    break;
            }

            CollectFans(group, fans);
        }

        return new KeyMetrics(
            CpuLoadPercent: cpuLoad,
            CpuPackageTempC: cpuPackageTemp ?? cpuCoreMaxTemp,
            CpuMaxClockMhz: cpuClock,
            CpuPackagePowerW: cpuPower,
            GpuLoadPercent: gpuLoad,
            GpuTempC: gpuTemp,
            GpuHotspotTempC: gpuHotspot,
            GpuMemoryUsedMb: vramUsed,
            GpuMemoryTotalMb: vramTotal,
            GpuPowerW: gpuPower,
            RamLoadPercent: ramLoad,
            RamUsedGb: ramUsed,
            RamAvailableGb: ramAvailable,
            Disks: disks,
            Fans: fans);
    }

    /// <summary>Kerää Fan-tyyppiset sensorit laitteesta ja sen alalaitteista rekursiivisesti.</summary>
    private static void CollectFans(HardwareGroup group, List<FanMetrics> fans)
    {
        foreach (SensorReading s in group.Sensors)
        {
            if (s.SensorType == "Fan")
            {
                fans.Add(new FanMetrics(s.SensorName, s.Value));
            }
        }

        foreach (HardwareGroup sub in group.SubHardware)
        {
            CollectFans(sub, fans);
        }
    }
```

Huom: GPU-arvoissa käytetään `??=`-operaattoria, jotta monen GPU:n koneessa
ensimmäinen arvon antava GPU voittaa eikä integroitu GPU ylikirjoita erillisen
näytönohjaimen arvoja.

- [ ] **Step 4: Aja testit ja varmista että KAIKKI menevät läpi**

Run: `dotnet test HardwareMonitor.sln`
Expected: PASS (8 testiä).

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.Tests src/HardwareMonitor.Core/Metrics
git commit -m "KeyMetricsService: GPU-, levy- ja tuuletinpoiminta (TDD)"
```

---

### Task 3: AppSettings + SettingsService (TDD)

**Files:**
- Create: `src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs`
- Create: `src/HardwareMonitor.Core/Settings/AppSettings.cs`
- Create: `src/HardwareMonitor.Core/Settings/SettingsService.cs`

**Interfaces:**
- Produces: `AppSettings { OverlaySettings Overlay }`, `OverlaySettings { bool Enabled; OverlayCorner Corner; int MarginPx; double Opacity; double FontSize; bool ShowCpu/ShowGpu/ShowRam/ShowDisks/ShowFans }`, `enum OverlayCorner { TopLeft, TopRight, BottomLeft, BottomRight }`, `SettingsService(string? directory).Load() -> AppSettings` ja `.Save(AppSettings)`. Taskit 5–6 käyttävät näitä.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

`src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs`:

```csharp
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.Tests.Settings;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "HardwareMonitorTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_TiedostoaEiOle_PalauttaaOletukset()
    {
        var service = new SettingsService(_dir);

        AppSettings settings = service.Load();

        Assert.False(settings.Overlay.Enabled);
        Assert.Equal(OverlayCorner.TopRight, settings.Overlay.Corner);
        Assert.Equal(0.85, settings.Overlay.Opacity);
        Assert.True(settings.Overlay.ShowCpu);
        Assert.False(settings.Overlay.ShowFans);
    }

    [Fact]
    public void SaveJaLoad_ArvotSailyvat()
    {
        var service = new SettingsService(_dir);
        var settings = new AppSettings();
        settings.Overlay.Enabled = true;
        settings.Overlay.Corner = OverlayCorner.BottomLeft;
        settings.Overlay.Opacity = 0.5;
        settings.Overlay.ShowDisks = false;

        service.Save(settings);
        AppSettings loaded = new SettingsService(_dir).Load();

        Assert.True(loaded.Overlay.Enabled);
        Assert.Equal(OverlayCorner.BottomLeft, loaded.Overlay.Corner);
        Assert.Equal(0.5, loaded.Overlay.Opacity);
        Assert.False(loaded.Overlay.ShowDisks);
    }

    [Fact]
    public void Load_VioittunutTiedosto_PalauttaaOletuksetEikaHeitaPoikkeusta()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ tämä ei ole jsonia");
        var service = new SettingsService(_dir);

        AppSettings settings = service.Load();

        Assert.False(settings.Overlay.Enabled);
    }
}
```

- [ ] **Step 2: Aja testit ja varmista että ne EPÄONNISTUVAT käännösvirheeseen**

Run: `dotnet test HardwareMonitor.sln`
Expected: FAIL (CS0246: SettingsService/AppSettings puuttuvat).

- [ ] **Step 3: Toteuta AppSettings ja SettingsService**

`src/HardwareMonitor.Core/Settings/AppSettings.cs`:

```csharp
namespace HardwareMonitor.Core.Settings;

/// <summary>Näytön kulma, johon overlay asemoidaan.</summary>
public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Overlayn käyttäjäasetukset (specin Vaihe 2.5).</summary>
public sealed class OverlaySettings
{
    public bool Enabled { get; set; }
    public OverlayCorner Corner { get; set; } = OverlayCorner.TopRight;
    public int MarginPx { get; set; } = 16;
    public double Opacity { get; set; } = 0.85;
    public double FontSize { get; set; } = 14;
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowDisks { get; set; } = true;
    public bool ShowFans { get; set; }
}

/// <summary>
/// Sovelluksen kaikki asetukset. Laajenee Vaihe 4:ssä raja-arvoilla (specin luku 29).
/// </summary>
public sealed class AppSettings
{
    public OverlaySettings Overlay { get; set; } = new();
}
```

`src/HardwareMonitor.Core/Settings/SettingsService.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HardwareMonitor.Core.Settings;

/// <summary>
/// Lataa ja tallentaa asetukset JSON-tiedostoon
/// (%LOCALAPPDATA%\HardwareMonitor\settings.json). Vioittunut tai puuttuva
/// tiedosto ei ole virhe: silloin palautetaan oletusasetukset.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsService(string? directory = null)
    {
        directory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor");

        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options)
                   ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
    }
}
```

- [ ] **Step 4: Aja testit ja varmista että KAIKKI menevät läpi**

Run: `dotnet test HardwareMonitor.sln`
Expected: PASS (11 testiä).

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.Tests/Settings src/HardwareMonitor.Core/Settings
git commit -m "Lisää AppSettings ja SettingsService JSON-tallennuksella (TDD)"
```

---

### Task 4: Dashboard-välilehti pääikkunaan

**Files:**
- Create: `src/HardwareMonitor.App/ViewModels/DashboardViewModel.cs`
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (Dashboard-property + Refresh-kytkentä)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (TabControl: Dashboard + Kaikki sensorit)

**Interfaces:**
- Consumes: `KeyMetricsService.Extract`, `KeyMetrics` (Task 1–2).
- Produces: `DashboardViewModel.Update(KeyMetrics)`; `MainViewModel.Dashboard`-property; `MainViewModel.Refresh()` kutsuu `KeyMetricsService.Extract(groups)` ja välittää tuloksen eteenpäin (Task 6 lisää saman overlaylle).

- [ ] **Step 1: Toteuta DashboardViewModel**

`src/HardwareMonitor.App/ViewModels/DashboardViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using HardwareMonitor.Core.Metrics;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Dashboard-korttien näkymämalli: KeyMetrics-arvot valmiiksi muotoiltuina
/// merkkijonoina. Puuttuva arvo näytetään aina "—":na (specin virheenkäsittely).
/// </summary>
public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private string _cpuLoad = "—", _cpuTemp = "—", _cpuClock = "—", _cpuPower = "—";
    private string _gpuLoad = "—", _gpuTemp = "—", _gpuHotspot = "—", _gpuVram = "—", _gpuPower = "—";
    private string _ramLoad = "—", _ramUsed = "—";

    public string CpuLoad { get => _cpuLoad; private set => Set(ref _cpuLoad, value, nameof(CpuLoad)); }
    public string CpuTemp { get => _cpuTemp; private set => Set(ref _cpuTemp, value, nameof(CpuTemp)); }
    public string CpuClock { get => _cpuClock; private set => Set(ref _cpuClock, value, nameof(CpuClock)); }
    public string CpuPower { get => _cpuPower; private set => Set(ref _cpuPower, value, nameof(CpuPower)); }
    public string GpuLoad { get => _gpuLoad; private set => Set(ref _gpuLoad, value, nameof(GpuLoad)); }
    public string GpuTemp { get => _gpuTemp; private set => Set(ref _gpuTemp, value, nameof(GpuTemp)); }
    public string GpuHotspot { get => _gpuHotspot; private set => Set(ref _gpuHotspot, value, nameof(GpuHotspot)); }
    public string GpuVram { get => _gpuVram; private set => Set(ref _gpuVram, value, nameof(GpuVram)); }
    public string GpuPower { get => _gpuPower; private set => Set(ref _gpuPower, value, nameof(GpuPower)); }
    public string RamLoad { get => _ramLoad; private set => Set(ref _ramLoad, value, nameof(RamLoad)); }
    public string RamUsed { get => _ramUsed; private set => Set(ref _ramUsed, value, nameof(RamUsed)); }

    public ObservableCollection<string> Disks { get; } = new();
    public ObservableCollection<string> Fans { get; } = new();

    public void Update(KeyMetrics m)
    {
        CpuLoad = Fmt(m.CpuLoadPercent, "%");
        CpuTemp = Fmt(m.CpuPackageTempC, "°C");
        CpuClock = Fmt(m.CpuMaxClockMhz, "MHz");
        CpuPower = Fmt(m.CpuPackagePowerW, "W");
        GpuLoad = Fmt(m.GpuLoadPercent, "%");
        GpuTemp = Fmt(m.GpuTempC, "°C");
        GpuHotspot = Fmt(m.GpuHotspotTempC, "°C");
        GpuVram = m.GpuMemoryUsedMb is { } used && m.GpuMemoryTotalMb is { } total
            ? $"{used:0} / {total:0} MB"
            : "—";
        GpuPower = Fmt(m.GpuPowerW, "W");
        RamLoad = Fmt(m.RamLoadPercent, "%");
        RamUsed = m.RamUsedGb is { } gb ? $"{gb.ToString("0.0", CultureInfo.CurrentCulture)} GB" : "—";

        SyncRows(Disks, m.Disks.Select(d =>
            $"{d.Name}   {Fmt(d.TemperatureC, "°C")}   {Fmt(d.ActivityPercent, "%")}"));
        SyncRows(Fans, m.Fans.Select(f => $"{f.Name}   {Fmt(f.Rpm, "RPM")}"));
    }

    private static string Fmt(float? value, string unit) =>
        value is { } v ? $"{v.ToString("0", CultureInfo.CurrentCulture)} {unit}" : "—";

    /// <summary>Päivittää listan rivit paikallaan, jotta UI ei vilku joka sekunti.</summary>
    private static void SyncRows(ObservableCollection<string> target, IEnumerable<string> rows)
    {
        var list = rows.ToList();
        while (target.Count > list.Count)
        {
            target.RemoveAt(target.Count - 1);
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (i >= target.Count)
            {
                target.Add(list[i]);
            }
            else if (target[i] != list[i])
            {
                target[i] = list[i];
            }
        }
    }

    private void Set(ref string field, string value, string name)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

- [ ] **Step 2: Kytke MainViewModeliin**

`MainViewModel.cs`: lisää using `HardwareMonitor.Core.Metrics;`, property ja kutsu:

```csharp
    public DashboardViewModel Dashboard { get; } = new();
```

ja `Refresh()`-metodiin heti `IReadOnlyList<HardwareGroup> groups = _sensorService.Read();` -rivin jälkeen:

```csharp
            KeyMetrics metrics = KeyMetricsService.Extract(groups);
            Dashboard.Update(metrics);
```

- [ ] **Step 3: Lisää TabControl MainWindow.xamliin**

Korvaa `MainWindow.xaml`:n `<!-- Sensoripuu -->` -osio (TreeView) tällä (TreeView siirtyy välilehden sisään sellaisenaan; Window.Resources-osioon lisätään korttistyyli):

Lisää `<Window.Resources>`-osioon:

```xml
        <Style x:Key="Card" TargetType="Border">
            <Setter Property="Background" Value="#252526" />
            <Setter Property="CornerRadius" Value="8" />
            <Setter Property="Padding" Value="16" />
            <Setter Property="Margin" Value="8" />
            <Setter Property="MinWidth" Value="220" />
        </Style>
        <Style x:Key="CardTitle" TargetType="TextBlock">
            <Setter Property="FontSize" Value="15" />
            <Setter Property="FontWeight" Value="Bold" />
            <Setter Property="Foreground" Value="#4FC3F7" />
            <Setter Property="Margin" Value="0,0,0,8" />
        </Style>
        <Style x:Key="CardLabel" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#9E9E9E" />
        </Style>
        <Style x:Key="CardValue" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#A5D6A7" />
            <Setter Property="FontFamily" Value="Consolas" />
            <Setter Property="HorizontalAlignment" Value="Right" />
        </Style>
```

TreeViewin tilalle:

```xml
        <TabControl Background="#1E1E1E" BorderThickness="0">
            <TabItem Header="Dashboard">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <WrapPanel Margin="8" DataContext="{Binding Dashboard}">

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Style="{StaticResource CardTitle}" Text="CPU" />
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                    </Grid.ColumnDefinitions>
                                    <Grid.RowDefinitions>
                                        <RowDefinition /><RowDefinition /><RowDefinition /><RowDefinition />
                                    </Grid.RowDefinitions>
                                    <TextBlock Grid.Row="0" Style="{StaticResource CardLabel}" Text="Käyttö" />
                                    <TextBlock Grid.Row="0" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding CpuLoad}" />
                                    <TextBlock Grid.Row="1" Style="{StaticResource CardLabel}" Text="Lämpötila" />
                                    <TextBlock Grid.Row="1" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding CpuTemp}" />
                                    <TextBlock Grid.Row="2" Style="{StaticResource CardLabel}" Text="Kello (max)" />
                                    <TextBlock Grid.Row="2" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding CpuClock}" />
                                    <TextBlock Grid.Row="3" Style="{StaticResource CardLabel}" Text="Teho" />
                                    <TextBlock Grid.Row="3" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding CpuPower}" />
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Style="{StaticResource CardTitle}" Text="GPU" />
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                    </Grid.ColumnDefinitions>
                                    <Grid.RowDefinitions>
                                        <RowDefinition /><RowDefinition /><RowDefinition /><RowDefinition /><RowDefinition />
                                    </Grid.RowDefinitions>
                                    <TextBlock Grid.Row="0" Style="{StaticResource CardLabel}" Text="Käyttö" />
                                    <TextBlock Grid.Row="0" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding GpuLoad}" />
                                    <TextBlock Grid.Row="1" Style="{StaticResource CardLabel}" Text="Lämpötila" />
                                    <TextBlock Grid.Row="1" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding GpuTemp}" />
                                    <TextBlock Grid.Row="2" Style="{StaticResource CardLabel}" Text="Hotspot" />
                                    <TextBlock Grid.Row="2" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding GpuHotspot}" />
                                    <TextBlock Grid.Row="3" Style="{StaticResource CardLabel}" Text="VRAM" />
                                    <TextBlock Grid.Row="3" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding GpuVram}" />
                                    <TextBlock Grid.Row="4" Style="{StaticResource CardLabel}" Text="Teho" />
                                    <TextBlock Grid.Row="4" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding GpuPower}" />
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Style="{StaticResource CardTitle}" Text="RAM" />
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                    </Grid.ColumnDefinitions>
                                    <Grid.RowDefinitions>
                                        <RowDefinition /><RowDefinition />
                                    </Grid.RowDefinitions>
                                    <TextBlock Grid.Row="0" Style="{StaticResource CardLabel}" Text="Käyttö" />
                                    <TextBlock Grid.Row="0" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding RamLoad}" />
                                    <TextBlock Grid.Row="1" Style="{StaticResource CardLabel}" Text="Käytössä" />
                                    <TextBlock Grid.Row="1" Grid.Column="1" Style="{StaticResource CardValue}" Text="{Binding RamUsed}" />
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Style="{StaticResource CardTitle}" Text="Levyt" />
                                <ItemsControl ItemsSource="{Binding Disks}">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding}" Foreground="#E0E0E0"
                                                       FontFamily="Consolas" Margin="0,2" />
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Style="{StaticResource CardTitle}" Text="Tuulettimet" />
                                <ItemsControl ItemsSource="{Binding Fans}">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding}" Foreground="#E0E0E0"
                                                       FontFamily="Consolas" Margin="0,2" />
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </Border>

                    </WrapPanel>
                </ScrollViewer>
            </TabItem>

            <TabItem Header="Kaikki sensorit">
                <!-- Nykyinen TreeView siirretään tähän SELLAISENAAN -->
            </TabItem>
        </TabControl>
```

- [ ] **Step 4: Käännä ja aja, tarkista silmämääräisesti**

Run: `dotnet build HardwareMonitor.sln` → PASS, sitten käynnistä (`.\run.ps1 -AsAdmin`).
Expected: Dashboard-välilehti auki oletuksena, CPU/GPU/RAM/Levyt/Tuulettimet-kortit
näyttävät arvoja, jotka päivittyvät sekunnin välein; "Kaikki sensorit" -välilehdellä vanha puu.

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.App
git commit -m "Lisää Dashboard-välilehti korteilla (Vaihe 2)"
```

---

### Task 5: OverlayWindow + OverlayViewModel

**Files:**
- Create: `src/HardwareMonitor.App/ViewModels/OverlayViewModel.cs`
- Create: `src/HardwareMonitor.App/OverlayWindow.xaml`
- Create: `src/HardwareMonitor.App/OverlayWindow.xaml.cs`

**Interfaces:**
- Consumes: `KeyMetrics` (Task 1–2), `OverlaySettings`, `OverlayCorner` (Task 3).
- Produces: `OverlayViewModel.Update(KeyMetrics m, OverlaySettings s)`; `OverlayWindow(OverlayViewModel vm)` + `OverlayWindow.ApplySettings(OverlaySettings s)` (asemointi + läpinäkyvyys). Task 6 kutsuu näitä.

- [ ] **Step 1: Toteuta OverlayViewModel**

`src/HardwareMonitor.App/ViewModels/OverlayViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Globalization;
using System.Text;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Overlayn näkymämalli: rakentaa KeyMetrics-arvoista ja asetuksista monirivisen
/// tekstin. Yksi Text-property pitää päivityksen välkkymättömänä ja kevyenä.
/// </summary>
public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private string _text = "";
    private double _fontSize = 14;
    private double _backgroundOpacity = 0.85;

    public string Text
    {
        get => _text;
        private set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public double FontSize
    {
        get => _fontSize;
        private set
        {
            if (Math.Abs(_fontSize - value) < 0.01)
            {
                return;
            }

            _fontSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSize)));
        }
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        private set
        {
            if (Math.Abs(_backgroundOpacity - value) < 0.01)
            {
                return;
            }

            _backgroundOpacity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundOpacity)));
        }
    }

    public void Update(KeyMetrics m, OverlaySettings s)
    {
        FontSize = s.FontSize;
        BackgroundOpacity = s.Opacity;

        var sb = new StringBuilder();

        if (s.ShowCpu)
        {
            sb.AppendLine($"CPU  {Pct(m.CpuLoadPercent)}  {Temp(m.CpuPackageTempC)}  {Num(m.CpuMaxClockMhz, "MHz")}");
        }

        if (s.ShowGpu)
        {
            sb.AppendLine($"GPU  {Pct(m.GpuLoadPercent)}  {Temp(m.GpuTempC)}  hot {Temp(m.GpuHotspotTempC)}");
            if (m.GpuMemoryUsedMb is { } used && m.GpuMemoryTotalMb is { } total)
            {
                sb.AppendLine($"VRAM {used:0}/{total:0} MB");
            }
        }

        if (s.ShowRam)
        {
            string usedGb = m.RamUsedGb is { } gb
                ? gb.ToString("0.0", CultureInfo.CurrentCulture) + " GB"
                : "—";
            sb.AppendLine($"RAM  {Pct(m.RamLoadPercent)}  {usedGb}");
        }

        if (s.ShowDisks)
        {
            foreach (DiskMetrics disk in m.Disks)
            {
                if (disk.TemperatureC is { } t)
                {
                    sb.AppendLine($"{Shorten(disk.Name)}  {t:0} °C");
                }
            }
        }

        if (s.ShowFans)
        {
            foreach (FanMetrics fan in m.Fans)
            {
                if (fan.Rpm is { } rpm and > 0)
                {
                    sb.AppendLine($"{fan.Name}  {rpm:0} RPM");
                }
            }
        }

        Text = sb.ToString().TrimEnd();
    }

    private static string Pct(float? v) => v is { } x ? $"{x:0} %" : "—";

    private static string Temp(float? v) => v is { } x ? $"{x:0} °C" : "—";

    private static string Num(float? v, string unit) => v is { } x ? $"{x:0} {unit}" : "—";

    /// <summary>Lyhentää pitkät levynimet overlayn kompakteille riveille.</summary>
    private static string Shorten(string name) =>
        name.Length <= 20 ? name : name[..20].TrimEnd();

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

- [ ] **Step 2: Toteuta OverlayWindow (XAML + code-behind)**

`src/HardwareMonitor.App/OverlayWindow.xaml`:

```xml
<Window x:Class="HardwareMonitor.App.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="HardwareMonitor Overlay"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ShowActivated="False"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight"
        IsHitTestVisible="False">

    <Border CornerRadius="10" Padding="14,10" Background="#1E1E1E"
            Opacity="{Binding BackgroundOpacity}">
        <TextBlock Text="{Binding Text}"
                   FontFamily="Consolas"
                   FontSize="{Binding FontSize}"
                   Foreground="#E8F5E9"
                   LineHeight="22" />
    </Border>
</Window>
```

`src/HardwareMonitor.App/OverlayWindow.xaml.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using HardwareMonitor.App.ViewModels;
using HardwareMonitor.Core.Settings;
using Microsoft.Win32;

namespace HardwareMonitor.App;

/// <summary>
/// Läpi-klikattava always-on-top-overlay (specin Vaihe 2.5). Ikkuna ei ota
/// fokusta, ei näy Alt+Tabissa eikä tehtäväpalkissa, ja hiiren klikkaukset
/// menevät sen läpi alla olevaan sovellukseen (WS_EX_TRANSPARENT).
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private OverlaySettings _settings = new();

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        SizeChanged += (_, _) => Reposition();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    /// <summary>Vie sijainti- ja ulkoasuasetukset ikkunaan ja asemoi sen uudelleen.</summary>
    public void ApplySettings(OverlaySettings settings)
    {
        _settings = settings;
        Reposition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Läpi-klikattavuus + ei fokusta + ei Alt+Tabia. Asetetaan kerran,
        // kun ikkunakahva on olemassa.
        var handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(handle, GwlExStyle);
        _ = SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(Reposition);

    /// <summary>Asemoi ikkunan valittuun työalueen kulmaan (DIP-yksiköissä).</summary>
    private void Reposition()
    {
        Rect workArea = SystemParameters.WorkArea;
        double margin = _settings.MarginPx;

        Left = _settings.Corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft
            ? workArea.Left + margin
            : workArea.Right - ActualWidth - margin;

        Top = _settings.Corner is OverlayCorner.TopLeft or OverlayCorner.TopRight
            ? workArea.Top + margin
            : workArea.Bottom - ActualHeight - margin;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
```

- [ ] **Step 3: Käännä**

Run: `dotnet build HardwareMonitor.sln`
Expected: PASS, 0 virhettä. (Ikkunaa ei vielä avata mistään — kytkentä Task 6:ssa.)

- [ ] **Step 4: Commit**

```bash
git add src/HardwareMonitor.App
git commit -m "Lisää läpi-klikattava OverlayWindow ja OverlayViewModel"
```

---

### Task 6: Overlay-asetukset UI:hin ja kytkentä

**Files:**
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (SettingsService, Overlay-VM, asetusproperyt)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (asetusrivi yläpalkkiin)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml.cs` (OverlayWindow-elinkaari)
- Modify: `docs/ROADMAP.md` (Vaihe 2 + 2.5 valmiiksi, FPS jatkokehitysideaksi)

**Interfaces:**
- Consumes: `SettingsService.Load/Save`, `AppSettings`, `OverlaySettings`, `OverlayCorner` (Task 3); `OverlayViewModel.Update`, `OverlayWindow.ApplySettings` (Task 5); `KeyMetricsService.Extract` (Task 1–2).
- Produces: `MainViewModel.Overlay` (OverlayViewModel), `MainViewModel.OverlayEnabled/OverlayCornerIndex/OverlayOpacity/OverlayShowCpu/OverlayShowGpu/OverlayShowRam/OverlayShowDisks/OverlayShowFans` (kaksisuuntaiset bindaukset), `event Action? OverlaySettingsChanged`.

- [ ] **Step 1: Laajenna MainViewModel**

Lisäykset `MainViewModel.cs`:ään (usingit: `HardwareMonitor.Core.Metrics`, `HardwareMonitor.Core.Settings`):

```csharp
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;

    public OverlayViewModel Overlay { get; } = new();

    /// <summary>Laukeaa kun mikä tahansa overlay-asetus muuttuu (tallennus + ikkunan päivitys).</summary>
    public event Action? OverlaySettingsChanged;

    public OverlaySettings OverlaySettings => _settings.Overlay;
```

Konstruktoriin ensimmäiseksi riviksi: `_settings = _settingsService.Load();`

Asetusproperty-malli — sama kaava kaikille kahdeksalle (Enabled, CornerIndex,
Opacity, ShowCpu, ShowGpu, ShowRam, ShowDisks, ShowFans); Enabled ja CornerIndex
kokonaisina esimerkkeinä:

```csharp
    public bool OverlayEnabled
    {
        get => _settings.Overlay.Enabled;
        set
        {
            if (_settings.Overlay.Enabled == value)
            {
                return;
            }

            _settings.Overlay.Enabled = value;
            OnOverlaySettingChanged(nameof(OverlayEnabled));
        }
    }

    /// <summary>0=vasen ylä, 1=oikea ylä, 2=vasen ala, 3=oikea ala (ComboBoxin järjestys).</summary>
    public int OverlayCornerIndex
    {
        get => (int)_settings.Overlay.Corner;
        set
        {
            if ((int)_settings.Overlay.Corner == value)
            {
                return;
            }

            _settings.Overlay.Corner = (OverlayCorner)value;
            OnOverlaySettingChanged(nameof(OverlayCornerIndex));
        }
    }

    public double OverlayOpacity
    {
        get => _settings.Overlay.Opacity;
        set
        {
            if (Math.Abs(_settings.Overlay.Opacity - value) < 0.01)
            {
                return;
            }

            _settings.Overlay.Opacity = value;
            OnOverlaySettingChanged(nameof(OverlayOpacity));
        }
    }

    // OverlayShowCpu/Gpu/Ram/Disks/Fans: sama kaava kuin OverlayEnabled,
    // kohteena _settings.Overlay.ShowCpu jne.

    private void OnOverlaySettingChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            _logger.Log($"VIRHE asetusten tallennuksessa: {ex.Message}");
        }

        OverlaySettingsChanged?.Invoke();
    }
```

`Refresh()`-metodiin `Dashboard.Update(metrics);`-rivin perään:

```csharp
            Overlay.Update(metrics, _settings.Overlay);
```

- [ ] **Step 2: Lisää asetusrivi yläpalkkiin (MainWindow.xaml)**

Yläpalkin `StackPanel`-elementin sisään, otsikkotekstien alle:

```xml
                <WrapPanel Margin="0,10,0,0">
                    <CheckBox Content="Overlay työpöydälle" IsChecked="{Binding OverlayEnabled}"
                              Foreground="White" VerticalAlignment="Center" />
                    <ComboBox SelectedIndex="{Binding OverlayCornerIndex}" Margin="16,0,0,0"
                              VerticalAlignment="Center" Width="110">
                        <ComboBoxItem Content="Vasen ylä" />
                        <ComboBoxItem Content="Oikea ylä" />
                        <ComboBoxItem Content="Vasen ala" />
                        <ComboBoxItem Content="Oikea ala" />
                    </ComboBox>
                    <TextBlock Text="Läpinäkyvyys" Foreground="#BDBDBD" Margin="16,0,6,0"
                               VerticalAlignment="Center" />
                    <Slider Value="{Binding OverlayOpacity}" Minimum="0.3" Maximum="1.0"
                            Width="90" VerticalAlignment="Center" />
                    <CheckBox Content="CPU" IsChecked="{Binding OverlayShowCpu}" Foreground="White"
                              Margin="16,0,0,0" VerticalAlignment="Center" />
                    <CheckBox Content="GPU" IsChecked="{Binding OverlayShowGpu}" Foreground="White"
                              Margin="8,0,0,0" VerticalAlignment="Center" />
                    <CheckBox Content="RAM" IsChecked="{Binding OverlayShowRam}" Foreground="White"
                              Margin="8,0,0,0" VerticalAlignment="Center" />
                    <CheckBox Content="Levyt" IsChecked="{Binding OverlayShowDisks}" Foreground="White"
                              Margin="8,0,0,0" VerticalAlignment="Center" />
                    <CheckBox Content="Tuulettimet" IsChecked="{Binding OverlayShowFans}" Foreground="White"
                              Margin="8,0,0,0" VerticalAlignment="Center" />
                </WrapPanel>
```

- [ ] **Step 3: OverlayWindow-elinkaari (MainWindow.xaml.cs)**

Korvaa `MainWindow.xaml.cs` kokonaan:

```csharp
using System.Windows;
using HardwareMonitor.App.ViewModels;

namespace HardwareMonitor.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private OverlayWindow? _overlay;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            _viewModel.Start();
            _viewModel.OverlaySettingsChanged += ApplyOverlaySettings;
            ApplyOverlaySettings();
        };

        Closed += (_, _) =>
        {
            _overlay?.Close();
            _viewModel.Dispose();
        };
    }

    /// <summary>Avaa, sulkee ja asemoi overlayn asetusten mukaan.</summary>
    private void ApplyOverlaySettings()
    {
        if (_viewModel.OverlayEnabled)
        {
            if (_overlay is null)
            {
                _overlay = new OverlayWindow(_viewModel.Overlay) { Owner = this };
                _overlay.Show();
            }

            _overlay.ApplySettings(_viewModel.OverlaySettings);
        }
        else if (_overlay is not null)
        {
            _overlay.Close();
            _overlay = null;
        }
    }
}
```

Huom: `Owner = this` pitää overlayn pääikkunan "mukana" — se sulkeutuu
automaattisesti pääikkunan mukana eikä jää orvoksi.

- [ ] **Step 4: Käännä, aja testit ja tee käsintestaus**

Run: `dotnet test HardwareMonitor.sln` → PASS (11 testiä).
Run: `.\run.ps1 -AsAdmin` ja tarkista:
1. "Overlay työpöydälle" -ruksi avaa overlayn valittuun kulmaan.
2. Klikkaus overlayn kohdalla menee LÄPI alla olevaan sovellukseen.
3. Overlay pysyy näkyvissä kun vaihtaa sovellusta (Alt+Tab) eikä näy Alt+Tab-listassa.
4. Kulman vaihto, läpinäkyvyys-slider ja rivivalinnat vaikuttavat heti.
5. Asetukset säilyvät sovelluksen uudelleenkäynnistyksessä (settings.json).
6. Ruksin poisto sulkee overlayn.

- [ ] **Step 5: Päivitä ROADMAP**

`docs/ROADMAP.md`: merkitse Vaihe 2 valmiiksi (Dashboard + KeyMetricsService + testit),
lisää sen alle "Vaihe 2.5 — Työpöytäoverlay (VALMIS)" (läpi-klikattava topmost-ikkuna,
asetukset settings.jsoniin) ja lisää jatkokehitysideoihin: FPS-mittaus (PresentMon/ETW),
overlay exclusive fullscreen -peleihin, monen näytön tuki, raja-arvojen värikoodaus
overlayhin (Vaihe 4:n yhteydessä).

- [ ] **Step 6: Commit**

```bash
git add src/HardwareMonitor.App docs/ROADMAP.md
git commit -m "Kytke overlay asetuksineen pääikkunaan (Vaihe 2.5)"
```
