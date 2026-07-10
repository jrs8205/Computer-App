# machine-insights.md v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Laajentaa machine-insights.md tiedostoksi, jonka voi antaa mille tahansa tekoälychatille koneen kontekstiksi (spec: docs/superpowers/specs/2026-07-10-machine-insights-v2-design.md).

**Architecture:** `MachineInsightsBuilder.Build` saa jatkossa yhden `MachineInsightsInput`-recordin (Now, Spec, Stats30d, Stats7d, Events, Limits) ja pysyy puhtaana funktiona. Uusi `MachineSpecReader` johtaa koneen kokoonpanon `HardwareGroup`-listasta. MainViewModel kokoaa syötteet (GetSampleStats × 2, viimeisin sensoriluenta, OS-kuvaus, InsightsNotes-asetus).

**Tech Stack:** C# / .NET 8, WPF (MVVM ilman kirjastoja), xUnit, resx-lokalisointi käsin tehdyillä accessoreilla.

## Global Constraints

- Testit: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` — **EI buildaa App-projektia**; App-muutosten jälkeen aja aina `dotnet build HardwareMonitor.sln`.
- HardwareMonitor.exe voi olla ajossa ja lukita DLL:t — käyttäjä sulkee sen trayn kautta (Lopeta) ennen buildia. ÄLÄ käytä Stop-Processia (kirjaisi kaatumistapahtuman).
- Lokalisointi: jokainen uusi avain LISÄTÄÄN MOLEMPIIN resx:iin (`Strings.resx` = neutraali fi, `Strings.en.resx` = en; App-puolella `UiStrings.resx`/`.en.resx`) JA accessor käsin accessor-luokkaan (resx-designer ei toimi dotnet CLI:llä). En-satelliitista puuttuva avain palautuu fi:hin — siksi myös identtiset en-arvot kirjataan.
- Tapahtumien Component- ja Level-arvot ("Laitteisto", "WARNING", ...) ovat kannan luokitteluavaimia — EI lokalisoida.
- Testit ajetaan fi-kulttuurissa (TestCulture / ModuleInitializer) — testien odotetut tekstit ovat suomea.
- WCAG AAA uusille UI-teksteille: noudata olemassa olevia värejä (label #E0E0E0, NoteText-tyyli).
- Testiluokkien nimeäminen suomeksi PascalCase-tyyliin (esim. `PoimiiNimetLaitetyypeittain`), kuten olemassa olevissa testeissä.

---

### Task 1: MachineSpec + MachineSpecReader

**Files:**
- Create: `src/HardwareMonitor.Core/Insights/MachineSpec.cs`
- Create: `src/HardwareMonitor.Core/Insights/MachineSpecReader.cs`
- Test: Create `src/HardwareMonitor.Tests/Insights/MachineSpecReaderTests.cs`

**Interfaces:**
- Consumes: `HardwareGroup(string Name, string HardwareType, IReadOnlyList<SensorReading> Sensors, IReadOnlyList<HardwareGroup> SubHardware)` ja `SensorReading(string HardwareName, string HardwareType, string SensorName, string SensorType, float? Value, string Unit, string Identifier)` (Core/Sensors, olemassa).
- Produces: `MachineSpec(string? CpuName, string? GpuName, string? MotherboardName, int? RamTotalGb, IReadOnlyList<string> DiskNames, string OsDescription, string UserNotes)` ja `MachineSpecReader.Read(IReadOnlyList<HardwareGroup> groups, string osDescription, string userNotes) → MachineSpec`.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
using HardwareMonitor.Core.Insights;
using HardwareMonitor.Core.Sensors;
using Xunit;

namespace HardwareMonitor.Tests.Insights;

public class MachineSpecReaderTests
{
    private static HardwareGroup Group(
        string name, string type, params SensorReading[] sensors) =>
        new(name, type, sensors, Array.Empty<HardwareGroup>());

    private static SensorReading Data(string hw, string sensor, float? value) =>
        new(hw, "Memory", sensor, "Data", value, "GB", $"/ram/{sensor}");

    [Fact]
    public void PoimiiNimetLaitetyypeittain()
    {
        var groups = new[]
        {
            Group("ASUS ROG STRIX Z390-F GAMING", "Motherboard"),
            Group("Intel Core i9-9900K", "Cpu"),
            Group("NVIDIA GeForce RTX 2060", "GpuNvidia"),
            Group("Samsung SSD 860 EVO 1TB", "Storage"),
            Group("Samsung SSD 970 EVO Plus 1TB", "Storage"),
        };

        MachineSpec spec = MachineSpecReader.Read(groups, "Windows 11 (build 26200)", "");

        Assert.Equal("Intel Core i9-9900K", spec.CpuName);
        Assert.Equal("NVIDIA GeForce RTX 2060", spec.GpuName);
        Assert.Equal("ASUS ROG STRIX Z390-F GAMING", spec.MotherboardName);
        Assert.Equal(
            new[] { "Samsung SSD 860 EVO 1TB", "Samsung SSD 970 EVO Plus 1TB" },
            spec.DiskNames);
        Assert.Equal("Windows 11 (build 26200)", spec.OsDescription);
    }

    [Fact]
    public void LaskeeRamKokonaismaaranJaPyoristaa()
    {
        var groups = new[]
        {
            Group("Generic Memory", "Memory",
                Data("Generic Memory", "Memory Used", 31.2f),
                Data("Generic Memory", "Memory Available", 32.7f),
                Data("Generic Memory", "Virtual Memory Used", 40f)),
        };

        MachineSpec spec = MachineSpecReader.Read(groups, "", "");

        Assert.Equal(64, spec.RamTotalGb); // 31.2 + 32.7 = 63.9 → 64; Virtual ohitetaan
    }

    [Fact]
    public void PuuttuvatLaitteet_PalauttaaNullitJaTyhjanLevylistan()
    {
        MachineSpec spec = MachineSpecReader.Read(
            Array.Empty<HardwareGroup>(), "", "");

        Assert.Null(spec.CpuName);
        Assert.Null(spec.GpuName);
        Assert.Null(spec.MotherboardName);
        Assert.Null(spec.RamTotalGb);
        Assert.Empty(spec.DiskNames);
    }

    [Fact]
    public void ValittaaLisatiedotSellaisenaan()
    {
        MachineSpec spec = MachineSpecReader.Read(
            Array.Empty<HardwareGroup>(), "", "AIO-vesijäähdytys");

        Assert.Equal("AIO-vesijäähdytys", spec.UserNotes);
    }
}
```

