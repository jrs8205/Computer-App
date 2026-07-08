# Tuulettimien nimilaput + tray + ikoni — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tuulettimien nimeäminen Dashboard-kortissa (näkyy myös overlayssa), pienennys ilmaisinalueelle asetuksena, ja vektori-ikoni sovellukselle.

**Architecture:** `FanMetrics` saa pysyvän `Identifier`-kentän; `AppSettings.FanLabels`-sanakirja (tunniste → nimi) sovitetaan näyttönimiin VM-kerroksessa. Tray toteutetaan WinForms `NotifyIcon`illa (ei NuGet-riippuvuuksia). Ikonin master on käsin kirjoitettu SVG, josta PowerShell-skripti generoi moniresoluutio-icon.

**Tech Stack:** .NET 8 WPF + WinForms NotifyIcon, System.Text.Json, xUnit, GDI+ (ikonigenerointi).

## Global Constraints

- TargetFramework `net8.0-windows`; file-scoped namespace, `sealed`, suomenkieliset XML-doc-kommentit.
- Ei uusia NuGet-paketteja (WinForms tulee `<UseWindowsForms>true</UseWindowsForms>`-lipulla).
- **UI-muutosten jälkeen aina `dotnet build HardwareMonitor.sln`** — `dotnet test` ei buildaa App-projektia. Pysäytä käynnissä oleva HardwareMonitor.exe ennen buildia.
- Commit-viestit suomeksi + `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Spec: `docs/superpowers/specs/2026-07-08-fan-labels-tray-icon-design.md`.

---

### Task 1: FanMetrics.Identifier + asetuskentät (TDD)

**Files:**
- Modify: `src/HardwareMonitor.Core/Metrics/KeyMetrics.cs` (FanMetrics-record)
- Modify: `src/HardwareMonitor.Core/Metrics/KeyMetricsService.cs` (CollectFans)
- Modify: `src/HardwareMonitor.Core/Settings/AppSettings.cs` (FanLabels, MinimizeToTray)
- Test: `src/HardwareMonitor.Tests/Metrics/KeyMetricsServiceTests.cs`, `src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `FanMetrics(string Name, float? Rpm, string Identifier)`;
  `AppSettings.FanLabels : Dictionary<string,string>` (oletus tyhjä);
  `AppSettings.MinimizeToTray : bool` (oletus true). Taskit 2 ja 4 käyttävät näitä.

- [ ] **Step 1: Epäonnistuvat testit.** `KeyMetricsServiceTests.cs`iin uusi testi ja
  tuuletintestin laajennus; `SettingsServiceTests.cs`iin asetustesti:

```csharp
    // KeyMetricsServiceTests.cs — uusi testi
    [Fact]
    public void Extract_TuulettimillaOnPysyvaTunniste()
    {
        var gpu = Group("RTX 2060", "GpuNvidia", new[]
        {
            Reading("gpu", "GpuNvidia", "GPU Fan 1", "Fan", 1200f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { gpu });

        Assert.Equal("/gpunvidia/gpu/fan/gpu fan 1", m.Fans[0].Identifier);
    }
```

```csharp
    // SettingsServiceTests.cs — uusi testi
    [Fact]
    public void FanLabelsJaMinimizeToTray_OletuksetJaTallennus()
    {
        var service = new SettingsService(_dir);

        AppSettings defaults = service.Load();
        Assert.Empty(defaults.FanLabels);
        Assert.True(defaults.MinimizeToTray);

        defaults.FanLabels["/lpc/nct6798d/0/fan/2"] = "AIO-pumppu";
        defaults.MinimizeToTray = false;
        service.Save(defaults);

        AppSettings loaded = new SettingsService(_dir).Load();
        Assert.Equal("AIO-pumppu", loaded.FanLabels["/lpc/nct6798d/0/fan/2"]);
        Assert.False(loaded.MinimizeToTray);
    }
```

- [ ] **Step 2: Aja** `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → FAIL (Identifier/FanLabels puuttuvat).

- [ ] **Step 3: Toteutus.** `KeyMetrics.cs`: `public sealed record FanMetrics(string Name, float? Rpm, string Identifier);`
  `KeyMetricsService.CollectFans`: `fans.Add(new FanMetrics(s.SensorName, s.Value, s.Identifier));`
  `AppSettings.cs`iin:

```csharp
    /// <summary>Käyttäjän omat nimet tuulettimille: sensorin Identifier -> nimi.</summary>
    public Dictionary<string, string> FanLabels { get; set; } = new();

    /// <summary>Pienennä- ja sulje-nappi vievät ilmaisinalueelle; mittaus jatkuu taustalla.</summary>
    public bool MinimizeToTray { get; set; } = true;
