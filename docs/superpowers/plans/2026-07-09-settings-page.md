# Asetussivu (Vaihe 8.2) — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Asetukset-välilehti, jolta kaikki asetukset (rajat, kestot, lokitus, overlay) voi muokata validoidusti ilman settings.jsonin käsin muokkausta; samalla yläpalkki siivotaan.

**Architecture:** Puhdas `SettingsValidator` Coreen (TDD). App-puolelle `NumericFieldViewModel` (teksti↔float + virhetila) ja `SettingsViewModel` (rivilistat, oletusten palautus), jotka kirjoittavat suoraan olemassa oleviin asetusolioihin viitteitä vaihtamatta — ThresholdMonitor lukee samaa `ThresholdSettings`-oliota, joten rajat vaikuttavat heti. Checkboxit ja kulma/läpinäkyvyys sidotaan MainViewModelin olemassa oleviin propertyihin (XAML vain siirtyy).

**Tech Stack:** C# / WPF (net8.0-windows), xUnit. Ei uusia NuGet-paketteja.

**Spec:** `docs/superpowers/specs/2026-07-09-settings-page-design.md`

## Global Constraints

- `dotnet test` EI buildaa App-projektia — UI-muutosten jälkeen aja AINA `dotnet build HardwareMonitor.sln`.
- Pysäytä käynnissä oleva HardwareMonitor.exe ennen buildia (lukitsee DLL:t).
- Kaikki UI-tekstit ja virheviestit suomeksi; kirjoita ne vain yhteen paikkaan (resx-siirto tulee myöhemmin).
- Asetusolioiden (AppSettings.Thresholds, .Logging, .Overlay) VIITTEET eivät saa vaihtua — muut komponentit pitävät niihin viitteitä.
- WPF: älä aseta paikallisia arvoja sidottuihin DP:ihin; visuaalitilat kulkevat VM:n kautta.
- Testikomento: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
- Commit-viestit suomeksi + `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: SettingsValidator (Core, TDD)

**Files:**
- Create: `src/HardwareMonitor.Core/Settings/SettingsValidator.cs`
- Test: `src/HardwareMonitor.Tests/Settings/SettingsValidatorTests.cs`

**Interfaces:**
- Consumes: —
- Produces: `ParseResult(float? Value, string? Error)` jossa `bool Ok`;
  `SettingsValidator.ParseNumber(string raw, float min, float max)` → ParseResult;
  `SettingsValidator.ValidateWarnCrit(float warn, float crit)` → string? (virhe tai null).
  Task 2 kutsuu molempia.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

`src/HardwareMonitor.Tests/Settings/SettingsValidatorTests.cs`:

```csharp
using HardwareMonitor.Core.Settings;
using Xunit;

namespace HardwareMonitor.Tests.Settings;

public class SettingsValidatorTests
{
    [Fact]
    public void PilkkuDesimaali_Kelpaa()
    {
        ParseResult r = SettingsValidator.ParseNumber("85,5", 20, 120);
        Assert.True(r.Ok);
        Assert.Equal(85.5f, r.Value);
    }

    [Fact]
    public void PisteDesimaali_Kelpaa()
    {
        ParseResult r = SettingsValidator.ParseNumber("85.5", 20, 120);
        Assert.True(r.Ok);
        Assert.Equal(85.5f, r.Value);
    }

    [Fact]
    public void ValilyonnitTrimmataan()
    {
        Assert.True(SettingsValidator.ParseNumber(" 85 ", 20, 120).Ok);
    }

    [Fact]
    public void TyhjaSyote_AntaaVirheen()
    {
        ParseResult r = SettingsValidator.ParseNumber("", 20, 120);
        Assert.False(r.Ok);
        Assert.Equal("Anna numero", r.Error);
    }

    [Fact]
    public void Roskasyote_AntaaVirheen()
    {
        Assert.Equal("Anna numero", SettingsValidator.ParseNumber("abc", 20, 120).Error);
    }

    [Fact]
    public void AlleMinimin_KertooSallitunValin()
    {
        Assert.Equal("Sallittu väli on 20–120",
            SettingsValidator.ParseNumber("10", 20, 120).Error);
    }

    [Fact]
    public void YliMaksimin_KertooSallitunValin()
    {
        Assert.Equal("Sallittu väli on 20–120",
            SettingsValidator.ParseNumber("150", 20, 120).Error);
    }

