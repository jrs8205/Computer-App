# fi/en-kielituki — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Koko sovellus (UI + Coren tuottamat sisällöt) suomeksi ja englanniksi; kielivalinta Asetusten Yleiset-ryhmässä, voimaan uudelleenkäynnistyksellä.

**Architecture:** resx-resurssit, neutraali kieli suomi (`NeutralResourcesLanguage("fi")`), englanti satelliittina. Koska PublicResXFileCodeGenerator toimii vain Visual Studiossa (ei dotnet CLI:llä), resursseihin kirjoitetaan ohuet käsintehdyt accessor-luokat (`Strings` Coreen, `UiStrings` Appiin) — staattiset propertyt toimivat XAML:n `x:Static`-laajennuksen kanssa ja kääntäjä valvoo avainten käytön. Kieli asetetaan `App.OnStartup`issa `CultureInfo.DefaultThreadCurrentUICulture`-arvoon ennen ikkunoiden luontia; `CurrentCulture` (numeromuotoilut) ei muutu.

**Tech Stack:** .resx + ResourceManager, ei uusia NuGet-paketteja.

**Spec:** `docs/superpowers/specs/2026-07-09-localization-design.md`

**Huom. käännöksistä:** fi-tekstit siirretään resursseihin SELLAISINAAN (ei
tekstimuutoksia samalla). En-käännökset kirjoitetaan suoraan .en.resx-
tiedostoihin migraation yhteydessä (amerikanenglanti, tekninen sanasto);
niitä ei listata tässä suunnitelmassa erikseen.

## Global Constraints

- `dotnet test` EI buildaa App-projektia — UI-muutosten jälkeen `dotnet build HardwareMonitor.sln`.
- Pysäytä HardwareMonitor.exe ennen buildia (käyttäjä sulkee trayn kautta).
- Resurssiavainten nimeäminen: PascalCase, tiedostokohtainen etuliite
  (esim. `Report_`, `Risk_`, `Csv_`, `Insights_`, `Threshold_`, `WinEvent_`,
  `Notify_`, `Validate_`; UiStrings: `Tab_`, `Top_`, `Set_`, `Hist_`,
  `Dash_`, `Tray_`, `Dlg_`, `Status_`, `Overlay_`).
- Parametrilliset viestit: format-string resurssissa (`{0}`, `{1}`, …),
  kutsu `string.Format(Strings.Avain, ...)`. Formatointi CurrentCulturella
  ellei koodissa jo toisin (CSV/raportti käyttävät nykyisiä kulttuurejaan).
- Testikomento: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
- Commit-viestit suomeksi + `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: Lokalisointi-infra (LanguageResolver TDD, kulttuurin asetus, kielivalitsin)

**Files:**
- Create: `src/HardwareMonitor.Core/Localization/LanguageResolver.cs`
- Test: `src/HardwareMonitor.Tests/Localization/LanguageResolverTests.cs`
- Create: `src/HardwareMonitor.Tests/TestCulture.cs` (kulttuurikiinnitys)
- Modify: `src/HardwareMonitor.Core/Settings/AppSettings.cs` (Language)
- Modify: `src/HardwareMonitor.Core/HardwareMonitor.Core.csproj` + `src/HardwareMonitor.App/HardwareMonitor.App.csproj` (`<NeutralLanguage>fi</NeutralLanguage>`)
- Modify: `src/HardwareMonitor.App/App.xaml.cs` (kulttuuri OnStartupissa)
- Modify: `src/HardwareMonitor.App/ViewModels/SettingsViewModel.cs` (LanguageIndex)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (Kieli-ComboBox Yleiset-ryhmään)

**Interfaces:**
- Produces: `LanguageResolver.Resolve(string language, CultureInfo installedUi)` →
  CultureInfo ("fi" → fi-FI, "en" → en-US, "" tai tuntematon → fi-FI jos
  installedUi.TwoLetterISOLanguageName == "fi", muuten en-US);
  `AppSettings.Language` (string, oletus "");
  `SettingsViewModel.LanguageIndex` (int 0/1/2 ↔ ""/fi/en).

- [ ] **Step 1: LanguageResolver-testit (RED)**

`src/HardwareMonitor.Tests/Localization/LanguageResolverTests.cs`:

```csharp
using System.Globalization;
using HardwareMonitor.Core.Localization;
using Xunit;