```

- [ ] **Step 4: Aja testit** → PASS (14 testiä).
- [ ] **Step 5: Commit** `git add src/HardwareMonitor.Core src/HardwareMonitor.Tests && git commit -m "FanMetrics.Identifier + FanLabels/MinimizeToTray-asetukset (TDD)"`

---

### Task 2: Nimilaput Dashboard-korttiin ja overlayhin

**Files:**
- Create: `src/HardwareMonitor.App/ViewModels/FanRowViewModel.cs`
- Modify: `src/HardwareMonitor.App/ViewModels/DashboardViewModel.cs` (Fans-kokoelma, Update-signatuuri)
- Modify: `src/HardwareMonitor.App/ViewModels/OverlayViewModel.cs` (Update saa AppSettings + labelit)
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (RenameFan, kutsujen päivitys)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (tuuletinrivin muokkaus-template)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml.cs` (tuplaklikkaus/Enter/Esc-käsittelijät)

**Interfaces:**
- Consumes: `FanMetrics.Identifier`, `AppSettings.FanLabels` (Task 1).
- Produces: `FanRowViewModel { string Identifier; string DisplayName; string Rpm; bool IsEditing; string EditText; void BeginEdit(); void CommitEdit(); void CancelEdit(); }`
  (CommitEdit kutsuu konstruktorissa annettua `Action<string,string> rename`-callbackia);
  `DashboardViewModel.Update(KeyMetrics m, IReadOnlyDictionary<string,string> fanLabels)`;
  `OverlayViewModel.Update(KeyMetrics m, AppSettings settings)`;
  `MainViewModel.RenameFan(string identifier, string newName)` (tyhjä nimi poistaa lapun).

- [ ] **Step 1: FanRowViewModel** (uusi tiedosto, INPC; DisplayName = label ?? oletusnimi):

```csharp
using System.ComponentModel;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Yksi tuuletinrivi Dashboardin Tuulettimet-kortissa. Nimen voi vaihtaa
/// kaksoisklikkaamalla (IsEditing); tyhjä nimi palauttaa oletuksen.
/// </summary>
public sealed class FanRowViewModel : INotifyPropertyChanged
{
    private readonly Action<string, string> _rename;
    private string _displayName;
    private string _rpm = "—";
    private bool _isEditing;
    private string _editText = "";

    public FanRowViewModel(string identifier, string displayName, Action<string, string> rename)
    {
        Identifier = identifier;
        _displayName = displayName;
        _rename = rename;
    }

    public string Identifier { get; }

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName != value) { _displayName = value; Notify(nameof(DisplayName)); } }
    }

    public string Rpm
    {
        get => _rpm;
        set { if (_rpm != value) { _rpm = value; Notify(nameof(Rpm)); } }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set { if (_isEditing != value) { _isEditing = value; Notify(nameof(IsEditing)); } }
    }

    public string EditText
    {
        get => _editText;
        set { if (_editText != value) { _editText = value; Notify(nameof(EditText)); } }
    }

    public void BeginEdit()
    {
        EditText = DisplayName;
        IsEditing = true;
    }

    public void CommitEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
        _rename(Identifier, EditText.Trim());
    }

    public void CancelEdit() => IsEditing = false;

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

- [ ] **Step 2: DashboardViewModel**: `Fans`-kokoelma tyypiksi `ObservableCollection<FanRowViewModel>`,
  `Update(KeyMetrics m, IReadOnlyDictionary<string, string> fanLabels)`; synkkaus tunnisteella:

```csharp
    public ObservableCollection<FanRowViewModel> Fans { get; } = new();

    /// <summary>Kutsutaan MainViewModelista; callback välittää nimenmuutokset takaisin.</summary>
    public Action<string, string>? RenameFan { get; set; }

    private void SyncFans(IReadOnlyList<FanMetrics> fans, IReadOnlyDictionary<string, string> labels)
    {
        while (Fans.Count > fans.Count)
        {
            Fans.RemoveAt(Fans.Count - 1);
        }

        for (int i = 0; i < fans.Count; i++)
        {
            FanMetrics fan = fans[i];
            string name = labels.TryGetValue(fan.Identifier, out string? label) && label.Length > 0
                ? label
                : fan.Name;
            string rpm = Fmt(fan.Rpm, "RPM");

            if (i >= Fans.Count || Fans[i].Identifier != fan.Identifier)
            {
                var row = new FanRowViewModel(fan.Identifier, name, (id, n) => RenameFan?.Invoke(id, n));
                row.Rpm = rpm;
                if (i >= Fans.Count) { Fans.Add(row); } else { Fans[i] = row; }
            }
            else
            {
                if (!Fans[i].IsEditing) { Fans[i].DisplayName = name; }
                Fans[i].Rpm = rpm;
            }
        }
    }