    [Fact]
    public void RajatKelpaavat()
    {
        Assert.True(SettingsValidator.ParseNumber("20", 20, 120).Ok);
        Assert.True(SettingsValidator.ParseNumber("120", 20, 120).Ok);
    }

    [Fact]
    public void VaroitusrajanOltavaPienempiKuinKriittisen()
    {
        Assert.NotNull(SettingsValidator.ValidateWarnCrit(95, 95));
        Assert.NotNull(SettingsValidator.ValidateWarnCrit(96, 95));
        Assert.Null(SettingsValidator.ValidateWarnCrit(85, 95));
    }
}
```

- [ ] **Step 2: Aja testit — varmista RED**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
Expected: FAIL, käännösvirhe CS0117/CS0246: `SettingsValidator`/`ParseResult` puuttuu.

- [ ] **Step 3: Minimitoteutus**

`src/HardwareMonitor.Core/Settings/SettingsValidator.cs`:

```csharp
using System.Globalization;

namespace HardwareMonitor.Core.Settings;

/// <summary>Numerokentän validointitulos: arvo TAI suomenkielinen virheviesti.</summary>
public sealed record ParseResult(float? Value, string? Error)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Asetussivun syötteiden validointi (puhdas, Vaihe 8.2). Parsinta hyväksyy
/// sekä desimaalipilkun (fi) että -pisteen (invariant) — fi kokeillaan ensin,
/// jotta "85,5" ei tulkkaudu invariantin tuhaterottimeksi.
/// </summary>
public static class SettingsValidator
{
    private static readonly CultureInfo Fi = CultureInfo.GetCultureInfo("fi-FI");

    public static ParseResult ParseNumber(string raw, float min, float max)
    {
        string trimmed = raw?.Trim() ?? "";
        if (trimmed.Length == 0
            || (!float.TryParse(trimmed, NumberStyles.Float, Fi, out float value)
                && !float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value)))
        {
            return new ParseResult(null, "Anna numero");
        }

        if (value < min || value > max)
        {
            return new ParseResult(null, $"Sallittu väli on {min:0}–{max:0}");
        }

        return new ParseResult(value, null);
    }

    public static string? ValidateWarnCrit(float warn, float crit) =>
        warn >= crit ? "Varoitusrajan on oltava pienempi kuin kriittisen rajan" : null;
}
```

- [ ] **Step 4: Aja testit — varmista GREEN**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
Expected: PASS, 116 testiä (107 vanhaa + 9 uutta), 0 varoitusta.

- [ ] **Step 5: Commit**

```powershell
git add src/HardwareMonitor.Core/Settings/SettingsValidator.cs src/HardwareMonitor.Tests/Settings/SettingsValidatorTests.cs
git commit -m @'
Lisää SettingsValidator asetussivua varten (TDD)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 2: NumericFieldViewModel + SettingsViewModel + MainViewModel.SettingsPage

**Files:**
- Create: `src/HardwareMonitor.App/ViewModels/NumericFieldViewModel.cs`
- Create: `src/HardwareMonitor.App/ViewModels/SettingsViewModel.cs`
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (ctor + property)

**Interfaces:**
- Consumes: `SettingsValidator.ParseNumber/ValidateWarnCrit` (Task 1),
  `AppSettings`/`ThresholdSettings`/`LoggingSettings`/`OverlaySettings` (olemassa),
  `MainViewModel.OnOverlaySettingChanged(string)` (olemassa: PropertyChanged + Save + OverlaySettingsChanged).
- Produces: `MainViewModel.SettingsPage` (SettingsViewModel);
  `SettingsViewModel.ThresholdRows: IReadOnlyList<ThresholdRow>`,
  `.DurationRows`/`.LoggingRows: IReadOnlyList<FieldRow>`,
  `.OverlayFontSize: NumericFieldViewModel`, `.ResetThresholds()`;
  `ThresholdRow(Label, Unit, Warn, Crit)`, `FieldRow(Label, Unit, Field, Note)`;
  `NumericFieldViewModel.Text` (string, two-way), `.Error` (string?), `.HasError` (bool).
  Task 3:n XAML sitoo näihin.

- [ ] **Step 1: Luo NumericFieldViewModel**