- [ ] **Step 2: Aja testit ja varmista että ne epäonnistuvat**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineSpecReader"`
Expected: käännösvirhe "MachineSpec"/"MachineSpecReader" puuttuu.

- [ ] **Step 3: Toteuta MachineSpec ja MachineSpecReader**

`src/HardwareMonitor.Core/Insights/MachineSpec.cs`:

```csharp
namespace HardwareMonitor.Core.Insights;

/// <summary>Koneen kokoonpano machine-insights.md:n Kokoonpano-osioon.</summary>
public sealed record MachineSpec(
    string? CpuName,
    string? GpuName,
    string? MotherboardName,
    int? RamTotalGb,
    IReadOnlyList<string> DiskNames,
    string OsDescription,
    string UserNotes);
```

`src/HardwareMonitor.Core/Insights/MachineSpecReader.cs`:

```csharp
using HardwareMonitor.Core.Sensors;

namespace HardwareMonitor.Core.Insights;

/// <summary>
/// Johtaa koneen kokoonpanon sensoridatasta (HardwareGroup-lista).
/// OS-kuvaus ja käyttäjän lisätiedot annetaan parametreina, jotta
/// luokka pysyy puhtaana funktiona.
/// </summary>
public static class MachineSpecReader
{
    public static MachineSpec Read(
        IReadOnlyList<HardwareGroup> groups, string osDescription, string userNotes)
    {
        string? cpu = null, gpu = null, motherboard = null;
        int? ramGb = null;
        var disks = new List<string>();

        foreach (HardwareGroup group in groups)
        {
            switch (group.HardwareType)
            {
                case "Cpu": cpu ??= group.Name; break;
                case "Motherboard": motherboard ??= group.Name; break;
                case "Storage": disks.Add(group.Name); break;
                case "Memory": ramGb ??= ReadRamTotalGb(group); break;
                default:
                    if (group.HardwareType.StartsWith("Gpu", StringComparison.Ordinal))
                    {
                        gpu ??= group.Name;
                    }

                    break;
            }
        }

        return new MachineSpec(cpu, gpu, motherboard, ramGb, disks, osDescription, userNotes);
    }

    /// <summary>
    /// Memory Used + Memory Available (GB) pyöristettynä kokonaisiin gigatavuihin.
    /// Tarkat sensorinimet, jotta Virtual Memory -sensorit eivät summaudu mukaan.
    /// </summary>
    private static int? ReadRamTotalGb(HardwareGroup memory)
    {
        float? used = null, available = null;
        foreach (SensorReading s in memory.Sensors)
        {
            if (s.SensorName == "Memory Used")
            {
                used = s.Value;
            }
            else if (s.SensorName == "Memory Available")
            {
                available = s.Value;
            }
        }

        return used is { } u && available is { } a ? (int)MathF.Round(u + a) : null;
    }
}
```

- [ ] **Step 4: Aja testit ja varmista että ne menevät läpi**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineSpecReader"`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```powershell
git add src/HardwareMonitor.Core/Insights/MachineSpec.cs src/HardwareMonitor.Core/Insights/MachineSpecReader.cs src/HardwareMonitor.Tests/Insights/MachineSpecReaderTests.cs
git commit -m "Lisää MachineSpec ja MachineSpecReader kokoonpanon tunnistukseen"
```

---

### Task 2: MachineInsightsInput ja Build-signatuurin refaktorointi

**Files:**
- Create: `src/HardwareMonitor.Core/Insights/MachineInsightsInput.cs`
- Modify: `src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs` (Build-signatuuri, rivit 20–44)
- Modify: `src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs` (Build- ja Spec-helperit)
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs:649-654` (kutsupaikka)

**Interfaces:**
- Consumes: `MachineSpec` (Task 1), `SampleStats`, `EventRow`, `ThresholdSettings` (olemassa).
- Produces: `MachineInsightsInput(DateTimeOffset Now, MachineSpec Spec, SampleStats Stats30d, SampleStats Stats7d, IReadOnlyList<EventRow> Events, ThresholdSettings Limits)`; `MachineInsightsBuilder.Build(MachineInsightsInput input) → string`. Tasks 3–6 lisäävät osioita tämän sisään; Task 8 rakentaa inputin oikeasta datasta.

- [ ] **Step 1: Luo MachineInsightsInput**

`src/HardwareMonitor.Core/Insights/MachineInsightsInput.cs`:

```csharp
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Insights;

/// <summary>
/// MachineInsightsBuilderin syötteet yhtenä recordina, jotta signatuuri
/// ei kasva sisällön laajentuessa. Stats7d on trendivertailua varten.
/// </summary>
public sealed record MachineInsightsInput(
    DateTimeOffset Now,
    MachineSpec Spec,
    SampleStats Stats30d,
    SampleStats Stats7d,
    IReadOnlyList<EventRow> Events,
    ThresholdSettings Limits);
```

- [ ] **Step 2: Muuta Build-signatuuri**

`MachineInsightsBuilder.cs`: korvaa metodin alku (rivit 20–29)

```csharp
    public static string Build(
        DateTimeOffset now,
        SampleStats stats,
        IReadOnlyList<EventRow> events,
        ThresholdSettings limits)
    {
```

muotoon

```csharp
    public static string Build(MachineInsightsInput input)
    {
        (DateTimeOffset now, MachineSpec _, SampleStats stats, SampleStats _,
            IReadOnlyList<EventRow> events, ThresholdSettings limits) = input;
```

(Recordin positionaalinen dekonstruktio; Spec ja Stats7d otetaan käyttöön Taskeissa 4–5.)