```

  `Update`-metodissa `SyncRows(Fans, ...)`-rivi korvataan kutsulla `SyncFans(m.Fans, fanLabels);`.

- [ ] **Step 3: OverlayViewModel.Update(KeyMetrics m, AppSettings settings)** — sisällä
  `OverlaySettings s = settings.Overlay;` ja tuuletinsilmukka käyttää labelia:

```csharp
        if (s.ShowFans)
        {
            foreach (FanMetrics fan in m.Fans)
            {
                if (fan.Rpm is { } rpm and > 0)
                {
                    string name = settings.FanLabels.TryGetValue(fan.Identifier, out string? label)
                                  && label.Length > 0 ? label : fan.Name;
                    sb.AppendLine($"{name}  {rpm:0} RPM");
                }
            }
        }
```

- [ ] **Step 4: MainViewModel**: `Refresh()`-kutsut muotoon
  `Dashboard.Update(metrics, _settings.FanLabels); Overlay.Update(metrics, _settings);`
  Konstruktoriin `Dashboard.RenameFan = RenameFan;` ja uusi metodi:

```csharp
    /// <summary>Tallentaa tuulettimen oman nimen; tyhjä nimi palauttaa oletuksen.</summary>
    public void RenameFan(string identifier, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            _settings.FanLabels.Remove(identifier);
        }
        else
        {
            _settings.FanLabels[identifier] = newName;
        }

        OnOverlaySettingChanged(nameof(Dashboard));
    }
```

- [ ] **Step 5: XAML**: Tuulettimet-kortin ItemsControlin ItemTemplate:

```xml
<ItemsControl ItemsSource="{Binding Fans}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="0,2">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <TextBlock Text="{Binding DisplayName}" Foreground="#E0E0E0"
                           FontFamily="Consolas" ToolTip="Kaksoisklikkaa nimetäksesi"
                           MouseLeftButtonDown="FanName_MouseLeftButtonDown">
                    <TextBlock.Style>
                        <Style TargetType="TextBlock">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsEditing}" Value="True">
                                    <Setter Property="Visibility" Value="Collapsed" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBlock.Style>
                </TextBlock>
                <TextBox Text="{Binding EditText, UpdateSourceTrigger=PropertyChanged}"
                         Background="#1E1E1E" Foreground="White" FontFamily="Consolas"
                         KeyDown="FanName_KeyDown" LostFocus="FanName_LostFocus"
                         IsVisibleChanged="FanName_IsVisibleChanged" Visibility="Collapsed">
                    <TextBox.Style>
                        <Style TargetType="TextBox">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsEditing}" Value="True">
                                    <Setter Property="Visibility" Value="Visible" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBox.Style>
                </TextBox>
                <TextBlock Grid.Column="1" Text="{Binding Rpm}" Foreground="#A5D6A7"
                           FontFamily="Consolas" Margin="16,0,0,0" />
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

- [ ] **Step 6: Code-behind-käsittelijät** (MainWindow.xaml.cs):

```csharp
    private void FanName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: FanRowViewModel row })
        {
            row.BeginEdit();
        }
    }

    private void FanName_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FanRowViewModel row })
        {
            return;
        }

        if (e.Key == Key.Enter) { row.CommitEdit(); }
        else if (e.Key == Key.Escape) { row.CancelEdit(); }
    }

    private void FanName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FanRowViewModel row })
        {
            row.CommitEdit();
        }
    }

    private void FanName_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && (bool)e.NewValue)
        {
            box.Dispatcher.BeginInvoke(() => { box.Focus(); box.SelectAll(); });
        }
    }
```

  Usingit: `System.Windows.Input`.

- [ ] **Step 7: Buildaa** `dotnet build HardwareMonitor.sln` (pysäytä appi ensin) → PASS. Testit → PASS.
- [ ] **Step 8: Commit** `git add src/HardwareMonitor.App && git commit -m "Tuulettimien nimilaput Dashboardiin ja overlayhin"`

---

### Task 3: Vektori-ikoni (SVG + generoitu app.ico)