`src/HardwareMonitor.App/ViewModels/NumericFieldViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Globalization;
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Yksi numeroasetuskenttä: teksti ↔ float. Kelvollinen arvo menee
/// apply-delegaatille (kirjoitus AppSettingsiin + tallennus); virheellinen
/// jättää virheviestin näkyviin eikä tallenna mitään.
/// </summary>
public sealed class NumericFieldViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Fi = CultureInfo.GetCultureInfo("fi-FI");

    private readonly float _min;
    private readonly float _max;
    private readonly Action<float> _apply;
    private readonly Func<float, string?>? _crossCheck;

    private string _text;
    private string? _error;

    public NumericFieldViewModel(
        float initialValue, float min, float max,
        Action<float> apply, Func<float, string?>? crossCheck = null)
    {
        _min = min;
        _max = max;
        _apply = apply;
        _crossCheck = crossCheck;
        _text = Format(initialValue);
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            ParseResult result = SettingsValidator.ParseNumber(value, _min, _max);
            string? error = result.Error
                ?? (result.Value is { } v ? _crossCheck?.Invoke(v) : null);
            if (error is null && result.Value is { } ok)
            {
                _apply(ok);
                _text = Format(ok);
            }

            Error = error;
            OnChanged(nameof(Text));
        }
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (_error == value)
            {
                return;
            }

            _error = value;
            OnChanged(nameof(Error));
            OnChanged(nameof(HasError));
        }
    }

    public bool HasError => _error is not null;

    /// <summary>Päivittää tekstin ulkoisen muutoksen (oletusten palautus) jälkeen tallentamatta.</summary>
    public void Refresh(float value)
    {
        _text = Format(value);
        Error = null;
        OnChanged(nameof(Text));
    }

    private static string Format(float v) => v.ToString("0.##", Fi);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 2: Luo SettingsViewModel**

`src/HardwareMonitor.App/ViewModels/SettingsViewModel.cs`:

```csharp
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.App.ViewModels;

/// <summary>Raja-arvorivi Asetukset-välilehdellä: varoitus + kriittinen kenttä.</summary>
public sealed record ThresholdRow(
    string Label, string Unit, NumericFieldViewModel Warn, NumericFieldViewModel Crit);

/// <summary>Yhden kentän rivi (kestot, lokitus); Note on harmaa lisähuomautus.</summary>
public sealed record FieldRow(
    string Label, string Unit, NumericFieldViewModel Field, string? Note = null);

/// <summary>
/// Asetukset-välilehden numerokentät validointeineen (spec
/// docs/superpowers/specs/2026-07-09-settings-page-design.md). Kelvollinen
/// arvo tallentuu ja vaikuttaa heti — ThresholdMonitor lukee samaa
/// ThresholdSettings-oliota, joten olioiden viitteet eivät vaihdu missään.
/// Checkboxit ja kulma/läpinäkyvyys sidotaan MainViewModelin propertyihin.
/// </summary>
public sealed class SettingsViewModel
{
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly List<(NumericFieldViewModel Field, Func<float> Get)> _fields = new();