namespace HardwareMonitor.Tests.Localization;

public class LanguageResolverTests
{
    private static readonly CultureInfo FinnishOs = CultureInfo.GetCultureInfo("fi-FI");
    private static readonly CultureInfo EnglishOs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo SwedishOs = CultureInfo.GetCultureInfo("sv-SE");

    [Fact]
    public void Fi_AntaaSuomen()
    {
        Assert.Equal("fi-FI", LanguageResolver.Resolve("fi", EnglishOs).Name);
    }

    [Fact]
    public void En_AntaaEnglannin()
    {
        Assert.Equal("en-US", LanguageResolver.Resolve("en", FinnishOs).Name);
    }

    [Fact]
    public void Automaattinen_SuomiKoneella_AntaaSuomen()
    {
        Assert.Equal("fi-FI", LanguageResolver.Resolve("", FinnishOs).Name);
    }

    [Fact]
    public void Automaattinen_MuuKieliKoneella_AntaaEnglannin()
    {
        Assert.Equal("en-US", LanguageResolver.Resolve("", EnglishOs).Name);
        Assert.Equal("en-US", LanguageResolver.Resolve("", SwedishOs).Name);
    }

    [Fact]
    public void TuntematonArvo_ToimiiKutenAutomaattinen()
    {
        Assert.Equal("fi-FI", LanguageResolver.Resolve("xyz", FinnishOs).Name);
    }
}
```

- [ ] **Step 2: Aja testit — RED** (CS0246: LanguageResolver puuttuu)

- [ ] **Step 3: Toteutus (GREEN)**

`src/HardwareMonitor.Core/Localization/LanguageResolver.cs`:

```csharp
using System.Globalization;

namespace HardwareMonitor.Core.Localization;

/// <summary>
/// Ratkaisee UI-kielen asetuksesta: "fi"/"en" suoraan, muu (ml. "" =
/// automaattinen) Windowsin kielestä — suomi suomenkielisellä koneella,
/// muuten englanti.
/// </summary>
public static class LanguageResolver
{
    public static CultureInfo Resolve(string language, CultureInfo installedUi) =>
        language switch
        {
            "fi" => CultureInfo.GetCultureInfo("fi-FI"),
            "en" => CultureInfo.GetCultureInfo("en-US"),
            _ => installedUi.TwoLetterISOLanguageName == "fi"
                ? CultureInfo.GetCultureInfo("fi-FI")
                : CultureInfo.GetCultureInfo("en-US"),
        };
}
```

Samassa stepissä `src/HardwareMonitor.Tests/TestCulture.cs` — suomenkieliset
assertiot eivät saa riippua ajokoneen kielestä:

```csharp
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HardwareMonitor.Tests;

/// <summary>Testit ajetaan aina suomenkielisellä UI-kulttuurilla (neutraali kieli).</summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Init() =>
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("fi-FI");
}
```

ja `AppSettings`iin (`MinimizeToTray`-propertyn edelle):

```csharp
    /// <summary>UI-kieli: "" = automaattinen (Windowsin kielestä), "fi", "en".</summary>
    public string Language { get; set; } = "";
```

- [ ] **Step 4: Aja testit — GREEN** (126 + 5 = 131)

- [ ] **Step 5: NeutralLanguage + kulttuurin asetus + kielivalitsin**

Molempiin csproj-tiedostoihin (PropertyGroupiin): `<NeutralLanguage>fi</NeutralLanguage>`

`App.xaml.cs` — OnStartupin ALKUUN ennen ikkunan luontia:

```csharp
        // Kieli asetuksista ennen ikkunoiden luontia; kattaa myös taustasäikeet.
        AppSettings settings = new SettingsService().Load();
        CultureInfo ui = LanguageResolver.Resolve(
            settings.Language, CultureInfo.InstalledUICulture);
        CultureInfo.DefaultThreadCurrentUICulture = ui;
        Thread.CurrentThread.CurrentUICulture = ui;