**Files:**
- Create: `src/HardwareMonitor.App/Assets/icon.svg` (master-vektori)
- Create: `tools/generate-icon.ps1` (GDI+-generointi)
- Create: `src/HardwareMonitor.App/Assets/app.ico` (generoitu, commitoidaan)
- Modify: `src/HardwareMonitor.App/HardwareMonitor.App.csproj` (`ApplicationIcon` + Resource)

**Interfaces:**
- Produces: `Assets/app.ico` Resource-itemina — Task 4:n NotifyIcon lataa sen
  URIsta `pack://application:,,,/Assets/app.ico`.

- [ ] **Step 1: icon.svg** — tumma pyöristetty neliö, syaani mittarikaari + neula, vihreä sykeviiva:

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#2A2A2E"/>
      <stop offset="1" stop-color="#161618"/>
    </linearGradient>
  </defs>
  <rect x="8" y="8" width="240" height="240" rx="52" fill="url(#bg)"/>
  <!-- Mittarikaari 180°..360° -->
  <path d="M 50 150 A 78 78 0 0 1 206 150" fill="none"
        stroke="#4FC3F7" stroke-width="18" stroke-linecap="round"/>
  <!-- Neula ~70 % (45° oikealle ylös) -->
  <line x1="128" y1="150" x2="180" y2="98" stroke="#E8F5E9"
        stroke-width="11" stroke-linecap="round"/>
  <circle cx="128" cy="150" r="13" fill="#4FC3F7"/>
  <!-- Sykeviiva -->
  <polyline points="48,206 88,206 104,184 122,224 138,206 208,206" fill="none"
            stroke="#A5D6A7" stroke-width="11"
            stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

- [ ] **Step 2: tools/generate-icon.ps1** — piirtää saman GDI+:lla koissa 16–256 ja kokoaa
  PNG-pakatun ICO:n (ICONDIR + ICONDIRENTRYt + PNG-datat):

```powershell
# generate-icon.ps1 — generoi src/HardwareMonitor.App/Assets/app.ico icon.svg:n designista.
# Ajo repon juuressa:  .\tools\generate-icon.ps1
Add-Type -AssemblyName System.Drawing

function Draw-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $s = $size / 256.0

    # Tausta: pyöristetty neliö pystygradientilla
    $rect = New-Object System.Drawing.Rectangle ([int](8*$s)), ([int](8*$s)), ([int](240*$s)), ([int](240*$s))
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.ColorTranslator]::FromHtml('#2A2A2E')),
        ([System.Drawing.ColorTranslator]::FromHtml('#161618')), 90
    $r = 52 * $s
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, 2*$r, 2*$r, 180, 90)
    $path.AddArc($rect.Right - 2*$r, $rect.Y, 2*$r, 2*$r, 270, 90)
    $path.AddArc($rect.Right - 2*$r, $rect.Bottom - 2*$r, 2*$r, 2*$r, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - 2*$r, 2*$r, 2*$r, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)

    # Mittarikaari (180°..360°)
    $penArc = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#4FC3F7')), (18*$s)
    $penArc.StartCap = 'Round'; $penArc.EndCap = 'Round'
    $g.DrawArc($penArc, [float](50*$s), [float](72*$s), [float](156*$s), [float](156*$s), 180, 180)

    # Neula + napa
    $penNeedle = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#E8F5E9')), (11*$s)
    $penNeedle.StartCap = 'Round'; $penNeedle.EndCap = 'Round'
    $g.DrawLine($penNeedle, [float](128*$s), [float](150*$s), [float](180*$s), [float](98*$s))
    $hub = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#4FC3F7'))
    $g.FillEllipse($hub, [float]((128-13)*$s), [float]((150-13)*$s), [float](26*$s), [float](26*$s))

    # Sykeviiva
    $penPulse = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#A5D6A7')), (11*$s)
    $penPulse.StartCap = 'Round'; $penPulse.EndCap = 'Round'; $penPulse.LineJoin = 'Round'
    $pts = @(48,206, 88,206, 104,184, 122,224, 138,206, 208,206)
    $points = for ($i = 0; $i -lt $pts.Count; $i += 2) {
        New-Object System.Drawing.PointF ([float]($pts[$i]*$s)), ([float]($pts[$i+1]*$s))
    }
    $g.DrawLines($penPulse, $points)

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) { ,(Draw-IconPng $size) }

# ICO-kontti: ICONDIR (6 t) + ICONDIRENTRY (16 t/kuva) + PNG-datat
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$icoPath = Join-Path $PSScriptRoot "..\src\HardwareMonitor.App\Assets\app.ico"
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $icoPath) | Join-Path -ChildPath "app.ico"), $out.ToArray())
Write-Host "app.ico generoitu: $($out.Length) tavua, koot: $($sizes -join ', ')"
```