- [ ] **Step 3: Päivitä testien helperit**

`MachineInsightsBuilderTests.cs`: lisää `Spec`-helper ja korvaa `Build`-helper:

```csharp
    private static MachineSpec Spec(string notes = "") => new(
        "Intel Core i9-9900K", "NVIDIA GeForce RTX 2060",
        "ASUS ROG STRIX Z390-F GAMING", 64,
        new[] { "970 EVO Plus" }, "Windows 11 (build 26200)", notes);

    private static string Build(
        SampleStats? stats = null,
        IReadOnlyList<EventRow>? events = null,
        SampleStats? stats7d = null,
        MachineSpec? spec = null) =>
        MachineInsightsBuilder.Build(new MachineInsightsInput(
            Now,
            spec ?? Spec(),
            stats ?? Stats(),
            stats7d ?? stats ?? Stats(),
            events ?? Array.Empty<EventRow>(),
            Limits));
```

- [ ] **Step 4: Päivitä MainViewModelin kutsupaikka (väliaikainen tyhjä spec)**

`MainViewModel.cs:649-654`: korvaa

```csharp
                string markdown = MachineInsightsBuilder.Build(
                    now,
                    db.GetSampleStats(now.AddDays(-30)),
                    db.ReadEventsSince(now.AddDays(-30)),
                    _settings.Thresholds);
```

muotoon

```csharp
                string markdown = MachineInsightsBuilder.Build(new MachineInsightsInput(
                    now,
                    MachineSpecReader.Read(Array.Empty<HardwareGroup>(), "", ""),
                    db.GetSampleStats(now.AddDays(-30)),
                    db.GetSampleStats(now.AddDays(-7)),
                    db.ReadEventsSince(now.AddDays(-30)),
                    _settings.Thresholds));
```

(Task 8 korvaa tyhjät parametrit oikealla datalla. `HardwareMonitor.Core.Insights` on jo usingeissa; `HardwareGroup` vaatii `HardwareMonitor.Core.Sensors` — tarkista usingit tiedoston alusta.)

- [ ] **Step 5: Aja kaikki testit ja buildaa koko sln**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → Expected: kaikki (134+) passed.
Run: `dotnet build HardwareMonitor.sln` → Expected: Build succeeded.

- [ ] **Step 6: Commit**

```powershell
git add src/HardwareMonitor.Core/Insights/ src/HardwareMonitor.Tests/Insights/ src/HardwareMonitor.App/ViewModels/MainViewModel.cs
git commit -m "Refaktoroi MachineInsightsBuilder käyttämään MachineInsightsInput-recordia"
```

---

### Task 3: Johdanto tekoälylle -osio

**Files:**
- Modify: `src/HardwareMonitor.Core/Localization/Strings.resx` (Insights-lohko, n. rivi 150)
- Modify: `src/HardwareMonitor.Core/Localization/Strings.en.resx` (sama kohta)
- Modify: `src/HardwareMonitor.Core/Localization/Strings.cs` (n. rivi 143 jälkeen)
- Modify: `src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs`
- Test: Modify `src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs`

**Interfaces:**
- Consumes: `Build(MachineInsightsInput)` (Task 2).
- Produces: tiedostoon osio `## Johdanto tekoälylle` heti päivitysaikakappaleen jälkeen, myös kun dataa ei ole.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
    [Fact]
    public void SisaltaaJohdannonTekoalylle()
    {
        string md = Build();

        Assert.Contains("## Johdanto tekoälylle", md);
        Assert.Contains("LibreHardwareMonitor", md);
        Assert.Contains("vianetsinnästä", md);
    }

    [Fact]
    public void JohdantoNakyyMyosIlmanDataa()
    {
        string md = Build(stats: Stats(count: 0));

        Assert.Contains("## Johdanto tekoälylle", md);
    }
```

- [ ] **Step 2: Aja testit, varmista FAIL**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: 2 uutta testiä FAIL ("## Johdanto tekoälylle" puuttuu).

- [ ] **Step 3: Lisää resx-avaimet ja accessorit**

`Strings.resx` (Insights_Intro-rivin jälkeen):

```xml
  <data name="Insights_AiIntroHeading" xml:space="preserve"><value>## Johdanto tekoälylle</value></data>
  <data name="Insights_AiIntroBody" xml:space="preserve"><value>Tämä tiedosto on Hardware Monitor -sovelluksen generoima yhteenveto tämän
tietokoneen kunnosta. Data tulee LibreHardwareMonitor-kirjastosta: lukemat
luetaan sekunnin välein, koostetaan tiiviiksi historiariveiksi ja
säilytetään 30 päivää. Taulukoissa keskiarvo kuvaa tyypillistä tasoa,
huippu on korkein hetkellinen lukema ja varoitusraja on sovelluksen
hälytysraja — hetkellinen huippu lähellä rajaa ei vielä tarkoita ongelmaa,
jos keskiarvo on matala. Käytä tätä tiedostoa kontekstina, kun käyttäjä
kysyy koneensa lämpötiloista, suorituskyvystä, jäähdytyksestä tai
vianetsinnästä.</value></data>
```

`Strings.en.resx` (sama kohta):

```xml
  <data name="Insights_AiIntroHeading" xml:space="preserve"><value>## Introduction for AI assistants</value></data>
  <data name="Insights_AiIntroBody" xml:space="preserve"><value>This file is a summary of this computer's health, generated by the
Hardware Monitor application. The data comes from the LibreHardwareMonitor
library: readings are taken every second, aggregated into compact history
rows and kept for 30 days. In the tables, the average describes the
typical level, the peak is the highest momentary reading and the warning
limit is the application's alert threshold — a momentary peak near the
limit does not necessarily indicate a problem if the average is low.
Use this file as context when the user asks about this machine's
temperatures, performance, cooling or troubleshooting.</value></data>
```

`Strings.cs` (Insights_Intro-accessorin jälkeen, rivi 143):

```csharp
    public static string Insights_AiIntroHeading => T(nameof(Insights_AiIntroHeading));
    public static string Insights_AiIntroBody => T(nameof(Insights_AiIntroBody));