    public SettingsViewModel(AppSettings settings, Action save)
    {
        _settings = settings;
        _save = save;
        ThresholdSettings t = settings.Thresholds;

        ThresholdRows = new[]
        {
            Pair("CPU-lämpötila", "°C", 20, 120,
                () => t.CpuWarningTemp, v => t.CpuWarningTemp = v,
                () => t.CpuCriticalTemp, v => t.CpuCriticalTemp = v),
            Pair("GPU-lämpötila", "°C", 20, 120,
                () => t.GpuWarningTemp, v => t.GpuWarningTemp = v,
                () => t.GpuCriticalTemp, v => t.GpuCriticalTemp = v),
            Pair("GPU hotspot", "°C", 20, 120,
                () => t.GpuHotspotWarningTemp, v => t.GpuHotspotWarningTemp = v,
                () => t.GpuHotspotCriticalTemp, v => t.GpuHotspotCriticalTemp = v),
            Pair("NVMe-levyt", "°C", 20, 120,
                () => t.NvmeWarningTemp, v => t.NvmeWarningTemp = v,
                () => t.NvmeCriticalTemp, v => t.NvmeCriticalTemp = v),
            Pair("RAM-käyttö", "%", 10, 100,
                () => t.RamWarningPercent, v => t.RamWarningPercent = v,
                () => t.RamCriticalPercent, v => t.RamCriticalPercent = v),
        };

        DurationRows = new[]
        {
            Row("Varoituksen kesto ennen tapahtumaa", "s", 1, 600,
                () => t.WarningSustainSeconds,
                v => t.WarningSustainSeconds = (int)MathF.Round(v)),
            Row("Kriittisen kesto ennen tapahtumaa", "s", 1, 600,
                () => t.CriticalSustainSeconds,
                v => t.CriticalSustainSeconds = (int)MathF.Round(v)),
            Row("Saman hälytyksen väli (cooldown)", "min", 1, 60,
                () => t.EventCooldownMinutes,
                v => t.EventCooldownMinutes = (int)MathF.Round(v)),
            Row("Tuuletinpysähdyksen CPU-raja", "°C", 20, 120,
                () => t.FanStopCpuTemp, v => t.FanStopCpuTemp = v),
        };

        LoggingRows = new[]
        {
            Row("Koosteväli", "s", 1, 60,
                () => _settings.Logging.SensorIntervalSeconds,
                v => _settings.Logging.SensorIntervalSeconds = (int)MathF.Round(v),
                "vaikuttaa seuraavasta käynnistyksestä"),
            Row("Historian säilytys", "pv", 1, 365,
                () => _settings.Logging.KeepHistoryDays,
                v => _settings.Logging.KeepHistoryDays = (int)MathF.Round(v)),
        };

        OverlayFontSize = Register(
            new NumericFieldViewModel((float)settings.Overlay.FontSize, 8, 32,
                Apply(v => settings.Overlay.FontSize = v)),
            () => (float)settings.Overlay.FontSize);
    }

    public IReadOnlyList<ThresholdRow> ThresholdRows { get; }

    public IReadOnlyList<FieldRow> DurationRows { get; }

    public IReadOnlyList<FieldRow> LoggingRows { get; }

    public NumericFieldViewModel OverlayFontSize { get; }

    /// <summary>Palauttaa raja-arvot ja kestot oletuksiin. Viite säilyy!</summary>
    public void ResetThresholds()
    {
        var d = new ThresholdSettings();
        ThresholdSettings t = _settings.Thresholds;
        t.CpuWarningTemp = d.CpuWarningTemp;
        t.CpuCriticalTemp = d.CpuCriticalTemp;
        t.GpuWarningTemp = d.GpuWarningTemp;
        t.GpuCriticalTemp = d.GpuCriticalTemp;
        t.GpuHotspotWarningTemp = d.GpuHotspotWarningTemp;
        t.GpuHotspotCriticalTemp = d.GpuHotspotCriticalTemp;
        t.NvmeWarningTemp = d.NvmeWarningTemp;
        t.NvmeCriticalTemp = d.NvmeCriticalTemp;
        t.RamWarningPercent = d.RamWarningPercent;
        t.RamCriticalPercent = d.RamCriticalPercent;
        t.FanStopCpuTemp = d.FanStopCpuTemp;
        t.WarningSustainSeconds = d.WarningSustainSeconds;
        t.CriticalSustainSeconds = d.CriticalSustainSeconds;
        t.EventCooldownMinutes = d.EventCooldownMinutes;
        _save();

        foreach ((NumericFieldViewModel field, Func<float> get) in _fields)
        {
            field.Refresh(get());
        }
    }

    private ThresholdRow Pair(
        string label, string unit, float min, float max,
        Func<float> getWarn, Action<float> setWarn,
        Func<float> getCrit, Action<float> setCrit)
    {
        NumericFieldViewModel warn = Register(
            new NumericFieldViewModel(getWarn(), min, max, Apply(setWarn),
                v => SettingsValidator.ValidateWarnCrit(v, getCrit())),
            getWarn);
        NumericFieldViewModel crit = Register(
            new NumericFieldViewModel(getCrit(), min, max, Apply(setCrit),
                v => SettingsValidator.ValidateWarnCrit(getWarn(), v)),
            getCrit);
        return new ThresholdRow(label, unit, warn, crit);
    }

    private FieldRow Row(
        string label, string unit, float min, float max,
        Func<float> get, Action<float> set, string? note = null) =>
        new(label, unit,
            Register(new NumericFieldViewModel(get(), min, max, Apply(set)), get), note);

    private Action<float> Apply(Action<float> set) => v =>
    {
        set(v);
        _save();
    };