```

(+ usingit `System.Globalization`, `HardwareMonitor.Core.Localization`,
`HardwareMonitor.Core.Settings` — tarkista olemassa olevat.)

`SettingsViewModel.cs` — kenttä + property (OverlayFontSize-propertyn jälkeen):

```csharp
    private static readonly string[] LanguageByIndex = { "", "fi", "en" };

    public int LanguageIndex
    {
        get => Math.Max(0, Array.IndexOf(LanguageByIndex, _settings.Language));
        set
        {
            if (value < 0 || value >= LanguageByIndex.Length
                || _settings.Language == LanguageByIndex[value])
            {
                return;
            }

            _settings.Language = LanguageByIndex[value];
            _save();
        }
    }
```

`MainWindow.xaml` — Yleiset-ryhmän loppuun (Hälytysilmoitukset-checkboxin jälkeen):

```xml
                                <StackPanel Orientation="Horizontal" Margin="0,8,0,2">
                                    <TextBlock Text="Kieli / Language" Foreground="#E0E0E0" Width="120"
                                               VerticalAlignment="Center" />
                                    <ComboBox SelectedIndex="{Binding SettingsPage.LanguageIndex}" Width="130"
                                              VerticalAlignment="Center">
                                        <ComboBoxItem Content="Automaattinen" />
                                        <ComboBoxItem Content="Suomi" />
                                        <ComboBoxItem Content="English" />
                                    </ComboBox>
                                    <TextBlock Style="{StaticResource NoteText}"
                                               Text="vaikuttaa uudelleenkäynnistyksen jälkeen"
                                               VerticalAlignment="Center" Margin="8,0,0,0" />
                                </StackPanel>
```

(Tekstit siirtyvät UiStrings-resursseihin Task 3:ssa.)

- [ ] **Step 6: Buildaa (0 varoitusta) + testit (131) + commit**

```powershell
git add -A src
git commit -m @'
Lisää kielituen infra: LanguageResolver, Language-asetus ja kielivalitsin

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 2: Coren tekstit resursseihin + englanninkäännökset (TDD)

**Files:**
- Create: `src/HardwareMonitor.Core/Localization/Strings.resx` (fi, neutraali)
- Create: `src/HardwareMonitor.Core/Localization/Strings.en.resx`
- Create: `src/HardwareMonitor.Core/Localization/Strings.cs` (accessor)
- Modify (fi-tekstit → resurssiavaimet): `NotificationBuilder.cs` (2),
  `SettingsValidator.cs` (2), `ThresholdMonitor.cs` (7),
  `WindowsEventClassifier.cs` (6), `RiskAnalyzer.cs` (24),
  `CsvExporter.cs` (16), `ReportBuilder.cs` (36),
  `MachineInsightsBuilder.cs` (28)
- Test: `src/HardwareMonitor.Tests/Localization/EnglishResourceTests.cs`

**Interfaces:**
- Produces: `HardwareMonitor.Core.Localization.Strings` — staattinen luokka,
  yksi property per avain, esim. `Strings.Threshold_WarningExceeded` →
  format-string `"{0} ylitti varoitusrajan: {1} {3} (raja {2} {3})"`.
  Accessor-malli:

```csharp
using System.Globalization;
using System.Resources;

namespace HardwareMonitor.Core.Localization;

/// <summary>
/// Coren lokalisoidut tekstit (Strings.resx = fi/neutraali, Strings.en.resx = en).
/// Käsintehty accessor, koska resx-designer-generointi ei toimi dotnet CLI:llä.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Rm =
        new("HardwareMonitor.Core.Localization.Strings", typeof(Strings).Assembly);

    private static string T(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Validate_EnterNumber => T(nameof(Validate_EnterNumber));
    public static string Validate_AllowedRange => T(nameof(Validate_AllowedRange));
    // ... yksi rivi per avain, sama kaava
}
```

- [ ] **Step 1: Englanti-testit (RED)**

`src/HardwareMonitor.Tests/Localization/EnglishResourceTests.cs` — ajetaan
en-kulttuurissa väliaikaisesti:

```csharp
using System.Globalization;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using Xunit;

namespace HardwareMonitor.Tests.Localization;

public class EnglishResourceTests
{
    private sealed class EnglishUi : IDisposable
    {
        private readonly CultureInfo _old = CultureInfo.CurrentUICulture;

        public EnglishUi() =>
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        public void Dispose() => CultureInfo.CurrentUICulture = _old;
    }

    [Fact]
    public void Raja_arvoviesti_KaantyyEnglanniksi()
    {
        using var _ = new EnglishUi();
        var monitor = new ThresholdMonitor(new ThresholdSettings());
        var metrics = new KeyMetrics(null, 90, null, null, null, null, null, null,
            null, null, null, null, null,
            Array.Empty<DiskMetrics>(), Array.Empty<FanMetrics>());
        var t0 = new DateTimeOffset(2026, 7, 9, 21, 0, 0, TimeSpan.FromHours(3));

        var all = new List<ThresholdEvent>();
        for (int s = 0; s <= 60; s++)
        {
            all.AddRange(monitor.Update(metrics, t0.AddSeconds(s),
                new Dictionary<string, string>()).Events);
        }

        ThresholdEvent e = Assert.Single(all);
        Assert.Contains("exceeded the warning limit", e.Message);
        Assert.DoesNotContain("ylitti", e.Message);
    }

    [Fact]
    public void Validointivirhe_KaantyyEnglanniksi()
    {
        using var _ = new EnglishUi();
        Assert.Equal("Enter a number",
            SettingsValidator.ParseNumber("abc", 20, 120).Error);
    }

    [Fact]
    public void SuomiPysyySamana()
    {
        // Neutraali resurssi = nykyiset fi-tekstit sellaisinaan.
        Assert.Equal("Anna numero",
            SettingsValidator.ParseNumber("abc", 20, 120).Error);
    }
}
```

(KeyMetrics-konstruktoriin katsotaan mallia ThresholdMonitorTestsin
Metrics-helperistä — parametrijärjestys sieltä.)

- [ ] **Step 2: Aja testit — RED** (englantia ei tule, koska resursseja ei ole)

- [ ] **Step 3: Migroi tiedosto kerrallaan**

Järjestys pienestä isoon, jokaisen jälkeen `dotnet test`:
1. `SettingsValidator.cs`: `"Anna numero"` → `Strings.Validate_EnterNumber`;
   `$"Sallittu väli on {min:0}–{max:0}"` →
   `string.Format(Strings.Validate_AllowedRange, min, max)` jossa resurssi
   `"Sallittu väli on {0:0}–{1:0}"` / en `"Allowed range is {0:0}–{1:0}"`.
2. `NotificationBuilder.cs` (otsikot: Varoitus / Kriittinen hälytys /
   {0} hälytystä).
3. `ThresholdMonitor.cs` (ylitys/palautus/tuuletinviestit + kesto-formaatti).
4. `WindowsEventClassifier.cs` (6 luokitteluviestiä).
5. `RiskAnalyzer.cs` (tilat, riskitasot, havainnot, suositukset).
6. `CsvExporter.cs` (otsikkorivin sarakenimet).
7. `ReportBuilder.cs` (otsikot, selitteet, vertailulauseet, sanasto).
8. `MachineInsightsBuilder.cs` (otsikot, taulukko-otsikot, vinkit).

Jokainen fi-merkkijono siirretään Strings.resx:ään TÄSMÄLLEEN nykyisellään
(testit valvovat tätä), en-käännös Strings.en.resx:ään, avain molempien
resurssien lisäksi accessor-luokkaan. resx-tiedostomuoto: vakio
resx-XML-runko (`<root>` + `resheader`-rivit + `<data name="Avain"
xml:space="preserve"><value>Teksti</value></data>`).

- [ ] **Step 4: Aja kaikki testit — GREEN** (131 + 3 = 134; vanhat fi-assertiot
  läpi TestCulture-kiinnityksen ansiosta)

- [ ] **Step 5: Commit**