- [ ] **Step 3: Aja** `.\tools\generate-icon.ps1` → app.ico syntyy. Tarkista silmämääräisesti (avaa Explorerissa / lue PNG-koko).

- [ ] **Step 4: csproj**:

```xml
  <PropertyGroup>
    <ApplicationIcon>Assets\app.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <Resource Include="Assets\app.ico" />
  </ItemGroup>
```

- [ ] **Step 5: Buildaa + commit** `git add src/HardwareMonitor.App tools && git commit -m "Lisää sovellusikoni (SVG-master + generoitu app.ico)"`

---

### Task 4: Tray-pienennys

**Files:**
- Modify: `src/HardwareMonitor.App/HardwareMonitor.App.csproj` (`<UseWindowsForms>true</UseWindowsForms>`)
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (MinimizeToTray-property)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (CheckBox "Pienennä trayhin")
- Modify: `src/HardwareMonitor.App/MainWindow.xaml.cs` (NotifyIcon, StateChanged, Closing)
- Modify: `docs/ROADMAP.md` (nimilaput+tray+ikoni valmiiksi)

**Interfaces:**
- Consumes: `AppSettings.MinimizeToTray` (Task 1), `Assets/app.ico` Resource (Task 3),
  `MainViewModel.OverlayEnabled` (olemassa).
- Produces: valmis tray-käyttäytyminen; ei uusia julkisia rajapintoja.

- [ ] **Step 1: csproj** — PropertyGroupiin `<UseWindowsForms>true</UseWindowsForms>`.

- [ ] **Step 2: MainViewModel.MinimizeToTray** (sama kaava kuin OverlayEnabled):

```csharp
    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray == value)
            {
                return;
            }

            _settings.MinimizeToTray = value;
            OnOverlaySettingChanged(nameof(MinimizeToTray));
        }
    }
```

- [ ] **Step 3: XAML** — yläpalkin WrapPaneliin viimeiseksi:

```xml
                    <CheckBox Content="Pienennä trayhin" IsChecked="{Binding MinimizeToTray}"
                              Foreground="White" Margin="16,0,0,0" VerticalAlignment="Center" />
```

- [ ] **Step 4: MainWindow.xaml.cs** — NotifyIcon-kenttä, luonti konstruktorissa, käyttäytyminen:

```csharp
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _reallyExiting;

    // Konstruktoriin InitializeComponentin jälkeen:
    CreateTrayIcon();

    // StateChanged: pienennys -> piiloon kun asetus päällä
    StateChanged += (_, _) =>
    {
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
        {
            Hide();
        }
    };

    // Closing: X -> trayhin kun asetus päällä (paitsi oikeasti lopetettaessa)
    Closing += (_, e) =>
    {
        if (_viewModel.MinimizeToTray && !_reallyExiting)
        {
            e.Cancel = true;
            Hide();
        }
    };

    // Closed-käsittelijään lisäksi: _trayIcon?.Dispose();

    private void CreateTrayIcon()
    {
        var iconStream = System.Windows.Application
            .GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Text = "Hardware Monitor",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Näytä", null, (_, _) => RestoreFromTray());
        var overlayItem = new System.Windows.Forms.ToolStripMenuItem("Overlay")
        {
            CheckOnClick = true,
            Checked = _viewModel.OverlayEnabled,
        };
        overlayItem.CheckedChanged += (_, _) => _viewModel.OverlayEnabled = overlayItem.Checked;
        menu.Items.Add(overlayItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Lopeta", null, (_, _) => { _reallyExiting = true; Close(); });
        menu.Opening += (_, _) => overlayItem.Checked = _viewModel.OverlayEnabled;
        _trayIcon.ContextMenuStrip = menu;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
```

- [ ] **Step 5: Buildaa, aja testit, todenna käsin** (pienennä → tray; X → tray; tuplaklikkaus palauttaa; Lopeta sulkee; ikoni näkyy). Ruutukaappaus.
- [ ] **Step 6: ROADMAP-päivitys + commit** `git add src/HardwareMonitor.App docs/ROADMAP.md && git commit -m "Tray-pienennys ja tray-valikko (Näytä/Overlay/Lopeta)"`