    private NumericFieldViewModel Register(NumericFieldViewModel field, Func<float> get)
    {
        _fields.Add((field, get));
        return field;
    }
}
```

- [ ] **Step 3: Lisää SettingsPage MainViewModeliin**

`src/HardwareMonitor.App/ViewModels/MainViewModel.cs` — ctoriin rivin
`_timer.Tick += (_, _) => Refresh();` jälkeen:

```csharp
        SettingsPage = new SettingsViewModel(
            _settings, () => OnOverlaySettingChanged(nameof(SettingsPage)));
```

ja propertyn `public OverlayViewModel Overlay { get; } = new();` jälkeen:

```csharp
    /// <summary>Asetukset-välilehden kentät (Vaihe 8.2).</summary>
    public SettingsViewModel SettingsPage { get; }
```

Huom: `OnOverlaySettingChanged` hoitaa tallennuksen SettingsServicellä ja
nostaa OverlaySettingsChanged-tapahtuman (fontti/kulma päivittyvät heti);
raja-arvoille ylimääräinen overlay-päivitys on harmiton.

- [ ] **Step 4: Buildaa**

Run: `dotnet build HardwareMonitor.sln --nologo`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 5: Commit**

```powershell
git add src/HardwareMonitor.App/ViewModels/NumericFieldViewModel.cs src/HardwareMonitor.App/ViewModels/SettingsViewModel.cs src/HardwareMonitor.App/ViewModels/MainViewModel.cs
git commit -m @'
Lisää asetussivun view modelit (NumericField + SettingsViewModel)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 3: Asetukset-välilehti + yläpalkin siivous (XAML)

**Files:**
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (yläpalkin WrapPanel ~rivit 73–115; uusi TabItem ennen `</TabControl>` ~rivi 337)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml.cs` (ResetThresholds_Click)

**Interfaces:**
- Consumes: `SettingsPage.ThresholdRows/DurationRows/LoggingRows/OverlayFontSize/ResetThresholds()` (Task 2); MainViewModelin olemassa olevat propertyt `MinimizeToTray`, `AutoStart`, `AlertNotificationsEnabled`, `OverlayCornerIndex`, `OverlayOpacity`, `OverlayShowCpu/Gpu/Ram/Disks/Fans`; olemassa olevat handlerit `MoveOverlay_Checked/Unchecked`, `CreateReport_Click`, `ExportCsv_Click`.
- Produces: valmis UI.

- [ ] **Step 1: Korvaa yläpalkin WrapPanel**

`MainWindow.xaml`: korvaa koko `<WrapPanel Margin="0,10,0,0">…</WrapPanel>` tällä:

```xml
                <WrapPanel Margin="0,10,0,0">
                    <CheckBox Content="Overlay työpöydälle" IsChecked="{Binding OverlayEnabled}"
                              Foreground="White" VerticalAlignment="Center" />
                    <CheckBox x:Name="MoveOverlayCheck" Content="Siirrä overlayta"
                              IsEnabled="{Binding OverlayEnabled}"
                              Checked="MoveOverlay_Checked" Unchecked="MoveOverlay_Unchecked"
                              Foreground="White" Margin="16,0,0,0" VerticalAlignment="Center"
                              ToolTip="Kun päällä, overlayn voi raahata hiirellä haluamaansa paikkaan" />
                    <Button Content="Luo raportti…" Click="CreateReport_Click"
                            Margin="24,0,0,0" Padding="10,3" VerticalAlignment="Center"
                            ToolTip="Selkokielinen yhteenveto koneen kunnosta tekstitiedostona" />
                    <Button Content="Vie CSV…" Click="ExportCsv_Click"
                            Margin="8,0,0,0" Padding="10,3" VerticalAlignment="Center"
                            ToolTip="Viimeisen 24 tunnin sensorihistoria Exceliin sopivana CSV:nä" />
                </WrapPanel>