```

- [ ] **Step 4: Lisää osio builderiin**

`MachineInsightsBuilder.cs`, Build-metodissa: lisää `AppendAiIntro(sb);` heti intro-kappaleen jälkeen, ENNEN `if (stats.SampleCount == 0)` -riviä. Uusi metodi:

```csharp
    private static void AppendAiIntro(StringBuilder sb)
    {
        sb.AppendLine(Strings.Insights_AiIntroHeading);
        sb.AppendLine();
        sb.AppendLine(Strings.Insights_AiIntroBody.ReplaceLineEndings());
        sb.AppendLine();
    }
```

- [ ] **Step 5: Aja testit, varmista PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: kaikki passed.

- [ ] **Step 6: Commit**

```powershell
git add src/HardwareMonitor.Core/Localization/ src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs
git commit -m "Lisää konetuntemus-lokiin johdanto tekoälylle"
```

---

### Task 4: Kokoonpano-osio

**Files:**
- Modify: `src/HardwareMonitor.Core/Localization/Strings.resx`, `Strings.en.resx`, `Strings.cs`
- Modify: `src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs`
- Test: Modify `src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs`

**Interfaces:**
- Consumes: `input.Spec` (`MachineSpec`, Task 1–2).
- Produces: osio `## Koneen kokoonpano` johdannon jälkeen, myös kun dataa ei ole.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
    [Fact]
    public void KokoonpanoListaaKomponentit()
    {
        string md = Build();

        Assert.Contains("## Koneen kokoonpano", md);
        Assert.Contains("i9-9900K", md);
        Assert.Contains("RTX 2060", md);
        Assert.Contains("Z390-F", md);
        Assert.Contains("64 GB", md);
        Assert.Contains("Windows 11", md);
    }

    [Fact]
    public void SamannimisetLevytRyhmitellaan()
    {
        MachineSpec spec = Spec() with
        {
            DiskNames = new[] { "860 EVO", "860 EVO", "970 EVO Plus" },
        };

        string md = Build(spec: spec);

        Assert.Contains("2 × 860 EVO", md);
        Assert.Contains("970 EVO Plus", md);
    }

    [Fact]
    public void LisatiedotMukanaVainKunAsetettu()
    {
        Assert.DoesNotContain("lisätiedot", Build(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AIO-vesijäähdytys", Build(spec: Spec(notes: "AIO-vesijäähdytys")));
    }

    [Fact]
    public void PuuttuvaKokoonpanotietoNaytetaanViivana()
    {
        var spec = new MachineSpec(null, null, null, null, Array.Empty<string>(), "", "");

        string md = Build(spec: spec);

        Assert.Contains("- Suoritin: —", md);
        Assert.Contains("- Levyt: —", md);
    }
```

- [ ] **Step 2: Aja testit, varmista FAIL**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: 4 uutta testiä FAIL.

- [ ] **Step 3: Lisää resx-avaimet ja accessorit**

`Strings.resx`:

```xml
  <data name="Insights_SpecHeading" xml:space="preserve"><value>## Koneen kokoonpano</value></data>
  <data name="Insights_SpecCpu" xml:space="preserve"><value>- Suoritin: {0}</value></data>
  <data name="Insights_SpecGpu" xml:space="preserve"><value>- Näytönohjain: {0}</value></data>
  <data name="Insights_SpecMotherboard" xml:space="preserve"><value>- Emolevy: {0}</value></data>
  <data name="Insights_SpecRam" xml:space="preserve"><value>- RAM: {0}</value></data>
  <data name="Insights_SpecDisks" xml:space="preserve"><value>- Levyt: {0}</value></data>
  <data name="Insights_SpecOs" xml:space="preserve"><value>- Käyttöjärjestelmä: {0}</value></data>
  <data name="Insights_SpecNotes" xml:space="preserve"><value>- Käyttäjän lisätiedot: {0}</value></data>
```

`Strings.en.resx`:

```xml
  <data name="Insights_SpecHeading" xml:space="preserve"><value>## Machine configuration</value></data>
  <data name="Insights_SpecCpu" xml:space="preserve"><value>- CPU: {0}</value></data>
  <data name="Insights_SpecGpu" xml:space="preserve"><value>- GPU: {0}</value></data>
  <data name="Insights_SpecMotherboard" xml:space="preserve"><value>- Motherboard: {0}</value></data>
  <data name="Insights_SpecRam" xml:space="preserve"><value>- RAM: {0}</value></data>
  <data name="Insights_SpecDisks" xml:space="preserve"><value>- Drives: {0}</value></data>
  <data name="Insights_SpecOs" xml:space="preserve"><value>- Operating system: {0}</value></data>
  <data name="Insights_SpecNotes" xml:space="preserve"><value>- User notes: {0}</value></data>
```

`Strings.cs`:

```csharp
    public static string Insights_SpecHeading => T(nameof(Insights_SpecHeading));
    public static string Insights_SpecCpu => T(nameof(Insights_SpecCpu));
    public static string Insights_SpecGpu => T(nameof(Insights_SpecGpu));
    public static string Insights_SpecMotherboard => T(nameof(Insights_SpecMotherboard));
    public static string Insights_SpecRam => T(nameof(Insights_SpecRam));
    public static string Insights_SpecDisks => T(nameof(Insights_SpecDisks));
    public static string Insights_SpecOs => T(nameof(Insights_SpecOs));
    public static string Insights_SpecNotes => T(nameof(Insights_SpecNotes));
```

- [ ] **Step 4: Lisää osio builderiin**

Build-metodissa: muuta dekonstruktion `MachineSpec _` → `MachineSpec spec` ja lisää `AppendSpec(sb, spec);` heti `AppendAiIntro(sb);`-rivin jälkeen (edelleen ennen SampleCount-tarkistusta). Uudet metodit:

```csharp
    private static void AppendSpec(StringBuilder sb, MachineSpec spec)
    {
        sb.AppendLine(Strings.Insights_SpecHeading);
        sb.AppendLine();
        sb.AppendLine(string.Format(Strings.Insights_SpecCpu, spec.CpuName ?? "—"));
        sb.AppendLine(string.Format(Strings.Insights_SpecGpu, spec.GpuName ?? "—"));
        sb.AppendLine(string.Format(
            Strings.Insights_SpecMotherboard, spec.MotherboardName ?? "—"));
        sb.AppendLine(string.Format(
            Strings.Insights_SpecRam, spec.RamTotalGb is { } gb ? $"{gb} GB" : "—"));
        sb.AppendLine(string.Format(Strings.Insights_SpecDisks, FormatDisks(spec.DiskNames)));
        sb.AppendLine(string.Format(
            Strings.Insights_SpecOs,
            string.IsNullOrWhiteSpace(spec.OsDescription) ? "—" : spec.OsDescription));
        if (!string.IsNullOrWhiteSpace(spec.UserNotes))
        {
            sb.AppendLine(string.Format(Strings.Insights_SpecNotes, spec.UserNotes.Trim()));
        }

        sb.AppendLine();
    }

    /// <summary>Samannimiset levyt ryhmitellään: "2 × Samsung SSD 860 EVO 1TB".</summary>
    private static string FormatDisks(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return "—";
        }

        return string.Join("; ", names
            .GroupBy(n => n)
            .Select(g => g.Count() > 1 ? $"{g.Count()} × {g.Key}" : g.Key));
    }
```

- [ ] **Step 5: Aja testit, varmista PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: kaikki passed.

- [ ] **Step 6: Commit**

```powershell
git add src/HardwareMonitor.Core/Localization/ src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs
git commit -m "Lisää konetuntemus-lokiin koneen kokoonpano -osio"
```

---

### Task 5: Trendit-osio (7 pv vs 30 pv)

**Files:**
- Modify: `src/HardwareMonitor.Core/Localization/Strings.resx`, `Strings.en.resx`, `Strings.cs`
- Modify: `src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs`
- Test: Modify `src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs`

**Interfaces:**
- Consumes: `input.Stats7d` ja `input.Stats30d` (`SampleStats`; `MetricStat(double? Avg, double? Max)`, `DiskStat(string Name, double? TempAvg, double? TempMax)`).
- Produces: osio `## Trendit (7 pv vs 30 pv)` taulukoiden jälkeen, ennen Tapahtumat-osiota. Kynnykset: lämpötilat 3 °C, prosentit 10 %-yks.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
    [Fact]
    public void TrendiNaytetaan_KunLampoNoussutSelvasti()
    {
        SampleStats s7 = Stats() with { CpuTemp = new MetricStat(56, 82) };

        string md = Build(stats7d: s7);

        Assert.Contains("## Trendit (7 pv vs 30 pv)", md);
        Assert.Contains("CPU-lämpötila: keskiarvo noussut (52 °C → 56 °C)", md);
    }

    [Fact]
    public void PieniMuutosEiNayTrendeissa()
    {
        SampleStats s7 = Stats() with { CpuTemp = new MetricStat(54, 82) };

        string md = Build(stats7d: s7);

        Assert.Contains("Ei merkittäviä muutoksia", md);
        Assert.DoesNotContain("noussut", md);
    }

    [Fact]
    public void LaskenutKeskiarvoNaytetaanLaskuna()
    {
        SampleStats s7 = Stats() with { RamLoad = new MetricStat(20, 71) };

        string md = Build(stats7d: s7);

        Assert.Contains("RAM-käyttö: keskiarvo laskenut (35 % → 20 %)", md);
    }

    [Fact]
    public void Ilman7pvDataaTodetaanPuute()
    {
        string md = Build(stats7d: Stats(count: 0));

        Assert.Contains("7 päivän vertailudataa", md);
    }

    [Fact]
    public void LevytrendiVerrataanNimenMukaan()
    {
        SampleStats s7 = Stats() with
        {
            Disks = new[] { new DiskStat("970 EVO Plus", 60, 62) },
        };

        string md = Build(stats7d: s7);

        Assert.Contains("970 EVO Plus: keskiarvo noussut (55 °C → 60 °C)", md);
    }
```

(HUOM: `Stats()`-helperin oletukset: CpuTemp avg 52, RamLoad avg 35, levy "970 EVO Plus" avg 55 — testiarvot ylittävät/alittavat kynnykset näihin verrattuna.)

- [ ] **Step 2: Aja testit, varmista FAIL**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: 5 uutta testiä FAIL.

- [ ] **Step 3: Lisää resx-avaimet ja accessorit**

`Strings.resx`:

```xml
  <data name="Insights_TrendsHeading" xml:space="preserve"><value>## Trendit (7 pv vs 30 pv)</value></data>
  <data name="Insights_TrendRise" xml:space="preserve"><value>- {0}: keskiarvo noussut ({1} → {2})</value></data>
  <data name="Insights_TrendFall" xml:space="preserve"><value>- {0}: keskiarvo laskenut ({1} → {2})</value></data>
  <data name="Insights_TrendsNone" xml:space="preserve"><value>- Ei merkittäviä muutoksia.</value></data>
  <data name="Insights_TrendsNotEnough" xml:space="preserve"><value>- 7 päivän vertailudataa ei ole vielä riittävästi.</value></data>
```

`Strings.en.resx`:

```xml
  <data name="Insights_TrendsHeading" xml:space="preserve"><value>## Trends (7 days vs 30 days)</value></data>
  <data name="Insights_TrendRise" xml:space="preserve"><value>- {0}: average has risen ({1} → {2})</value></data>
  <data name="Insights_TrendFall" xml:space="preserve"><value>- {0}: average has fallen ({1} → {2})</value></data>
  <data name="Insights_TrendsNone" xml:space="preserve"><value>- No significant changes.</value></data>
  <data name="Insights_TrendsNotEnough" xml:space="preserve"><value>- Not enough data from the last 7 days for comparison yet.</value></data>
```

`Strings.cs`:

```csharp
    public static string Insights_TrendsHeading => T(nameof(Insights_TrendsHeading));
    public static string Insights_TrendRise => T(nameof(Insights_TrendRise));
    public static string Insights_TrendFall => T(nameof(Insights_TrendFall));
    public static string Insights_TrendsNone => T(nameof(Insights_TrendsNone));
    public static string Insights_TrendsNotEnough => T(nameof(Insights_TrendsNotEnough));
```

- [ ] **Step 4: Lisää osio builderiin**

Build-metodissa: muuta dekonstruktion `SampleStats _` → `SampleStats stats7d` ja lisää `AppendTrends(sb, stats, stats7d);` heti `AppendLevels(...)`-kutsun jälkeen. Uudet jäsenet:

```csharp
    private const double TempTrendThreshold = 3;
    private const double PercentTrendThreshold = 10;

    private static void AppendTrends(StringBuilder sb, SampleStats stats30, SampleStats stats7)
    {
        sb.AppendLine(Strings.Insights_TrendsHeading);
        sb.AppendLine();

        if (stats7.SampleCount == 0)
        {
            sb.AppendLine(Strings.Insights_TrendsNotEnough);
            sb.AppendLine();
            return;
        }

        var lines = new List<string>();
        AddTrend(lines, Strings.Common_CpuTemp,
            stats7.CpuTemp.Avg, stats30.CpuTemp.Avg, "°C", TempTrendThreshold);
        AddTrend(lines, Strings.Insights_CpuLoad,
            stats7.CpuLoad.Avg, stats30.CpuLoad.Avg, "%", PercentTrendThreshold);
        AddTrend(lines, Strings.Common_GpuTemp,
            stats7.GpuTemp.Avg, stats30.GpuTemp.Avg, "°C", TempTrendThreshold);
        AddTrend(lines, Strings.Common_GpuHotspot,
            stats7.GpuHotspot.Avg, stats30.GpuHotspot.Avg, "°C", TempTrendThreshold);
        AddTrend(lines, Strings.Common_RamLoad,
            stats7.RamLoad.Avg, stats30.RamLoad.Avg, "%", PercentTrendThreshold);
        foreach (DiskStat disk7 in stats7.Disks)
        {
            DiskStat? disk30 = stats30.Disks.FirstOrDefault(d => d.Name == disk7.Name);
            if (disk30 is not null)
            {
                AddTrend(lines, disk7.Name,
                    disk7.TempAvg, disk30.TempAvg, "°C", TempTrendThreshold);
            }
        }

        if (lines.Count == 0)
        {
            sb.AppendLine(Strings.Insights_TrendsNone);
        }
        else
        {
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
        }

        sb.AppendLine();
    }

    private static void AddTrend(
        List<string> lines, string label,
        double? avg7, double? avg30, string unit, double threshold)
    {
        if (avg7 is not { } a7 || avg30 is not { } a30 || Math.Abs(a7 - a30) < threshold)
        {
            return;
        }

        string format = a7 > a30 ? Strings.Insights_TrendRise : Strings.Insights_TrendFall;
        lines.Add(string.Format(format, label, Fmt(a30, unit), Fmt(a7, unit)));
    }
```

- [ ] **Step 5: Aja testit, varmista PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: kaikki passed. HUOM: aiemmat testit käyttävät oletuksena stats7d == stats30d → "Ei merkittäviä muutoksia" ei riko niitä.

- [ ] **Step 6: Commit**

```powershell
git add src/HardwareMonitor.Core/Localization/ src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs
git commit -m "Lisää konetuntemus-lokiin trendivertailu 7 pv vs 30 pv"
```

---

### Task 6: Viimeisimmät tapahtumat -lista

**Files:**
- Modify: `src/HardwareMonitor.Core/Localization/Strings.resx`, `Strings.en.resx`, `Strings.cs`
- Modify: `src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs`
- Test: Modify `src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs`

**Interfaces:**
- Consumes: `input.Events` (`EventRow(DateTimeOffset Timestamp, string Level, string Component, string? Sensor, double? Value, double? Threshold, string Message)`).
- Produces: alaotsikko `### Viimeisimmät varoitus- ja kriittiset tapahtumat` Tapahtumat-laskureiden perään; enintään 10 riviä, uusin ensin, INFO ohitetaan; aikaleima paikallisajassa.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
    [Fact]
    public void ViimeisimmatTapahtumatListataanUusinEnsin()
    {
        var events = new EventRow[]
        {
            new(Now.AddDays(-2), "WARNING", "CPU", "CPU Package", 88, 85, "vanhempi"),
            new(Now.AddDays(-1), "CRITICAL", "GPU", "GPU Core", 97, 95, "uudempi"),
            new(Now.AddDays(-3), "INFO", "App", null, null, null, "info-ohitetaan"),
        };

        string md = Build(events: events);

        Assert.Contains("### Viimeisimmät varoitus- ja kriittiset tapahtumat", md);
        Assert.Contains("CRITICAL: uudempi", md);
        Assert.Contains("WARNING: vanhempi", md);
        Assert.DoesNotContain("info-ohitetaan", md);
        Assert.True(md.IndexOf("uudempi") < md.IndexOf("vanhempi"));
    }

    [Fact]
    public void TapahtumalistassaEnintaanKymmenenRivia()
    {
        EventRow[] events = Enumerable.Range(0, 15)
            .Select(i => new EventRow(Now.AddHours(-i), "WARNING", "CPU", "CPU Package",
                88, 85, $"tapahtuma-{i}"))
            .ToArray();

        string md = Build(events: events);

        Assert.Contains("tapahtuma-0", md);
        Assert.Contains("tapahtuma-9", md);
        Assert.DoesNotContain("tapahtuma-10", md);
        Assert.DoesNotContain("tapahtuma-14", md);
    }

    [Fact]
    public void IlmanTapahtumiaListaaEiNayteta()
    {
        Assert.DoesNotContain("Viimeisimmät varoitus", Build());
    }
```

- [ ] **Step 2: Aja testit, varmista FAIL**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: 2 uutta FAIL (kolmas menee läpi jo nyt — se varmistaa regressiot jatkossa).

- [ ] **Step 3: Lisää resx-avaimet ja accessorit**

`Strings.resx`:

```xml
  <data name="Insights_RecentEventsHeading" xml:space="preserve"><value>### Viimeisimmät varoitus- ja kriittiset tapahtumat</value></data>
  <data name="Insights_RecentEventLine" xml:space="preserve"><value>- {0:d.M.yyyy 'klo' HH.mm} — {1}: {2}</value></data>
```

`Strings.en.resx`:

```xml
  <data name="Insights_RecentEventsHeading" xml:space="preserve"><value>### Latest warning and critical events</value></data>
  <data name="Insights_RecentEventLine" xml:space="preserve"><value>- {0:d.M.yyyy 'at' HH.mm} — {1}: {2}</value></data>
```

`Strings.cs`:

```csharp
    public static string Insights_RecentEventsHeading => T(nameof(Insights_RecentEventsHeading));
    public static string Insights_RecentEventLine => T(nameof(Insights_RecentEventLine));
```

- [ ] **Step 4: Lisää lista builderiin**

`AppendEvents`-metodin loppuun (viimeisen `sb.AppendLine();`-rivin edelle EI kosketa — lisää kutsu Build-metodiin `AppendEvents(...)`-kutsun JÄLKEEN): `AppendRecentEvents(sb, events);`. Uusi metodi:

```csharp
    private static void AppendRecentEvents(StringBuilder sb, IReadOnlyList<EventRow> events)
    {
        var recent = events
            .Where(e => e.Level != "INFO")
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToList();
        if (recent.Count == 0)
        {
            return;
        }

        sb.AppendLine(Strings.Insights_RecentEventsHeading);
        sb.AppendLine();
        foreach (EventRow e in recent)
        {
            // Aikaleima paikallisajassa — kannassa aika on UTC-offsetilla.
            sb.AppendLine(string.Format(
                Strings.Insights_RecentEventLine, e.Timestamp.ToLocalTime(), e.Level, e.Message));
        }

        sb.AppendLine();
    }
```

- [ ] **Step 5: Aja testit, varmista PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~MachineInsightsBuilder"`
Expected: kaikki passed.

- [ ] **Step 6: Commit**

```powershell
git add src/HardwareMonitor.Core/Localization/ src/HardwareMonitor.Core/Insights/MachineInsightsBuilder.cs src/HardwareMonitor.Tests/Insights/MachineInsightsBuilderTests.cs
git commit -m "Lisää konetuntemus-lokiin viimeisimmät WARNING/CRITICAL-tapahtumat"
```

---

### Task 7: InsightsNotes-asetus + asetussivun kenttä

**Files:**
- Modify: `src/HardwareMonitor.Core/Settings/AppSettings.cs` (AppSettings-luokka, n. rivi 88)
- Modify: `src/HardwareMonitor.App/ViewModels/SettingsViewModel.cs`
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (Yleiset-GroupBox, kielirivin StackPanelin jälkeen, n. rivi 427)
- Modify: `src/HardwareMonitor.App/Localization/UiStrings.cs`, `UiStrings.resx`, `UiStrings.en.resx`
- Test: Modify `src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs`

**Interfaces:**
- Consumes: `AppSettings`, `SettingsService.Load()/Save()` (olemassa); `SettingsViewModel(AppSettings settings, Action save)`.
- Produces: `AppSettings.InsightsNotes` (string, oletus ""); `SettingsViewModel.InsightsNotes` (string-property joka tallentaa muutokset). Task 8 lukee `_settings.InsightsNotes`.

- [ ] **Step 1: Kirjoita epäonnistuva testi**

`SettingsServiceTests.cs`:

```csharp
    [Fact]
    public void InsightsNotes_OletusTyhjaJaTallennusSailyy()
    {
        var service = new SettingsService(_dir);
        AppSettings defaults = service.Load();
        Assert.Equal("", defaults.InsightsNotes);

        defaults.InsightsNotes = "AIO-vesijäähdytys, näyttö 3440x1440";
        service.Save(defaults);

        Assert.Equal("AIO-vesijäähdytys, näyttö 3440x1440",
            new SettingsService(_dir).Load().InsightsNotes);
    }
```

- [ ] **Step 2: Aja testi, varmista FAIL (käännösvirhe: InsightsNotes puuttuu)**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~SettingsService"`

- [ ] **Step 3: Lisää asetus**

`AppSettings.cs`, AppSettings-luokkaan `AlertNotificationsEnabled`-propertyn jälkeen:

```csharp
    /// <summary>Käyttäjän omat lisätiedot koneesta machine-insights.md:n kokoonpanoon.</summary>
    public string InsightsNotes { get; set; } = "";
```

- [ ] **Step 4: Aja testi, varmista PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter "FullyQualifiedName~SettingsService"`
Expected: kaikki passed.

- [ ] **Step 5: SettingsViewModel-property**

`SettingsViewModel.cs`, `LanguageIndex`-propertyn jälkeen:

```csharp
    /// <summary>Käyttäjän lisätiedot koneesta — liitetään machine-insights.md:n kokoonpanoon.</summary>
    public string InsightsNotes
    {
        get => _settings.InsightsNotes;
        set
        {
            if (_settings.InsightsNotes == value)
            {
                return;
            }

            _settings.InsightsNotes = value;
            _save();
        }
    }
```

- [ ] **Step 6: UiStrings-avaimet**

`UiStrings.resx` (Set_RestartNote-rivin jälkeen):

```xml
  <data name="Set_InsightsNotes" xml:space="preserve"><value>Omat lisätiedot koneesta</value></data>
  <data name="Set_InsightsNotesTip" xml:space="preserve"><value>liitetään konetuntemus-lokiin (machine-insights.md), jonka voi antaa tekoälychatille</value></data>
```

`UiStrings.en.resx`:

```xml
  <data name="Set_InsightsNotes" xml:space="preserve"><value>Your notes about this machine</value></data>
  <data name="Set_InsightsNotesTip" xml:space="preserve"><value>included in the machine insights log (machine-insights.md) that you can give to an AI chat</value></data>
```

`UiStrings.cs` (Set_RestartNote-accessorin jälkeen):

```csharp
    public static string Set_InsightsNotes => T(nameof(Set_InsightsNotes));
    public static string Set_InsightsNotesTip => T(nameof(Set_InsightsNotesTip));
```

- [ ] **Step 7: XAML-kenttä**

`MainWindow.xaml`, Yleiset-GroupBoxissa kielirivin `</StackPanel>`-sulkutagin jälkeen (ennen GroupBoxin sisä-StackPanelin loppua, n. rivi 427):

```xml
<StackPanel Margin="0,8,0,2">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="{x:Static loc:UiStrings.Set_InsightsNotes}" Foreground="#E0E0E0"
                   VerticalAlignment="Center" />
        <TextBlock Style="{StaticResource NoteText}"
                   Text="{x:Static loc:UiStrings.Set_InsightsNotesTip}"
                   VerticalAlignment="Center" Margin="8,0,0,0" />
    </StackPanel>
    <TextBox Text="{Binding SettingsPage.InsightsNotes, UpdateSourceTrigger=LostFocus}"
             AcceptsReturn="True" TextWrapping="Wrap" MinHeight="48" Width="520"
             HorizontalAlignment="Left" Margin="0,4,0,0" />
</StackPanel>
```

HUOM: jos raja-arvokenttien DataTemplaten TextBoxissa on inline-väriattribuutit (Background/Foreground), kopioi samat attribuutit tähän TextBoxiin, jotta kenttä näyttää samalta (tarkista samasta tiedostosta n. rivit 380–400).

- [ ] **Step 8: Buildaa koko sln**

Run: `dotnet build HardwareMonitor.sln`
Expected: Build succeeded (XAML + App kääntyvät).

- [ ] **Step 9: Commit**

```powershell
git add src/HardwareMonitor.Core/Settings/AppSettings.cs src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs src/HardwareMonitor.App/
git commit -m "Lisää InsightsNotes-asetus ja kenttä asetussivulle"
```

---

### Task 8: MainViewModel-kytkentä oikeaan dataan

**Files:**
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (kenttä + luentatick n. rivi 371 + WriteMachineInsightsInBackground n. rivi 636)

**Interfaces:**
- Consumes: `MachineSpecReader.Read(groups, osDescription, userNotes)` (Task 1), `MachineInsightsInput` (Task 2), `_settings.InsightsNotes` (Task 7), `_sensorService.Read()` (olemassa).
- Produces: machine-insights.md generoituu oikealla kokoonpanolla, 7 pv statseilla ja lisätiedoilla.

- [ ] **Step 1: Lisää kentät**

Muiden yksityisten kenttien joukkoon:

```csharp
    /// <summary>Viimeisin sensoriluenta konetuntemus-lokin kokoonpano-osiota varten.</summary>
    private IReadOnlyList<HardwareGroup>? _latestGroups;

    /// <summary>Esim. "Windows 11 (build 26200)" — build ≥ 22000 on Windows 11.</summary>
    private static readonly string OsDescriptionText =
        $"Windows {(Environment.OSVersion.Version.Build >= 22000 ? 11 : 10)} " +
        $"(build {Environment.OSVersion.Version.Build})";
```

- [ ] **Step 2: Talleta luenta**

Rivin `IReadOnlyList<HardwareGroup> groups = _sensorService.Read();` (n. rivi 371) jälkeen:

```csharp
            _latestGroups = groups;
```

(Viittausasetus on atominen; taustasäie saa korkeintaan hieman vanhan listan — ei haittaa.)

- [ ] **Step 3: Korvaa väliaikainen spec oikealla**

`WriteMachineInsightsInBackground`-metodissa korvaa Task 2:n rivi

```csharp
                    MachineSpecReader.Read(Array.Empty<HardwareGroup>(), "", ""),
```

muotoon

```csharp
                    MachineSpecReader.Read(
                        _latestGroups ?? Array.Empty<HardwareGroup>(),
                        OsDescriptionText,
                        _settings.InsightsNotes),
```

- [ ] **Step 4: Aja kaikki testit ja buildaa**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → Expected: kaikki passed.
Run: `dotnet build HardwareMonitor.sln` → Expected: Build succeeded.

- [ ] **Step 5: Commit**

```powershell
git add src/HardwareMonitor.App/ViewModels/MainViewModel.cs
git commit -m "Kytke konetuntemus-loki oikeaan kokoonpanoon, 7 pv statseihin ja lisätietoihin"
```

---

### Task 9: Ajonaikainen todennus + ROADMAP

**Files:**
- Modify: `docs/ROADMAP.md` (merkitse machine-insights v2 tehdyksi)
- Verify: `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md`

- [ ] **Step 1: Varmista että sovellus ei ole ajossa, buildaa**

Käyttäjä sulkee sovelluksen trayn kautta (Lopeta), jos ajossa. Sitten:
Run: `dotnet build HardwareMonitor.sln` → Build succeeded.

- [ ] **Step 2: Käyttäjä käynnistää sovelluksen** ("Hardware Monitor.lnk", UAC).
Insights-kirjoitus tapahtuu ~60 s käynnistyksestä (tick % 1800 == 60).

- [ ] **Step 3: Lue generoitu tiedosto ja tarkista osiot**

Lue `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md`. Odotettu: johdanto tekoälylle, kokoonpano (i9-9900K, RTX 2060, Z390-F, 64 GB, "2 × Samsung SSD 860 EVO 1TB", Windows 11), trendit, tapahtumalistaus aikaleimoineen (paikallisaika), lisätiedot jos käyttäjä ehti asettaa. HUOM: kieli = UI-kieli.

- [ ] **Step 4: Päivitä ROADMAP ja committaa**

Lisää/merkitse ROADMAP.md:hen machine-insights v2 valmiiksi (10.7.2026).

```powershell
git add docs/ROADMAP.md
git commit -m "Merkitse machine-insights v2 valmiiksi ROADMAPiin"
git push
```