```powershell
git add -A src
git commit -m @'
Siirrä Coren tekstit resursseihin ja lisää englanninkäännökset

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 3: App-kerroksen tekstit resursseihin (XAML + VM:t)

**Files:**
- Create: `src/HardwareMonitor.App/Localization/UiStrings.resx` + `UiStrings.en.resx` + `UiStrings.cs` (sama accessor-malli kuin Coressa,
  namespace `HardwareMonitor.App.Localization`, resurssinimi
  `HardwareMonitor.App.Localization.UiStrings`)
- Modify: `MainWindow.xaml` (~67 tekstiä: `Text=`, `Content=`, `Header=`,
  `ToolTip=` → `{x:Static loc:UiStrings.Avain}`;
  xmlns: `xmlns:loc="clr-namespace:HardwareMonitor.App.Localization"`)
- Modify: `MainWindow.xaml.cs` (tray-valikko Näytä/Overlay/Lopeta,
  SaveFileDialog-otsikot/suodattimet, MessageBoxit)
- Modify: `MainViewModel.cs` (Status-tekstit, virherivit),
  `SettingsViewModel.cs` (rivien nimikkeet ja yksikkötekstit ctorissa),
  `DashboardViewModel.cs` (1), `OverlayWindow.xaml` (1),
  `HistoryViewModel.cs` (ei tekstejä — sarjanimet CPU/GPU/RAM universaaleja;
  Coren ChartHistoryBuilderin "GPU hotspot" jää sellaisenaan molempiin kieliin)
- Modify: `App.xaml.cs` — ei lisämuutoksia (kulttuuri jo Task 1:ssä)

Huom: ikkunan otsikko, "Windows 11 Hardware Monitor" -brändi ja
tuulettimien oletusnimet (Fan #1) eivät käänny. Kieli-ComboBoxin
vaihtoehdot ("Automaattinen"/"Suomi"/"English") lokalisoidaan:
Automaattinen → "Automatic" en-kielellä, kielten omat nimet pysyvät.

- [ ] **Step 1: UiStrings.resx + .en.resx + accessor, migroi XAML ja VM:t**

Sama mekaniikka kuin Task 2:ssa; XAML-esimerkki:

```xml
<TabItem Header="{x:Static loc:UiStrings.Tab_Settings}">
<CheckBox Content="{x:Static loc:UiStrings.Set_MinimizeToTray}" ... />
```

VM-esimerkki (SettingsViewModel ctor):

```csharp
Pair(UiStrings.Set_CpuTemp, "°C", 20, 120, ...)
```

- [ ] **Step 2: Buildaa (0 varoitusta) + kaikki testit (134)**

- [ ] **Step 3: Commit**

```powershell
git add -A src
git commit -m @'
Siirrä käyttöliittymän tekstit resursseihin (fi/en)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 4: Ajonaikainen todennus + HANDOFF + push

- [ ] **Step 1: Todenna suomeksi** — `.\run.ps1 -AsAdmin` (asetus
  Automaattinen → fi tällä koneella). Käyttäjä tarkistaa: UI ennallaan
  suomeksi, tilapaneeli, raportti ja CSV suomeksi, ei regressioita.

- [ ] **Step 2: Todenna englanniksi** — käyttäjä vaihtaa Kieli → English,
  sulkee (tray → Lopeta) ja Claude käynnistää uudelleen. Tarkistukset:
  välilehdet/napit/asetukset englanniksi, tilapaneelin havainnot ja
  suositukset englanniksi, Luo raportti → englanninkielinen raportti,
  CSV-otsikot englanniksi (desimaalipilkku säilyy), machine-insights.md
  generoituu englanniksi 60 s sisällä käynnistyksestä (MainViewModelin
  insights-kirjoitus), tray-valikko englanniksi.

- [ ] **Step 3: Palauta kieli** — käyttäjän valinnan mukaan (Automaattinen
  tai Suomi), uudelleenkäynnistys.

- [ ] **Step 4: HANDOFF-päivitys (kielituki tehty) + commit + push**

```powershell
git add HANDOFF.md
git commit -m @'
Päivitä HANDOFF: fi/en-kielituki valmis

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
git push
```