```

- [ ] **Step 2: Lisää Asetukset-TabItem**

`MainWindow.xaml`: lisää ennen `</TabControl>`-riviä:

```xml
            <TabItem Header="Asetukset">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel Margin="16" MaxWidth="780" HorizontalAlignment="Left">
                        <StackPanel.Resources>
                            <Style TargetType="GroupBox">
                                <Setter Property="Foreground" Value="#4FC3F7" />
                                <Setter Property="Margin" Value="0,0,0,16" />
                                <Setter Property="Padding" Value="12" />
                            </Style>
                            <Style x:Key="NumField" TargetType="TextBox">
                                <Setter Property="Width" Value="70" />
                                <Setter Property="Margin" Value="0,0,8,0" />
                                <Setter Property="Padding" Value="4,2" />
                                <Setter Property="Background" Value="#2D2D30" />
                                <Setter Property="Foreground" Value="#E0E0E0" />
                                <Setter Property="BorderBrush" Value="#3F3F46" />
                                <Setter Property="VerticalAlignment" Value="Center" />
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding HasError}" Value="True">
                                        <Setter Property="BorderBrush" Value="#F44336" />
                                        <Setter Property="BorderThickness" Value="2" />
                                        <Setter Property="ToolTip" Value="{Binding Error}" />
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                            <Style x:Key="ErrorText" TargetType="TextBlock">
                                <Setter Property="Foreground" Value="#F44336" />
                                <Setter Property="TextWrapping" Value="Wrap" />
                                <Style.Triggers>
                                    <Trigger Property="Text" Value="{x:Null}">
                                        <Setter Property="Visibility" Value="Collapsed" />
                                    </Trigger>
                                    <Trigger Property="Text" Value="">
                                        <Setter Property="Visibility" Value="Collapsed" />
                                    </Trigger>
                                </Style.Triggers>
                            </Style>
                            <Style x:Key="NoteText" TargetType="TextBlock">
                                <Setter Property="Foreground" Value="#9E9E9E" />
                                <Setter Property="FontStyle" Value="Italic" />
                                <Style.Triggers>
                                    <Trigger Property="Text" Value="{x:Null}">
                                        <Setter Property="Visibility" Value="Collapsed" />
                                    </Trigger>
                                    <Trigger Property="Text" Value="">
                                        <Setter Property="Visibility" Value="Collapsed" />
                                    </Trigger>
                                </Style.Triggers>
                            </Style>
                            <DataTemplate x:Key="ThresholdRowTemplate">
                                <Grid Margin="0,3">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="190" />
                                        <ColumnDefinition Width="80" />
                                        <ColumnDefinition Width="80" />
                                        <ColumnDefinition Width="40" />
                                        <ColumnDefinition Width="*" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Text="{Binding Label}" Foreground="#E0E0E0"
                                               VerticalAlignment="Center" />
                                    <TextBox Grid.Column="1" Style="{StaticResource NumField}"
                                             DataContext="{Binding Warn}" Text="{Binding Text}" />
                                    <TextBox Grid.Column="2" Style="{StaticResource NumField}"
                                             DataContext="{Binding Crit}" Text="{Binding Text}" />
                                    <TextBlock Grid.Column="3" Text="{Binding Unit}" Foreground="#9E9E9E"
                                               VerticalAlignment="Center" />
                                    <StackPanel Grid.Column="4" VerticalAlignment="Center" Margin="8,0,0,0">
                                        <TextBlock Style="{StaticResource ErrorText}" Text="{Binding Warn.Error}" />
                                        <TextBlock Style="{StaticResource ErrorText}" Text="{Binding Crit.Error}" />
                                    </StackPanel>
                                </Grid>
                            </DataTemplate>
                            <DataTemplate x:Key="FieldRowTemplate">
                                <Grid Margin="0,3">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="270" />
                                        <ColumnDefinition Width="80" />
                                        <ColumnDefinition Width="40" />
                                        <ColumnDefinition Width="*" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Text="{Binding Label}" Foreground="#E0E0E0"
                                               VerticalAlignment="Center" />
                                    <TextBox Grid.Column="1" Style="{StaticResource NumField}"
                                             DataContext="{Binding Field}" Text="{Binding Text}" />
                                    <TextBlock Grid.Column="2" Text="{Binding Unit}" Foreground="#9E9E9E"
                                               VerticalAlignment="Center" />
                                    <StackPanel Grid.Column="3" VerticalAlignment="Center" Margin="8,0,0,0">
                                        <TextBlock Style="{StaticResource ErrorText}" Text="{Binding Field.Error}" />
                                        <TextBlock Style="{StaticResource NoteText}" Text="{Binding Note}" />
                                    </StackPanel>
                                </Grid>
                            </DataTemplate>
                        </StackPanel.Resources>

                        <GroupBox Header="Yleiset">
                            <StackPanel>
                                <CheckBox Content="Pienennä trayhin" IsChecked="{Binding MinimizeToTray}"
                                          Foreground="White" Margin="0,2" />
                                <CheckBox Content="Käynnistä Windowsin mukana" IsChecked="{Binding AutoStart}"
                                          Foreground="White" Margin="0,2" />
                                <CheckBox Content="Hälytysilmoitukset" IsChecked="{Binding AlertNotificationsEnabled}"
                                          Foreground="White" Margin="0,2"
                                          ToolTip="Näytä ilmoitus ilmaisinalueella, kun varoitus- tai kriittinen raja ylittyy" />
                            </StackPanel>
                        </GroupBox>

                        <GroupBox Header="Raja-arvot">
                            <StackPanel>
                                <Grid Margin="0,0,0,4">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="190" />
                                        <ColumnDefinition Width="80" />
                                        <ColumnDefinition Width="80" />
                                        <ColumnDefinition Width="*" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="1" Text="Varoitus" Foreground="#FFB74D"
                                               FontWeight="SemiBold" />
                                    <TextBlock Grid.Column="2" Text="Kriittinen" Foreground="#F44336"
                                               FontWeight="SemiBold" />
                                </Grid>
                                <ItemsControl ItemsSource="{Binding SettingsPage.ThresholdRows}"
                                              ItemTemplate="{StaticResource ThresholdRowTemplate}" />
                                <Button Content="Palauta oletusrajat" Click="ResetThresholds_Click"
                                        HorizontalAlignment="Left" Margin="0,8,0,0" Padding="10,3"
                                        ToolTip="Palauttaa raja-arvot ja kestot ohjelman oletuksiin" />
                            </StackPanel>
                        </GroupBox>

                        <GroupBox Header="Kestot">
                            <ItemsControl ItemsSource="{Binding SettingsPage.DurationRows}"
                                          ItemTemplate="{StaticResource FieldRowTemplate}" />
                        </GroupBox>

                        <GroupBox Header="Lokitus">
                            <ItemsControl ItemsSource="{Binding SettingsPage.LoggingRows}"
                                          ItemTemplate="{StaticResource FieldRowTemplate}" />
                        </GroupBox>

                        <GroupBox Header="Overlay">
                            <StackPanel>
                                <StackPanel Orientation="Horizontal" Margin="0,2">
                                    <TextBlock Text="Kulma" Foreground="#E0E0E0" Width="120"
                                               VerticalAlignment="Center" />
                                    <ComboBox SelectedIndex="{Binding OverlayCornerIndex}" Width="110"
                                              VerticalAlignment="Center">
                                        <ComboBoxItem Content="Vasen ylä" />
                                        <ComboBoxItem Content="Oikea ylä" />
                                        <ComboBoxItem Content="Vasen ala" />
                                        <ComboBoxItem Content="Oikea ala" />
                                    </ComboBox>
                                </StackPanel>
                                <StackPanel Orientation="Horizontal" Margin="0,2">
                                    <TextBlock Text="Läpinäkyvyys" Foreground="#E0E0E0" Width="120"
                                               VerticalAlignment="Center" />
                                    <Slider Value="{Binding OverlayOpacity}" Minimum="0.3" Maximum="1.0"
                                            Width="140" VerticalAlignment="Center" />
                                </StackPanel>
                                <StackPanel Orientation="Horizontal" Margin="0,2">
                                    <TextBlock Text="Fonttikoko" Foreground="#E0E0E0" Width="120"
                                               VerticalAlignment="Center" />
                                    <TextBox Style="{StaticResource NumField}"
                                             DataContext="{Binding SettingsPage.OverlayFontSize}"
                                             Text="{Binding Text}" />
                                    <TextBlock Style="{StaticResource ErrorText}"
                                               Text="{Binding SettingsPage.OverlayFontSize.Error}"
                                               VerticalAlignment="Center" Margin="8,0,0,0" />
                                </StackPanel>
                                <TextBlock Text="Näytettävät mittarit" Foreground="#E0E0E0"
                                           Margin="0,8,0,2" />
                                <WrapPanel>
                                    <CheckBox Content="CPU" IsChecked="{Binding OverlayShowCpu}"
                                              Foreground="White" Margin="0,0,12,0" />
                                    <CheckBox Content="GPU" IsChecked="{Binding OverlayShowGpu}"
                                              Foreground="White" Margin="0,0,12,0" />
                                    <CheckBox Content="RAM" IsChecked="{Binding OverlayShowRam}"
                                              Foreground="White" Margin="0,0,12,0" />
                                    <CheckBox Content="Levyt" IsChecked="{Binding OverlayShowDisks}"
                                              Foreground="White" Margin="0,0,12,0" />
                                    <CheckBox Content="Tuulettimet" IsChecked="{Binding OverlayShowFans}"
                                              Foreground="White" Margin="0,0,12,0" />
                                </WrapPanel>
                            </StackPanel>
                        </GroupBox>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>
```

HUOM: virhe-TextBlockin `Text`-sidonta OverlayFontSize-rivillä osoittaa
MainViewModeliin (StackPanelin DataContext), TextBoxin DataContext taas
vaihdettu kenttä-VM:ään — molemmat ovat oikein.

- [ ] **Step 3: Lisää ResetThresholds_Click**

`src/HardwareMonitor.App/MainWindow.xaml.cs`, metodin `RestoreFromTray()` jälkeen:

```csharp
    private void ResetThresholds_Click(object sender, RoutedEventArgs e) =>
        _viewModel.SettingsPage.ResetThresholds();
```

(`RoutedEventArgs` tulee jo käytössä olevasta `System.Windows`-usingista.)

- [ ] **Step 4: Buildaa ja aja testit**

Run: `dotnet build HardwareMonitor.sln --nologo`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).
Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --nologo`
Expected: PASS, 116 testiä.

- [ ] **Step 5: Commit**

```powershell
git add src/HardwareMonitor.App/MainWindow.xaml src/HardwareMonitor.App/MainWindow.xaml.cs
git commit -m @'
Lisää Asetukset-välilehti ja siivoa yläpalkki (Vaihe 8.2)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 4: Ajonaikainen todennus + HANDOFF + push

**Files:**
- Modify: `HANDOFF.md` (kohta 2 tehdyksi)

**Interfaces:**
- Consumes: valmis sovellus (Task 1–3).
- Produces: todennettu ominaisuus, pushattu haara.

- [ ] **Step 1: Käynnistä ja todenna**

Varmista ettei HardwareMonitor.exe ole ajossa (`Get-Process HardwareMonitor`),
sitten `.\run.ps1 -AsAdmin` (käyttäjä hyväksyy UAC:n). Tarkista
kuvakaappauksin (CopyFromScreen — FindWindow ei näe korotettua ikkunaa):

1. Asetukset-välilehti näkyy ja kentissä on nykyiset arvot (85, 95, …).
2. Yläpalkissa vain: Overlay työpöydälle, Siirrä overlayta, Luo raportti…, Vie CSV….
3. Muuta CPU-varoitusraja 30:ksi → Dashboardin tilapaneeli näyttää
   Varoitusta parin sekunnin sisällä ILMAN uudelleenkäynnistystä
   (huom: 10 s kuluttua syntyy myös tapahtuma + tray-ilmoitus — odotettua).
   Palauta 85 → tila palautuu.
4. Syötä CPU-varoitukseen "abc" → punainen reunus + "Anna numero";
   settings.json ei muutu (tarkista aikaleima). Syötä 96 (kriittinen 95)
   → "Varoitusrajan on oltava pienempi kuin kriittisen rajan".
5. Fonttikoko 20 → overlay kasvaa heti. Palauta 14.
6. Palauta oletusrajat -nappi: muuta ensin jotain arvoa, paina nappia →
   kentät ja settings.json palautuvat oletuksiin.
7. Sulje siististi (tray → Lopeta, käyttäjä tekee) ja käynnistä uudelleen →
   muutetut arvot säilyivät.

- [ ] **Step 2: Päivitä HANDOFF.md**

Merkitse "Seuraavat askeleet" -listan kohta 2 (Asetussivu) tehdyksi samaan
tapaan kuin kohta 1: lyhyt kuvaus toteutuksesta (SettingsValidator,
NumericFieldViewModel/SettingsViewModel, Asetukset-välilehti, yläpalkin
siivous) ja todennuksesta.

- [ ] **Step 3: Commitoi ja pushaa**

```powershell
git add HANDOFF.md
git commit -m @'
Päivitä HANDOFF: asetussivu valmis

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
git push
```
