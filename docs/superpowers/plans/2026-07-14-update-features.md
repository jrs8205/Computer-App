# Päivitysominaisuudet — toteutussuunnitelma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sovellus ilmoittaa omista päivityksistään (GitHub Releases + tray-ilmoitus + yhden klikkauksen asennus allekirjoitustarkistuksella) ja uusi Ylläpito-välilehti näyttää laitteiden nykyversiot (WMI) sekä valmistajien tukisivulinkit.

**Architecture:** Core saa vain puhtaita TDD-luokkia (UpdateChecker, VendorLinkResolver, NvidiaDriverVersion) — ei verkkoa eikä WMI:tä. App hoitaa I/O:n (UpdateService/HTTP, DeviceVersionReader/WMI, UpdateInstaller/lataus+Authenticode) ja UI:n (UpdateDialog, Ylläpito-TabItem). Spec: `docs/superpowers/specs/2026-07-14-update-features-design.md`.

**Tech Stack:** C#/.NET 8, WPF, xUnit, System.Text.Json (Core), HttpClient + System.Management 8.0.0 (vain App), WinVerifyTrust (wintrust.dll).

## Global Constraints

- Koodikommentit ja testinimet SUOMEKSI; tekniset termit englanniksi lauseen sisällä.
- Commit-viesteissä EI `Co-Authored-By`-riviä. Push lopuksi: `git push origin HEAD:claude/windows-11-program-setup-rxuyhn HEAD:main`.
- `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` EI buildaa App-projektia — UI-muutosten jälkeen aja aina `dotnet build HardwareMonitor.sln`. Käynnissä oleva repo-exe lukitsee DLL:t (asennettu exe ei).
- Olemassa olevat 226 testiä pysyvät vihreinä joka taskin jälkeen.
- Lokalisointi: jokainen uusi UI-teksti molempiin resx:iin (`src/HardwareMonitor.App/Localization/UiStrings.resx` = fi/neutraali, `UiStrings.en.resx` = en) + käsin kirjoitettu accessor `UiStrings.cs`:ään. `EnglishResourceTests` valvoo en-kattavuutta.
- WCAG AAA (kontrasti ≥ 7:1) UI-teksteille: käytä olemassa olevaa palettia (#FFFFFF, #E0E0E0, #BDBDBD, #4FC3F7, #A5D6A7, #FF8A80 tummalla #1E1E1E/#252526-taustalla).
- Core-projektiin EI saa lisätä System.Management- eikä HTTP-riippuvuuksia.
- Julkaisuvarmenteen thumbprint: `346D869550F3A7BD54FA947E024341C64F729AF8`.
- Versio päivitetään julkaisussa KAHTEEN paikkaan: `src/HardwareMonitor.App/HardwareMonitor.App.csproj` `<Version>` + `installer/setup.iss` `MyAppVersion`.

---

### Task 1: UpdateInfo + UpdateChecker.ParseLatestRelease (Core, TDD)

**Files:**
- Create: `src/HardwareMonitor.Core/Updates/UpdateInfo.cs`
- Create: `src/HardwareMonitor.Core/Updates/UpdateChecker.cs`
- Test: `src/HardwareMonitor.Tests/Updates/UpdateCheckerTests.cs`

**Interfaces:**
- Produces: `record UpdateInfo(string Version, string ReleaseUrl, string? SetupAssetUrl, string ReleaseNotes)`; `static UpdateInfo? UpdateChecker.ParseLatestRelease(string json)`.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
// src/HardwareMonitor.Tests/Updates/UpdateCheckerTests.cs
using HardwareMonitor.Core.Updates;

namespace HardwareMonitor.Tests.Updates;

public class UpdateCheckerTests
{
    private const string ValidJson = """
        {
          "tag_name": "v1.0.6",
          "html_url": "https://github.com/jrs8205/Computer-App/releases/tag/v1.0.6",
          "body": "Uutta: Ylläpito-välilehti.",
          "assets": [
            { "name": "muu-liite.zip", "browser_download_url": "https://example.test/muu.zip" },
            { "name": "HardwareMonitor-Setup-1.0.6.exe",
              "browser_download_url": "https://github.com/jrs8205/Computer-App/releases/download/v1.0.6/HardwareMonitor-Setup-1.0.6.exe" }
          ]
        }
        """;

    [Fact]
    public void ParseLatestRelease_poimii_version_linkit_ja_muutostekstin()
    {
        UpdateInfo? info = UpdateChecker.ParseLatestRelease(ValidJson);

        Assert.NotNull(info);
        Assert.Equal("1.0.6", info!.Version);
        Assert.Equal("https://github.com/jrs8205/Computer-App/releases/tag/v1.0.6", info.ReleaseUrl);
        Assert.Equal(
            "https://github.com/jrs8205/Computer-App/releases/download/v1.0.6/HardwareMonitor-Setup-1.0.6.exe",
            info.SetupAssetUrl);
        Assert.Equal("Uutta: Ylläpito-välilehti.", info.ReleaseNotes);
    }

    [Fact]
    public void ParseLatestRelease_ilman_setup_liitetta_url_on_null()
    {
        const string json = """
            { "tag_name": "v1.0.6", "html_url": "https://example.test/rel", "body": "x",
              "assets": [ { "name": "muu.zip", "browser_download_url": "https://example.test/muu.zip" } ] }
            """;

        UpdateInfo? info = UpdateChecker.ParseLatestRelease(json);

        Assert.NotNull(info);
        Assert.Null(info!.SetupAssetUrl);
    }

    [Fact]
    public void ParseLatestRelease_puuttuva_body_on_tyhja_teksti()
    {
        const string json = """{ "tag_name": "v1.0.6", "html_url": "https://example.test/rel" }""";

        Assert.Equal("", UpdateChecker.ParseLatestRelease(json)!.ReleaseNotes);
    }

    [Fact]
    public void ParseLatestRelease_kelvoton_vastaus_palauttaa_null()
    {
        Assert.Null(UpdateChecker.ParseLatestRelease("ei jsonia"));
        Assert.Null(UpdateChecker.ParseLatestRelease("{}"));
        Assert.Null(UpdateChecker.ParseLatestRelease("""{ "tag_name": "v" }"""));
    }
}
```

- [ ] **Step 2: Aja testit — odota FAIL**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter UpdateCheckerTests`
Expected: käännösvirhe "UpdateChecker does not exist".

- [ ] **Step 3: Toteuta**

```csharp
// src/HardwareMonitor.Core/Updates/UpdateInfo.cs
namespace HardwareMonitor.Core.Updates;

/// <summary>GitHub-releasen tiedot päivitysilmoitusta varten.</summary>
public sealed record UpdateInfo(
    string Version, string ReleaseUrl, string? SetupAssetUrl, string ReleaseNotes);
```

```csharp
// src/HardwareMonitor.Core/Updates/UpdateChecker.cs
using System.Text.Json;

namespace HardwareMonitor.Core.Updates;

/// <summary>
/// Päivitystarkistuksen puhdas logiikka: releases/latest-vastauksen jäsennys
/// ja versiovertailu. Verkko-I/O on App-kerroksen UpdateServicessä.
/// </summary>
public static class UpdateChecker
{
    public static UpdateInfo? ParseLatestRelease(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("tag_name", out JsonElement tag) ||
                !root.TryGetProperty("html_url", out JsonElement url))
            {
                return null;
            }

            string version = (tag.GetString() ?? "").TrimStart('v', 'V');
            string releaseUrl = url.GetString() ?? "";
            if (version.Length == 0 || releaseUrl.Length == 0)
            {
                return null;
            }

            string notes = root.TryGetProperty("body", out JsonElement body)
                ? body.GetString() ?? ""
                : "";

            string? assetUrl = null;
            if (root.TryGetProperty("assets", out JsonElement assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out JsonElement n)
                        ? n.GetString() ?? ""
                        : "";
                    if (name.StartsWith("HardwareMonitor-Setup-", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        asset.TryGetProperty("browser_download_url", out JsonElement dl))
                    {
                        assetUrl = dl.GetString();
                        break;
                    }
                }
            }

            return new UpdateInfo(version, releaseUrl, assetUrl, notes);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Aja testit — odota PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter UpdateCheckerTests`
Expected: 4 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.Core/Updates src/HardwareMonitor.Tests/Updates
git commit -m "Lisää UpdateChecker: GitHub-releasen jäsennys (TDD)"
```

---

### Task 2: UpdateChecker.IsNewer + ShouldNotify (Core, TDD)

**Files:**
- Modify: `src/HardwareMonitor.Core/Updates/UpdateChecker.cs`
- Test: `src/HardwareMonitor.Tests/Updates/UpdateCheckerTests.cs`

**Interfaces:**
- Produces: `static bool IsNewer(string currentVersion, string latestVersion)`; `static bool ShouldNotify(string latestVersion, string currentVersion, string lastNotifiedVersion)`.

- [ ] **Step 1: Kirjoita epäonnistuvat testit** (lisää UpdateCheckerTests-luokkaan)

```csharp
    [Theory]
    [InlineData("1.0.5", "1.0.6", true)]
    [InlineData("1.0.5", "1.0.5", false)]
    [InlineData("1.0.6", "1.0.5", false)]
    [InlineData("1.0.5", "1.1.0", true)]
    [InlineData("1.0.5.0", "1.0.6", true)]
    public void IsNewer_vertaa_versionumeroita(string current, string latest, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(current, latest));

    [Fact]
    public void IsNewer_jasentymaton_versio_ei_ole_uudempi()
    {
        Assert.False(UpdateChecker.IsNewer("1.0.5", "beta"));
        Assert.False(UpdateChecker.IsNewer("outo", "1.0.6"));
    }

    [Fact]
    public void ShouldNotify_uusi_versio_ilmoitetaan_vain_kerran()
    {
        Assert.True(UpdateChecker.ShouldNotify("1.0.6", "1.0.5", ""));
        Assert.False(UpdateChecker.ShouldNotify("1.0.6", "1.0.5", "1.0.6"));
        Assert.True(UpdateChecker.ShouldNotify("1.0.7", "1.0.5", "1.0.6"));
        Assert.False(UpdateChecker.ShouldNotify("1.0.5", "1.0.5", ""));
    }
```

- [ ] **Step 2: Aja testit — odota FAIL** (käännösvirhe: IsNewer puuttuu)

- [ ] **Step 3: Toteuta** (lisää UpdateChecker-luokkaan)

```csharp
    public static bool IsNewer(string currentVersion, string latestVersion) =>
        Version.TryParse(currentVersion, out Version? current) &&
        Version.TryParse(latestVersion, out Version? latest) &&
        latest > current;

    /// <summary>
    /// Ilmoitetaan vain uudemmasta versiosta ja vain kerran per versio —
    /// "Myöhemmin"-valinta ei johda jankutukseen joka käynnistyksellä.
    /// </summary>
    public static bool ShouldNotify(
        string latestVersion, string currentVersion, string lastNotifiedVersion) =>
        IsNewer(currentVersion, latestVersion) &&
        !string.Equals(latestVersion, lastNotifiedVersion, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 4: Aja testit — odota PASS** (koko UpdateCheckerTests vihreä)

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.Core/Updates/UpdateChecker.cs src/HardwareMonitor.Tests/Updates/UpdateCheckerTests.cs
git commit -m "Lisää UpdateCheckeriin versiovertailu ja ilmoituspäätös (TDD)"
```

---

### Task 3: UpdateSettings-asetukset (Core, TDD)

**Files:**
- Modify: `src/HardwareMonitor.Core/Settings/AppSettings.cs`
- Modify: `src/HardwareMonitor.Core/Settings/SettingsService.cs` (Normalize)
- Test: `src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `class UpdateSettings { bool CheckAutomatically = true; string LastNotifiedVersion = "" }`; `AppSettings.Updates` (ei koskaan null Loadin jälkeen).

- [ ] **Step 1: Kirjoita epäonnistuva testi** (lisää SettingsServiceTests-luokkaan; käytä tiedoston olemassa olevaa temp-hakemistokäytäntöä — jos sellaista ei ole, tämä muoto)

```csharp
    [Fact]
    public void Load_taydentaa_puuttuvan_updates_osion_oletuksilla()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hwmon-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                """{ "Updates": null }""");

            AppSettings settings = new SettingsService(dir).Load();

            Assert.NotNull(settings.Updates);
            Assert.True(settings.Updates.CheckAutomatically);
            Assert.Equal("", settings.Updates.LastNotifiedVersion);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
```

- [ ] **Step 2: Aja testit — odota FAIL** (`AppSettings.Updates` puuttuu)

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter SettingsServiceTests`

- [ ] **Step 3: Toteuta**

`AppSettings.cs` — lisää luokka ennen `AppSettings`-luokkaa ja property sen sisään:

```csharp
/// <summary>Sovelluksen oman päivitystarkistuksen asetukset.</summary>
public sealed class UpdateSettings
{
    /// <summary>Tarkista GitHub Releases käynnistyksessä + kerran vuorokaudessa.</summary>
    public bool CheckAutomatically { get; set; } = true;

    /// <summary>Versio josta on jo ilmoitettu — sama versio ilmoitetaan vain kerran.</summary>
    public string LastNotifiedVersion { get; set; } = "";
}
```

```csharp
    // AppSettings-luokkaan, InsightsNotes-propertyn jälkeen:
    /// <summary>Sovelluksen päivitystarkistus (ainoa verkkoyhteys — voi kytkeä pois).</summary>
    public UpdateSettings Updates { get; set; } = new();
```

`SettingsService.cs` `Normalize`-metodiin muiden `??=`-rivien jatkoksi:

```csharp
        s.Updates ??= new UpdateSettings();
        s.Updates.LastNotifiedVersion ??= "";
```

- [ ] **Step 4: Aja testit — odota PASS** (koko testiprojekti vihreä)

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj`

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.Core/Settings src/HardwareMonitor.Tests/Settings/SettingsServiceTests.cs
git commit -m "Lisää päivitystarkistuksen asetukset (TDD)"
```

---

### Task 4: UpdateService + ajastus + asetusvalinta (App)

**Files:**
- Create: `src/HardwareMonitor.App/Services/UpdateService.cs`
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs`
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (Asetukset → Yleiset -ryhmä, rivin 418 CheckBoxin jälkeen)
- Modify: `src/HardwareMonitor.App/Localization/UiStrings.cs` + `UiStrings.resx` + `UiStrings.en.resx`

**Interfaces:**
- Consumes: `UpdateChecker.ParseLatestRelease/IsNewer/ShouldNotify`, `AppSettings.Updates` (Taskit 1–3).
- Produces: `MainViewModel.CurrentVersion` (static string, esim. "1.0.5"); `enum UpdateCheckOutcome { UpdateAvailable, UpToDate, Failed }`; `record UpdateCheckResult(UpdateCheckOutcome Outcome, UpdateInfo? Update)`; `Task<UpdateCheckResult> MainViewModel.CheckForUpdatesAsync(bool manual)`; `event Action<UpdateInfo>? MainViewModel.UpdateAvailable` (laukeaa VAIN automaattitarkistuksesta, taustasäikeessä).

- [ ] **Step 1: UpdateService**

```csharp
// src/HardwareMonitor.App/Services/UpdateService.cs
using System.Net.Http;
using System.Net.Http.Headers;
using HardwareMonitor.Core.Updates;

namespace HardwareMonitor.App.Services;

/// <summary>
/// Hakee uusimman julkaisun tiedot GitHubista. Sovelluksen ainoa verkkokutsu —
/// kaikki virheet ovat hiljaisia (null + debug-lokirivi), ilman verkkoa
/// mikään ei häiriinny.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/jrs8205/Computer-App/releases/latest";

    private readonly HttpClient _http;
    private readonly Action<string> _log;

    public UpdateService(string currentVersion, Action<string> log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub API vaatii User-Agent-otsakkeen — ilman sitä vastaus on 403.
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("HardwareMonitor", currentVersion));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>Uusimman releasen tiedot tai null (virhe on jo lokitettu).</summary>
    public async Task<UpdateInfo?> FetchLatestAsync()
    {
        try
        {
            string json = await _http.GetStringAsync(LatestReleaseUrl).ConfigureAwait(false);
            UpdateInfo? info = UpdateChecker.ParseLatestRelease(json);
            if (info is null)
            {
                _log("Päivitystarkistus: GitHub-vastaus ei jäsentynyt.");
            }

            return info;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log($"Päivitystarkistus epäonnistui: {ex.Message}");
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
```

- [ ] **Step 2: MainViewModel — versio, tulostyypit, tarkistusmetodi, ajastus, asetusproperty**

Kenttien joukkoon (rivin 68 `_tickCount`-kentän jälkeen):

```csharp
    private UpdateService? _updateService;
    private int _updateCheckRunning;
```

Luokkaan (LogOverlayRecovered-metodin jälkeen):

```csharp
    /// <summary>Sovelluksen versio kolmiosaisena (1.0.5 — ei .NET:n neljättä nollaa).</summary>
    public static string CurrentVersion { get; } =
        (typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0, 0)).ToString(3);

    /// <summary>Automaattitarkistus löysi uuden version (laukeaa taustasäikeessä).</summary>
    public event Action<UpdateInfo>? UpdateAvailable;

    /// <summary>
    /// Tarkistaa päivitykset. Automaattikutsu ilmoittaa UpdateAvailable-eventillä
    /// vain kerran per versio; manuaalikutsu palauttaa aina tuloksen kutsujalle
    /// (Ylläpito-välilehden nappi näyttää dialogin itse, ei balloonia).
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool manual)
    {
        // Vain yksi tarkistus kerrallaan (nappi + ajastus eivät kilpaile).
        if (Interlocked.CompareExchange(ref _updateCheckRunning, 1, 0) != 0)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.Failed, null);
        }

        try
        {
            _updateService ??= new UpdateService(CurrentVersion, _logger.Log);
            UpdateInfo? info = await Task.Run(_updateService.FetchLatestAsync).ConfigureAwait(false);
            if (info is null)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.Failed, null);
            }

            if (!UpdateChecker.IsNewer(CurrentVersion, info.Version))
            {
                return new UpdateCheckResult(UpdateCheckOutcome.UpToDate, null);
            }

            if (!manual && UpdateChecker.ShouldNotify(
                    info.Version, CurrentVersion, _settings.Updates.LastNotifiedVersion))
            {
                _settings.Updates.LastNotifiedVersion = info.Version;
                _settingsService.Save(_settings);
                UpdateAvailable?.Invoke(info);
            }

            return new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, info);
        }
        finally
        {
            Interlocked.Exchange(ref _updateCheckRunning, 0);
        }
    }
```

Uusi tiedostotason tyyppi samaan namespaceen (MainViewModel.cs:n loppuun tai omaksi lohkoksi luokan yläpuolelle):

```csharp
/// <summary>Manuaalisen päivitystarkistuksen tulos Ylläpito-välilehdelle.</summary>
public enum UpdateCheckOutcome
{
    UpdateAvailable,
    UpToDate,
    Failed,
}

public sealed record UpdateCheckResult(UpdateCheckOutcome Outcome, UpdateInfo? Update);
```

Ajastus `Refresh()`-metodiin, `_tickCount % 300 == 0` -lohkon (rivi ~513) jälkeen:

```csharp
            // Päivitystarkistus: 30 s käynnistyksestä, sitten kerran vuorokaudessa.
            if (_settings.Updates.CheckAutomatically &&
                (_tickCount == 30 || _tickCount % 86_400 == 0))
            {
                _ = CheckForUpdatesAsync(manual: false);
            }
```

Asetusproperty `AlertNotificationsEnabled`-propertyn (rivi ~304) viereen — kopioi sen runko täsmälleen (tallennus + PropertyChanged samalla tavalla):

```csharp
    /// <summary>Automaattinen päivitystarkistus GitHubista (Asetukset → Yleiset).</summary>
    public bool UpdateCheckEnabled
    {
        get => _settings.Updates.CheckAutomatically;
        set
        {
            if (_settings.Updates.CheckAutomatically == value)
            {
                return;
            }

            _settings.Updates.CheckAutomatically = value;
            // sama tallennus- ja PropertyChanged-kutsu kuin AlertNotificationsEnabled-setterissä
        }
    }
```

Lisää `using HardwareMonitor.Core.Updates;` ja `using HardwareMonitor.App.Services;` (jälkimmäinen on jo).

- [ ] **Step 3: XAML-checkbox** (`MainWindow.xaml`, Set_AlertNotifications-CheckBoxin jälkeen rivillä ~420)

```xml
                                <CheckBox Content="{x:Static loc:UiStrings.Set_UpdateCheck}" IsChecked="{Binding UpdateCheckEnabled}"
                                          Foreground="White" Margin="0,2"
                                          ToolTip="{x:Static loc:UiStrings.Set_UpdateCheckTip}" />
```

- [ ] **Step 4: Lokalisointi** — avaimet MOLEMPIIN resx:iin + accessorit `UiStrings.cs`:ään:

| Avain | fi (UiStrings.resx) | en (UiStrings.en.resx) |
|---|---|---|
| Set_UpdateCheck | Tarkista päivitykset automaattisesti | Check for updates automatically |
| Set_UpdateCheckTip | Hakee uusimman version tiedot GitHubista käynnistyksessä ja kerran vuorokaudessa. Sovelluksen ainoa verkkoyhteys. | Fetches the latest release info from GitHub at startup and once a day. The app's only network connection. |

```csharp
    public static string Set_UpdateCheck => T(nameof(Set_UpdateCheck));
    public static string Set_UpdateCheckTip => T(nameof(Set_UpdateCheckTip));
```

- [ ] **Step 5: Buildaa ja testaa**

Run: `dotnet build HardwareMonitor.sln` → 0 virhettä; `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → kaikki vihreitä (EnglishResourceTests mukaan lukien).

- [ ] **Step 6: Commit**

```bash
git add src/HardwareMonitor.App
git commit -m "Lisää päivitystarkistus: UpdateService, vuorokausiajastus ja asetus"
```

---

### Task 5: AuthenticodeVerifier + UpdateInstaller + UpdateDialog + tray-ilmoitus (App)

**Files:**
- Create: `src/HardwareMonitor.App/Services/AuthenticodeVerifier.cs`
- Create: `src/HardwareMonitor.App/Services/UpdateInstaller.cs`
- Create: `src/HardwareMonitor.App/UpdateDialog.xaml` + `UpdateDialog.xaml.cs`
- Modify: `src/HardwareMonitor.App/MainWindow.xaml.cs` (balloon + BalloonTipClicked + dialogi)
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (LogUpdate-apuri)
- Modify: `src/HardwareMonitor.App/Localization/UiStrings.cs` + molemmat resx:t

**Interfaces:**
- Consumes: `UpdateInfo`, `MainViewModel.UpdateAvailable`, `MainViewModel.CurrentVersion` (Task 4).
- Produces: `static bool AuthenticodeVerifier.IsValid(string filePath, Action<string> log)`; `static Task<string?> UpdateInstaller.DownloadAndRunAsync(UpdateInfo update, Action<string> log)` (null = onnistui, muuten virheviesti); `void MainViewModel.LogUpdate(string message)`; `MainWindow.ShowPendingUpdateDialog()` + kenttä `_pendingUpdate`.

- [ ] **Step 1: AuthenticodeVerifier** (WinVerifyTrust + thumbprint-pinnaus)

```csharp
// src/HardwareMonitor.App/Services/AuthenticodeVerifier.cs
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HardwareMonitor.App.Services;

/// <summary>
/// Varmistaa ladatun tiedoston Authenticode-allekirjoituksen (WinVerifyTrust)
/// JA että allekirjoittaja on täsmälleen oma julkaisuvarmenteemme
/// (thumbprint-pinnaus) — pelkkä "joku luotettu allekirjoitus" ei riitä.
/// </summary>
public static class AuthenticodeVerifier
{
    /// <summary>CN=jrs8205 Hardware Monitor (voimassa 2031, luotettu koneen Rootissa).</summary>
    public const string ExpectedThumbprint = "346D869550F3A7BD54FA947E024341C64F729AF8";

    public static bool IsValid(string filePath, Action<string> log)
    {
        if (!VerifyTrust(filePath))
        {
            log($"Päivityksen allekirjoitus ei kelpaa: {filePath}");
            return false;
        }

        try
        {
            using var cert = new X509Certificate2(
                X509Certificate.CreateFromSignedFile(filePath));
            bool match = string.Equals(
                cert.Thumbprint, ExpectedThumbprint, StringComparison.OrdinalIgnoreCase);
            if (!match)
            {
                log($"Päivityksen allekirjoittaja on väärä: {cert.Thumbprint}");
            }

            return match;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            log($"Päivityksen varmenteen luku epäonnistui: {ex.Message}");
            return false;
        }
    }

    private static readonly Guid ActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private static bool VerifyTrust(string filePath)
    {
        IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
        try
        {
            var fileInfo = new WintrustFileInfo
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(),
                pcwszFilePath = filePath,
            };
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, fDeleteOld: false);

            var data = new WintrustData
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustData>(),
                dwUIChoice = 2,          // WTD_UI_NONE — ei dialogeja
                fdwRevocationChecks = 0, // WTD_REVOKE_NONE — itse allekirjoitettu, ei CRL:ää
                dwUnionChoice = 1,       // WTD_CHOICE_FILE
                pFile = fileInfoPtr,
            };
            Guid action = ActionGenericVerifyV2;
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd, ref Guid actionId, ref WintrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustFileInfo
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustData
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }
}
```

- [ ] **Step 2: UpdateInstaller**

```csharp
// src/HardwareMonitor.App/Services/UpdateInstaller.cs
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using HardwareMonitor.App.Localization;
using HardwareMonitor.Core.Updates;

namespace HardwareMonitor.App.Services;

/// <summary>Lataa setup.exen, varmistaa allekirjoituksen ja käynnistää asennuksen.</summary>
public static class UpdateInstaller
{
    /// <summary>Palauttaa null onnistuessa, muuten käyttäjälle näytettävän virheviestin.</summary>
    public static async Task<string?> DownloadAndRunAsync(UpdateInfo update, Action<string> log)
    {
        if (update.SetupAssetUrl is null)
        {
            return UiStrings.Upd_NoAsset;
        }

        string target = Path.Combine(
            Path.GetTempPath(), $"HardwareMonitor-Setup-{update.Version}.exe");
        try
        {
            // Oma client omalla timeoutilla: self-contained setup on kymmeniä
            // megatavuja — API-kutsujen 10 s ei riittäisi lataukseen.
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("HardwareMonitor", update.Version));
            byte[] bytes = await http.GetByteArrayAsync(update.SetupAssetUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(target, bytes).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            log($"Päivityksen lataus epäonnistui: {ex.Message}");
            return string.Format(UiStrings.Upd_DownloadError, ex.Message);
        }

        if (!AuthenticodeVerifier.IsValid(target, log))
        {
            return UiStrings.Upd_SignatureError;
        }

        log($"Käynnistetään päivitysasennus {update.Version}.");
        // Installerin CloseApplications sulkee tämän sovelluksen siististi
        // Restart Managerilla — sovellus ei sulje itseään tässä.
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        return null;
    }
}
```

- [ ] **Step 3: UpdateDialog**

```xml
<!-- src/HardwareMonitor.App/UpdateDialog.xaml -->
<Window x:Class="HardwareMonitor.App.UpdateDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:loc="clr-namespace:HardwareMonitor.App.Localization"
        Title="{x:Static loc:UiStrings.Upd_DialogTitle}"
        Width="520" SizeToContent="Height" MaxHeight="560"
        Background="#1E1E1E" ResizeMode="NoResize" ShowInTaskbar="False">
    <StackPanel Margin="16">
        <TextBlock x:Name="VersionText" Foreground="White" FontSize="15" FontWeight="Bold" />
        <TextBlock Text="{x:Static loc:UiStrings.Upd_WhatsNew}" Foreground="#4FC3F7"
                   FontWeight="SemiBold" Margin="0,12,0,4" />
        <TextBox x:Name="NotesText" IsReadOnly="True" TextWrapping="Wrap"
                 VerticalScrollBarVisibility="Auto" MaxHeight="260" BorderThickness="1"
                 Background="#252526" Foreground="#E0E0E0" BorderBrush="#9E9E9E" Padding="8" />
        <TextBlock x:Name="StatusText" Foreground="#E0E0E0" TextWrapping="Wrap" Margin="0,8,0,0" />
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button x:Name="InstallButton" Content="{x:Static loc:UiStrings.Upd_InstallNow}"
                    Click="InstallNow_Click" Padding="12,4" />
            <Button x:Name="LaterButton" Content="{x:Static loc:UiStrings.Upd_Later}"
                    Click="Later_Click" Padding="12,4" Margin="8,0,0,0" />
        </StackPanel>
    </StackPanel>
</Window>
```

```csharp
// src/HardwareMonitor.App/UpdateDialog.xaml.cs
using System.Windows;
using HardwareMonitor.App.Localization;
using HardwareMonitor.App.Services;
using HardwareMonitor.App.ViewModels;
using HardwareMonitor.Core.Updates;

namespace HardwareMonitor.App;

/// <summary>Uuden version tiedot + Asenna nyt / Myöhemmin.</summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _update;
    private readonly Action<string> _log;

    public UpdateDialog(UpdateInfo update, Action<string> log)
    {
        InitializeComponent();
        _update = update;
        _log = log;
        VersionText.Text = string.Format(
            UiStrings.Upd_VersionLine, update.Version, MainViewModel.CurrentVersion);
        NotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? UiStrings.Upd_NoNotes
            : update.ReleaseNotes;
    }

    private async void InstallNow_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        StatusText.Text = UiStrings.Upd_Downloading;

        string? error = await UpdateInstaller.DownloadAndRunAsync(_update, _log);
        if (error is null)
        {
            // Installeri sulkee sovelluksen Restart Managerilla — dialogi vain pois.
            Close();
            return;
        }

        StatusText.Text = error;
        InstallButton.IsEnabled = true;
        LaterButton.IsEnabled = true;
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 4: MainViewModel-apuri** (LogOverlayRecovered-metodin viereen)

```csharp
    /// <summary>Päivityskomponenttien lokikanava (UpdateDialog, UpdateInstaller).</summary>
    public void LogUpdate(string message) => _logger.Log(message);
```

- [ ] **Step 5: MainWindow-wiring** (`MainWindow.xaml.cs`)

Kenttä (rivin 22 `_monitoringStarted` jälkeen):

```csharp
    private Core.Updates.UpdateInfo? _pendingUpdate;
```

`StartMonitoring()`-metodiin (rivi ~87, NotificationRequested-rivin jälkeen):

```csharp
        _viewModel.UpdateAvailable += OnUpdateAvailable;
```

`CreateTrayIcon()`-metodiin (DoubleClick-rivin 116 jälkeen):

```csharp
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.BeginInvoke(ShowPendingUpdateDialog);
```

Uudet metodit (ShowTrayNotification-metodin jälkeen):

```csharp
    /// <summary>Automaattitarkistus löysi uuden version — balloon, klikkaus avaa dialogin.</summary>
    private void OnUpdateAvailable(Core.Updates.UpdateInfo update) =>
        Dispatcher.BeginInvoke(() =>
        {
            _pendingUpdate = update;
            _trayIcon?.ShowBalloonTip(
                10_000,
                UiStrings.Upd_NotifyTitle,
                string.Format(UiStrings.Upd_NotifyMessage, update.Version),
                System.Windows.Forms.ToolTipIcon.Info);
        });

    /// <summary>Avaa päivitysdialogin, jos ilmoitettu päivitys odottaa.</summary>
    private void ShowPendingUpdateDialog()
    {
        if (_pendingUpdate is null)
        {
            return;
        }

        var dialog = new UpdateDialog(_pendingUpdate, _viewModel.LogUpdate);
        if (IsVisible)
        {
            dialog.Owner = this;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            // Tray-tilassa pääikkuna on piilossa — piilotettu Owner estäisi dialogin näkymisen.
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
    }
```

- [ ] **Step 6: Lokalisointi** — molemmat resx:t + accessorit:

| Avain | fi | en |
|---|---|---|
| Upd_NotifyTitle | Päivitys saatavilla | Update available |
| Upd_NotifyMessage | Hardware Monitor {0} on julkaistu. Napsauta asentaaksesi. | Hardware Monitor {0} has been released. Click to install. |
| Upd_DialogTitle | Sovelluksen päivitys | Application update |
| Upd_VersionLine | Uusi versio {0} (nykyinen {1}) | New version {0} (current {1}) |
| Upd_WhatsNew | Mitä uutta | What's new |
| Upd_NoNotes | Julkaisulla ei ole muutostekstiä. | This release has no notes. |
| Upd_InstallNow | Asenna nyt | Install now |
| Upd_Later | Myöhemmin | Later |
| Upd_Downloading | Ladataan ja tarkistetaan asennuspakettia… | Downloading and verifying the installer… |
| Upd_DownloadError | Lataus epäonnistui: {0} | Download failed: {0} |
| Upd_SignatureError | Ladatun paketin allekirjoitus ei kelpaa — asennusta ei käynnistetty. | The downloaded package's signature is invalid — installation was not started. |
| Upd_NoAsset | Julkaisusta puuttuu asennuspaketti. | The release has no installer attachment. |

```csharp
    public static string Upd_NotifyTitle => T(nameof(Upd_NotifyTitle));
    public static string Upd_NotifyMessage => T(nameof(Upd_NotifyMessage));
    public static string Upd_DialogTitle => T(nameof(Upd_DialogTitle));
    public static string Upd_VersionLine => T(nameof(Upd_VersionLine));
    public static string Upd_WhatsNew => T(nameof(Upd_WhatsNew));
    public static string Upd_NoNotes => T(nameof(Upd_NoNotes));
    public static string Upd_InstallNow => T(nameof(Upd_InstallNow));
    public static string Upd_Later => T(nameof(Upd_Later));
    public static string Upd_Downloading => T(nameof(Upd_Downloading));
    public static string Upd_DownloadError => T(nameof(Upd_DownloadError));
    public static string Upd_SignatureError => T(nameof(Upd_SignatureError));
    public static string Upd_NoAsset => T(nameof(Upd_NoAsset));
```

- [ ] **Step 7: Buildaa ja testaa**

Run: `dotnet build HardwareMonitor.sln` → 0 virhettä; `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → vihreä.

- [ ] **Step 8: Commit**

```bash
git add src/HardwareMonitor.App
git commit -m "Lisää päivitysdialogi, lataus ja allekirjoitustarkistus"
```

---

### Task 6: VendorLinkResolver (Core, TDD)

**Files:**
- Create: `src/HardwareMonitor.Core/Maintenance/VendorLinkResolver.cs`
- Test: `src/HardwareMonitor.Tests/Maintenance/VendorLinkResolverTests.cs`

**Interfaces:**
- Produces: `static string? VendorLinkResolver.Resolve(string? deviceName, string language)` — `language` on kaksikirjaiminen ("fi", "en", …).

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
// src/HardwareMonitor.Tests/Maintenance/VendorLinkResolverTests.cs
using HardwareMonitor.Core.Maintenance;

namespace HardwareMonitor.Tests.Maintenance;

public class VendorLinkResolverTests
{
    [Fact]
    public void Asus_emolevy_ohjautuu_kielen_mukaiselle_tukisivulle()
    {
        Assert.Equal("https://www.asus.com/fi/support/",
            VendorLinkResolver.Resolve("ASUS ROG STRIX Z390-F GAMING", "fi"));
        Assert.Equal("https://www.asus.com/support/",
            VendorLinkResolver.Resolve("ASUS ROG STRIX Z390-F GAMING", "xx"));
    }

    [Fact]
    public void Nvidia_gpu_ohjautuu_ajurisivulle()
    {
        Assert.Equal("https://www.nvidia.com/fi-fi/drivers/",
            VendorLinkResolver.Resolve("NVIDIA GeForce RTX 2060", "fi"));
        Assert.Equal("https://www.nvidia.com/Download/index.aspx",
            VendorLinkResolver.Resolve("NVIDIA GeForce RTX 2060", "xx"));
    }

    [Fact]
    public void Samsung_ssd_ohjautuu_globaalille_tyokalusivulle()
    {
        Assert.Equal("https://semiconductor.samsung.com/consumer-storage/support/tools/",
            VendorLinkResolver.Resolve("Samsung SSD 970 EVO Plus 1TB", "fi"));
    }

    [Fact]
    public void Tuntematon_valmistaja_tai_tyhja_nimi_ei_saa_linkkia()
    {
        Assert.Null(VendorLinkResolver.Resolve("Kingston A400", "fi"));
        Assert.Null(VendorLinkResolver.Resolve(null, "fi"));
        Assert.Null(VendorLinkResolver.Resolve("  ", "fi"));
    }

    [Fact]
    public void Hantavalilyonnit_ja_kirjainkoko_eivat_haittaa()
    {
        Assert.Equal("https://semiconductor.samsung.com/consumer-storage/support/tools/",
            VendorLinkResolver.Resolve("SAMSUNG ssd 860 EVO 1TB ", "fi"));
    }
}
```

- [ ] **Step 2: Aja testit — odota FAIL** (VendorLinkResolver puuttuu)

- [ ] **Step 3: Toteuta**

```csharp
// src/HardwareMonitor.Core/Maintenance/VendorLinkResolver.cs
namespace HardwareMonitor.Core.Maintenance;

/// <summary>
/// Laitenimi → valmistajan tukisivun osoite. Tarkoituksella EI syvälinkkejä
/// mallisivuille (osoitteet muuttuvat ja hajoavat hiljaa) — linkki vie
/// valmistajan tukisivulle ja mallinimen voi kopioida hakua varten.
/// Aluevalinta tulee Windowsin kieliasetuksesta, ei IP-paikannuksesta.
/// </summary>
public static class VendorLinkResolver
{
    public static string? Resolve(string? deviceName, string language)
    {
        string name = deviceName?.Trim() ?? "";
        if (name.Length == 0)
        {
            return null;
        }

        bool finnish = string.Equals(language, "fi", StringComparison.OrdinalIgnoreCase);

        if (name.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
        {
            return finnish ? "https://www.asus.com/fi/support/" : "https://www.asus.com/support/";
        }

        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return finnish
                ? "https://www.nvidia.com/fi-fi/drivers/"
                : "https://www.nvidia.com/Download/index.aspx";
        }

        if (name.Contains("Samsung SSD", StringComparison.OrdinalIgnoreCase))
        {
            return "https://semiconductor.samsung.com/consumer-storage/support/tools/";
        }

        return null;
    }
}
```

- [ ] **Step 4: Aja testit — odota PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter VendorLinkResolverTests`

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.Core/Maintenance src/HardwareMonitor.Tests/Maintenance
git commit -m "Lisää VendorLinkResolver: valmistajien tukisivulinkit (TDD)"
```

---

### Task 7: NvidiaDriverVersion (Core, TDD)

**Files:**
- Create: `src/HardwareMonitor.Core/Maintenance/NvidiaDriverVersion.cs`
- Test: `src/HardwareMonitor.Tests/Maintenance/NvidiaDriverVersionTests.cs`

**Interfaces:**
- Produces: `static string? NvidiaDriverVersion.ToMarketingVersion(string? wmiVersion)`.

- [ ] **Step 1: Kirjoita epäonnistuvat testit**

```csharp
// src/HardwareMonitor.Tests/Maintenance/NvidiaDriverVersionTests.cs
using HardwareMonitor.Core.Maintenance;

namespace HardwareMonitor.Tests.Maintenance;

public class NvidiaDriverVersionTests
{
    [Theory]
    [InlineData("32.0.15.4680", "546.80")]
    [InlineData("31.0.15.5222", "552.22")]
    [InlineData("30.0.14.7168", "471.68")]
    public void ToMarketingVersion_muuntaa_wmi_version_nvidian_muotoon(string wmi, string expected) =>
        Assert.Equal(expected, NvidiaDriverVersion.ToMarketingVersion(wmi));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4680")]
    [InlineData("1.2")]
    [InlineData("a.b.c.d")]
    public void ToMarketingVersion_kelvoton_syote_palauttaa_null(string? wmi) =>
        Assert.Null(NvidiaDriverVersion.ToMarketingVersion(wmi));
}
```

- [ ] **Step 2: Aja testit — odota FAIL**

- [ ] **Step 3: Toteuta**

```csharp
// src/HardwareMonitor.Core/Maintenance/NvidiaDriverVersion.cs
namespace HardwareMonitor.Core.Maintenance;

/// <summary>
/// WMI:n DriverVersion (esim. "32.0.15.4680") → NVIDIAn markkinointiversio
/// ("546.80"): kahden viimeisen kentän numerot yhteen ja viisi viimeistä
/// merkkiä muodossa xxx.xx. Ilman muunnosta arvoa ei voi verrata NVIDIAn
/// sivulla näkyvään versionumeroon.
/// </summary>
public static class NvidiaDriverVersion
{
    public static string? ToMarketingVersion(string? wmiVersion)
    {
        string[] parts = (wmiVersion ?? "").Split('.');
        if (parts.Length < 4)
        {
            return null;
        }

        string digits = string.Concat(parts[^2], parts[^1]);
        if (digits.Length < 5 || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        string tail = digits[^5..];
        return $"{tail[..3]}.{tail[3..]}";
    }
}
```

- [ ] **Step 4: Aja testit — odota PASS**

Run: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj --filter NvidiaDriverVersionTests`

- [ ] **Step 5: Commit**

```bash
git add src/HardwareMonitor.Core/Maintenance/NvidiaDriverVersion.cs src/HardwareMonitor.Tests/Maintenance/NvidiaDriverVersionTests.cs
git commit -m "Lisää NVIDIA-ajuriversion muunnos WMI-muodosta (TDD)"
```

---

### Task 8: DeviceVersionReader (App, WMI)

**Files:**
- Modify: `src/HardwareMonitor.App/HardwareMonitor.App.csproj` (PackageReference)
- Create: `src/HardwareMonitor.App/Services/DeviceVersionReader.cs`

**Interfaces:**
- Produces: `record DeviceVersions(string? BiosVersion, DateTime? BiosDate, string? GpuDriverVersion, DateTime? GpuDriverDate, IReadOnlyList<(string Model, string Firmware)> DiskFirmware)`; `static DeviceVersions DeviceVersionReader.Read(string? gpuName, Action<string> log)`.

- [ ] **Step 1: Paketti** — `HardwareMonitor.App.csproj`, LiveCharts-PackageReferencen viereen:

```xml
    <!-- WMI-luku (BIOS-, ajuri- ja firmware-versiot Ylläpito-välilehdelle). -->
    <PackageReference Include="System.Management" Version="8.0.0" />
```

- [ ] **Step 2: Toteuta**

```csharp
// src/HardwareMonitor.App/Services/DeviceVersionReader.cs
using System.Management;

namespace HardwareMonitor.App.Services;

/// <summary>Laitteiden nykyversiot Ylläpito-välilehdelle.</summary>
public sealed record DeviceVersions(
    string? BiosVersion,
    DateTime? BiosDate,
    string? GpuDriverVersion,
    DateTime? GpuDriverDate,
    IReadOnlyList<(string Model, string Firmware)> DiskFirmware);

/// <summary>
/// Lukee BIOS-, näytönohjain- ja levyversiot WMI:stä. Vain luku; jokainen
/// kysely siedetään erikseen — WMI-virhe ei kaada Ylläpito-näkymää, puuttuva
/// arvo näkyy viivana.
/// </summary>
public static class DeviceVersionReader
{
    public static DeviceVersions Read(string? gpuName, Action<string> log)
    {
        string? biosVersion = null;
        DateTime? biosDate = null;
        Query("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", row =>
        {
            biosVersion ??= (row["SMBIOSBIOSVersion"] as string)?.Trim();
            biosDate ??= ToDate(row["ReleaseDate"]);
        }, log);

        // Hybridikoneessa voi olla useita ohjaimia — valitaan sama GPU kuin
        // mittauksissa (nimivertailu), muuten ensimmäinen.
        var controllers = new List<(string Name, string? Version, DateTime? Date)>();
        Query("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController", row =>
            controllers.Add((
                ((row["Name"] as string) ?? "").Trim(),
                (row["DriverVersion"] as string)?.Trim(),
                ToDate(row["DriverDate"]))), log);
        (string Name, string? Version, DateTime? Date) gpu = controllers.FirstOrDefault(c =>
            string.Equals(c.Name, gpuName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (gpu.Name is null or "" && controllers.Count > 0)
        {
            gpu = controllers[0];
        }

        var disks = new List<(string, string)>();
        Query("SELECT Model, FirmwareRevision FROM Win32_DiskDrive", row =>
        {
            string model = ((row["Model"] as string) ?? "").Trim();
            if (model.Length > 0)
            {
                disks.Add((model, ((row["FirmwareRevision"] as string) ?? "").Trim()));
            }
        }, log);

        return new DeviceVersions(biosVersion, biosDate, gpu.Version, gpu.Date, disks);
    }

    private static void Query(string wql, Action<ManagementBaseObject> onRow, Action<string> log)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementBaseObject row in rows)
            {
                using (row)
                {
                    onRow(row);
                }
            }
        }
        catch (Exception ex)
        {
            // WMI heittää mm. ManagementException- ja COMException-poikkeuksia —
            // mikään niistä ei saa estää muiden rivien näyttämistä.
            log($"WMI-kysely epäonnistui ({wql}): {ex.Message}");
        }
    }

    /// <summary>CIM_DATETIME ("20240215000000.000000+000") → DateTime tai null.</summary>
    private static DateTime? ToDate(object? cimDate)
    {
        try
        {
            return cimDate is string s && s.Length > 0
                ? ManagementDateTimeConverter.ToDateTime(s)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

- [ ] **Step 3: Buildaa**

Run: `dotnet build HardwareMonitor.sln` → 0 virhettä.

- [ ] **Step 4: Commit**

```bash
git add src/HardwareMonitor.App/HardwareMonitor.App.csproj src/HardwareMonitor.App/Services/DeviceVersionReader.cs
git commit -m "Lisää DeviceVersionReader: BIOS-, ajuri- ja firmware-versiot WMI:stä"
```

---

### Task 9: Ylläpito-välilehti (MaintenanceViewModel + XAML + manuaalitarkistus)

**Files:**
- Create: `src/HardwareMonitor.App/ViewModels/MaintenanceViewModel.cs`
- Modify: `src/HardwareMonitor.App/ViewModels/MainViewModel.cs` (Maintenance-property + EnsureMaintenanceLoaded)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml` (uusi TabItem Historia-välilehden jälkeen, ennen `</TabControl>`; TabControlille `x:Name="MainTabs"` ja `SelectionChanged="MainTabs_SelectionChanged"`)
- Modify: `src/HardwareMonitor.App/MainWindow.xaml.cs` (välilehden lataus + nappien käsittelijät)
- Modify: `src/HardwareMonitor.App/Localization/UiStrings.cs` + molemmat resx:t

**Interfaces:**
- Consumes: `MachineSpecReader.Read(groups, osDescription, userNotes)` → `MachineSpec` (CpuName, GpuName, MotherboardName, RamTotalGb, DiskNames, …); `DeviceVersionReader.Read(gpuName, log)` (Task 8); `VendorLinkResolver.Resolve` (Task 6); `NvidiaDriverVersion.ToMarketingVersion` (Task 7); `MainViewModel.CheckForUpdatesAsync(manual: true)` (Task 4); `MainWindow._pendingUpdate` + `ShowPendingUpdateDialog()` (Task 5).
- Produces: `MainViewModel.Maintenance` (MaintenanceViewModel); `MaintenanceViewModel.Load(MachineSpec, DeviceVersions, string language)`; `MaintenanceViewModel.ReportCheckResult(UpdateCheckOutcome)`; `MaintenanceRowViewModel { string Kind; string Model; string VersionText; string? Url; bool HasLink }`.

- [ ] **Step 1: MaintenanceViewModel**

```csharp
// src/HardwareMonitor.App/ViewModels/MaintenanceViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using HardwareMonitor.App.Localization;
using HardwareMonitor.App.Services;
using HardwareMonitor.Core.Insights;
using HardwareMonitor.Core.Maintenance;

namespace HardwareMonitor.App.ViewModels;

/// <summary>Ylläpito-välilehden laiterivi.</summary>
public sealed class MaintenanceRowViewModel
{
    public required string Kind { get; init; }
    public required string Model { get; init; }
    public required string VersionText { get; init; }
    public string? Url { get; init; }
    public bool HasLink => Url is not null;
}

/// <summary>
/// Ylläpito-välilehti: laitteiden nykyversiot + valmistajalinkit + sovelluksen
/// päivitystarkistuksen tila. Rivit rakennetaan kerran välilehden avauksessa.
/// </summary>
public sealed class MaintenanceViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MaintenanceRowViewModel> Rows { get; } = new();

    public string AppVersionText =>
        string.Format(UiStrings.Maint_AppVersion, MainViewModel.CurrentVersion);

    private string _checkStatus = "";

    public string CheckStatus
    {
        get => _checkStatus;
        private set
        {
            _checkStatus = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckStatus)));
        }
    }

    public void ReportCheckResult(UpdateCheckOutcome outcome)
    {
        string time = DateTime.Now.ToString("HH.mm");
        CheckStatus = outcome switch
        {
            UpdateCheckOutcome.UpToDate => string.Format(UiStrings.Maint_CheckUpToDate, time),
            UpdateCheckOutcome.UpdateAvailable => string.Format(UiStrings.Maint_CheckFound, time),
            _ => string.Format(UiStrings.Maint_CheckFailed, time),
        };
    }

    public void Load(MachineSpec spec, DeviceVersions versions, string language)
    {
        Rows.Clear();

        Rows.Add(new MaintenanceRowViewModel
        {
            Kind = UiStrings.Maint_Motherboard,
            Model = spec.MotherboardName ?? "—",
            VersionText = Format(UiStrings.Maint_BiosFormat, versions.BiosVersion, versions.BiosDate),
            Url = VendorLinkResolver.Resolve(spec.MotherboardName, language),
        });

        string? marketing = NvidiaDriverVersion.ToMarketingVersion(versions.GpuDriverVersion);
        string driverText = marketing is not null
            ? $"{marketing} ({versions.GpuDriverVersion})"
            : versions.GpuDriverVersion ?? "—";
        Rows.Add(new MaintenanceRowViewModel
        {
            Kind = UiStrings.Maint_Gpu,
            Model = spec.GpuName ?? "—",
            VersionText = Format(UiStrings.Maint_DriverFormat, driverText, versions.GpuDriverDate),
            Url = VendorLinkResolver.Resolve(spec.GpuName, language),
        });

        // WMI-firmware yhdistetään LHM-levynimeen mallinimellä; samannimiset
        // levyt kuluttavat osumia järjestyksessä (2 × 860 EVO tällä koneella).
        var firmwarePool = versions.DiskFirmware.ToList();
        foreach (string disk in spec.DiskNames)
        {
            string model = disk.Trim();
            int match = firmwarePool.FindIndex(f =>
                string.Equals(f.Model, model, StringComparison.OrdinalIgnoreCase));
            string firmware = "—";
            if (match >= 0)
            {
                firmware = firmwarePool[match].Firmware;
                firmwarePool.RemoveAt(match);
            }

            Rows.Add(new MaintenanceRowViewModel
            {
                Kind = UiStrings.Maint_Disk,
                Model = model,
                VersionText = string.Format(UiStrings.Maint_FirmwareFormat, firmware),
                Url = VendorLinkResolver.Resolve(model, language),
            });
        }
    }

    /// <summary>"BIOS 1401 (23.4.2024)" — versio ja päiväys vain jos saatavilla.</summary>
    private static string Format(string format, string? value, DateTime? date)
    {
        string text = string.Format(format, value ?? "—");
        return date is { } d ? $"{text} ({d:d.M.yyyy})" : text;
    }
}
```

Huom: `MachineSpec`-recordin levylista on `DiskNames`
(`IReadOnlyList<string>`) — varmistettu MachineSpec.cs:stä.

- [ ] **Step 2: MainViewModel** — property + lataus (kenttien ja CheckForUpdatesAsync-metodin lähelle):

```csharp
    public MaintenanceViewModel Maintenance { get; } = new();

    private int _maintenanceLoaded;

    /// <summary>
    /// Lataa Ylläpito-välilehden tiedot kerran, taustasäikeessä (WMI-kyselyt
    /// eivät saa jäädyttää UI:ta). Kutsutaan välilehden valinnasta; ennen
    /// ensimmäistä sensoriluentaa kutsu ei tee mitään ja yrittää seuraavalla
    /// valinnalla uudelleen.
    /// </summary>
    public void EnsureMaintenanceLoaded()
    {
        IReadOnlyList<HardwareGroup>? groups = _latestGroups;
        if (groups is null ||
            Interlocked.CompareExchange(ref _maintenanceLoaded, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            MachineSpec spec = MachineSpecReader.Read(
                groups, OsDescriptionText, _settings.InsightsNotes);
            DeviceVersions versions = DeviceVersionReader.Read(spec.GpuName, _logger.Log);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                Maintenance.Load(spec, versions,
                    CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
        });
    }
```

- [ ] **Step 3: XAML** — TabControlille (rivi 109) `x:Name` ja käsittelijä:

```xml
        <TabControl x:Name="MainTabs" Background="#1E1E1E" BorderThickness="0"
                    SelectionChanged="MainTabs_SelectionChanged">
```

Uusi TabItem Historia-TabItemin sulkevan `</TabItem>`:n jälkeen, ennen `</TabControl>`:

```xml
            <TabItem x:Name="MaintenanceTab" Header="{x:Static loc:UiStrings.Tab_Maintenance}">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel Margin="16" MaxWidth="900" HorizontalAlignment="Left"
                                DataContext="{Binding Maintenance}">
                        <TextBlock Text="{x:Static loc:UiStrings.Maint_Intro}" Foreground="#BDBDBD"
                                   TextWrapping="Wrap" Margin="0,0,0,12" />
                        <ItemsControl ItemsSource="{Binding Rows}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Border Background="#252526" CornerRadius="8" Padding="12" Margin="0,0,0,8">
                                        <Grid>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="170" />
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Text="{Binding Kind}" Foreground="#4FC3F7"
                                                       FontWeight="SemiBold" VerticalAlignment="Center" />
                                            <StackPanel Grid.Column="1" Margin="8,0">
                                                <TextBlock Text="{Binding Model}" Foreground="White" />
                                                <TextBlock Text="{Binding VersionText}" Foreground="#A5D6A7"
                                                           Margin="0,2,0,0" />
                                            </StackPanel>
                                            <StackPanel Grid.Column="2" Orientation="Horizontal"
                                                        VerticalAlignment="Center">
                                                <Button Content="{x:Static loc:UiStrings.Maint_CopyModel}"
                                                        Click="CopyModel_Click" Padding="8,2" />
                                                <Button Content="{x:Static loc:UiStrings.Maint_OpenVendorPage}"
                                                        Click="OpenVendorPage_Click"
                                                        IsEnabled="{Binding HasLink}"
                                                        Padding="8,2" Margin="8,0,0,0" />
                                            </StackPanel>
                                        </Grid>
                                    </Border>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                        <Border Background="#252526" CornerRadius="8" Padding="12" Margin="0,8,0,0">
                            <StackPanel>
                                <TextBlock Text="{Binding AppVersionText}" Foreground="White"
                                           FontWeight="SemiBold" />
                                <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                    <Button x:Name="CheckUpdatesButton"
                                            Content="{x:Static loc:UiStrings.Maint_CheckNow}"
                                            Click="CheckUpdates_Click" Padding="10,3" />
                                    <TextBlock Text="{Binding CheckStatus}" Foreground="#BDBDBD"
                                               VerticalAlignment="Center" Margin="12,0,0,0" />
                                </StackPanel>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>
```

- [ ] **Step 4: MainWindow.xaml.cs — käsittelijät** (ResetThresholds_Clickin viereen)

```csharp
    /// <summary>Ylläpito-välilehden tiedot ladataan vasta kun välilehti avataan.</summary>
    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged kuplii myös välilehtien sisältä (esim. ComboBoxit) —
        // reagoidaan vain TabControlin omaan valintaan.
        if (ReferenceEquals(e.Source, MainTabs) && MainTabs.SelectedItem == MaintenanceTab)
        {
            _viewModel.EnsureMaintenanceLoaded();
        }
    }

    private void CopyModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MaintenanceRowViewModel row })
        {
            try
            {
                Clipboard.SetText(row.Model);
            }
            catch (Exception)
            {
                // Leikepöytä voi olla toisen prosessin varaama — kopiointi vain jää tekemättä.
            }
        }
    }

    private void OpenVendorPage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MaintenanceRowViewModel row } &&
            row.Url is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(row.Url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowError(UiStrings.Dlg_SaveFailed, ex);
            }
        }
    }

    /// <summary>Manuaalinen tarkistus: tulos statustekstiin, löytynyt päivitys dialogiin.</summary>
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        try
        {
            UpdateCheckResult result = await _viewModel.CheckForUpdatesAsync(manual: true);
            _viewModel.Maintenance.ReportCheckResult(result.Outcome);
            if (result is { Outcome: UpdateCheckOutcome.UpdateAvailable, Update: not null })
            {
                _pendingUpdate = result.Update;
                ShowPendingUpdateDialog();
            }
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }
```

- [ ] **Step 5: Lokalisointi** — molemmat resx:t + accessorit:

| Avain | fi | en |
|---|---|---|
| Tab_Maintenance | Ylläpito | Maintenance |
| Maint_Intro | Laitteiden nykyiset versiot ja valmistajien tukisivut. Sovellus ei tarkista laitepäivityksiä automaattisesti — avaa valmistajan sivu ja vertaa versiota siellä näkyvään uusimpaan. | Current device versions and vendor support pages. The app does not check device updates automatically — open the vendor page and compare with the latest version shown there. |
| Maint_Motherboard | Emolevy (BIOS) | Motherboard (BIOS) |
| Maint_Gpu | Näytönohjain | GPU |
| Maint_Disk | Levy | Disk |
| Maint_BiosFormat | BIOS {0} | BIOS {0} |
| Maint_DriverFormat | Ajuri {0} | Driver {0} |
| Maint_FirmwareFormat | Firmware {0} | Firmware {0} |
| Maint_CopyModel | Kopioi malli | Copy model |
| Maint_OpenVendorPage | Valmistajan tukisivu | Vendor support page |
| Maint_AppVersion | Hardware Monitor {0} | Hardware Monitor {0} |
| Maint_CheckNow | Tarkista päivitykset | Check for updates |
| Maint_CheckUpToDate | Uusin versio on käytössä (tarkistettu {0}) | You have the latest version (checked {0}) |
| Maint_CheckFound | Uusi versio saatavilla (tarkistettu {0}) | New version available (checked {0}) |
| Maint_CheckFailed | Tarkistus epäonnistui ({0}) | Check failed ({0}) |

Accessorit `UiStrings.cs`:ään samaan tapaan kuin Task 4:ssä (yksi `T(nameof(...))`-rivi per avain; `Tab_Maintenance` muiden Tab_-avainten viereen).

- [ ] **Step 6: Buildaa ja testaa**

Run: `dotnet build HardwareMonitor.sln` → 0 virhettä; `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → vihreä.

- [ ] **Step 7: Commit**

```bash
git add src/HardwareMonitor.App
git commit -m "Lisää Ylläpito-välilehti: laiteversiot, valmistajalinkit ja manuaalitarkistus"
```

---

### Task 10: Ajonaikainen todennus (verify-skillin mukaisesti)

**Files:** ei pysyviä muutoksia (väliaikainen versiomuutos perutaan).

- [ ] **Step 1:** Sulje mahdollinen repo-buildin exe. `dotnet build HardwareMonitor.sln` ja käynnistä `src\HardwareMonitor.App\bin\Debug\net8.0-windows\HardwareMonitor.exe` korotettuna.
- [ ] **Step 2:** Ylläpito-välilehti (käyttäjä klikkaa UIPI:n takia TAI todenna kuvakaappauksella): rivit Emolevy/Näytönohjain/3 levyä, BIOS-versio ja ajuriversio eivät ole "—", NVIDIA-versio muodossa xxx.xx, linkkinapit aktiivisia ASUS/NVIDIA/Samsung-riveillä.
- [ ] **Step 3:** "Tarkista päivitykset" → status "Uusin versio on käytössä" (repo 1.0.5 = julkaistu 1.0.5). debug.logissa ei virherivejä.
- [ ] **Step 4:** Päivityspolun päästä päähän -testi: muuta `HardwareMonitor.App.csproj` väliaikaisesti `<Version>1.0.4</Version>` → build → käynnistä → odota ~35 s → balloon "Päivitys saatavilla" ilmestyy → klikkaus avaa dialogin (muutosteksti näkyy) → "Asenna nyt" lataa v1.0.5-setupin, allekirjoitus hyväksytään ja Inno-installeri käynnistyy → PERU asennus. Todenna debug.logista rivi "Käynnistetään päivitysasennus 1.0.5".
- [ ] **Step 5:** Palauta versio: `git checkout -- src/HardwareMonitor.App/HardwareMonitor.App.csproj`. Sulje dev-exe siististi (verify-skillin WM_CLOSE-tapa; MinimizeToTray-huomio).
- [ ] **Step 6:** Aja koko testisetti vielä kerran: `dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj` → kaikki vihreitä.

---

### Task 11: Julkaisu v1.0.6 + dokumentit

**Files:**
- Modify: `src/HardwareMonitor.App/HardwareMonitor.App.csproj` (`<Version>1.0.6</Version>`)
- Modify: `installer/setup.iss` (`MyAppVersion "1.0.6"`)
- Modify: `HANDOFF.md`, `docs/ROADMAP.md`

- [ ] **Step 1:** Nosta versio KAHTEEN paikkaan (csproj + setup.iss).
- [ ] **Step 2:** `.\tools\release.ps1` (tyhjentää publishin, tarkistaa versiot ja lisenssit, allekirjoittaa, kääntää ISCC:llä, allekirjoittaa setupin).
- [ ] **Step 3:** Commit + release. Releasen kuvausteksti kirjoitetaan SUOMEKSI ja huolella — se näkyy jatkossa sovelluksen omassa päivitysdialogissa ("Mitä uutta"):

```bash
git add -A
git commit -m "Julkaise v1.0.6: päivitysilmoitus ja Ylläpito-välilehti"
gh release create v1.0.6 "installer/Output/HardwareMonitor-Setup-1.0.6.exe" --target main --title "v1.0.6" --notes "Uutta:
- Sovellus tarkistaa uudet versiot GitHubista ja ilmoittaa niistä (asetuksista pois kytkettävissä). Asennus yhdellä klikkauksella allekirjoitustarkistuksen kera.
- Uusi Ylläpito-välilehti: emolevyn BIOS-versio, näytönohjaimen ajuriversio ja levyjen firmware + linkit valmistajien tukisivuille."
```

- [ ] **Step 4:** Asenna: käynnistä `installer/Output/HardwareMonitor-Setup-1.0.6.exe` (Restart Manager sulkee ajossa olevan v1.0.5:n). Todenna asennuksen jälkeen: sovellus ajossa, Ylläpito-välilehti toimii, debug.logissa ei virheitä.
- [ ] **Step 5:** Päivitä `HANDOFF.md` (v1.0.6:n sisältö, uudet sudenkuopat: User-Agent-vaatimus, WinVerifyTrust, System.Management vain Appissa, WMI-ajuriversion NVIDIA-muunnos) ja `docs/ROADMAP.md` (ominaisuudet valmiit).
- [ ] **Step 6:** Commit + push molempiin haaroihin:

```bash
git add HANDOFF.md docs/ROADMAP.md
git commit -m "Päivitä HANDOFF ja ROADMAP v1.0.6:n jälkeen"
git push origin HEAD:claude/windows-11-program-setup-rxuyhn HEAD:main
```

---

## Self-review-muistiinpanot

- Spec-kattavuus: osa A = Taskit 1–5, osa B = Taskit 6–9, virhepolut (offline/rate limit → Task 4 hiljainen null; epäkelpo allekirjoitus → Task 5; WMI-virhe → Task 8; puuttuva asset → Task 5 Upd_NoAsset), todennus = Task 10, julkaisu = Task 11.
- Tyyppien nimet yhtenäiset: `UpdateInfo`, `UpdateCheckOutcome`, `UpdateCheckResult`, `DeviceVersions`, `MaintenanceRowViewModel` — määritelty kukin täsmälleen yhdessä taskissa, myöhemmät kuluttavat.
- `MachineSpec.DiskNames` varmistettu MachineSpec.cs:stä (14.7.2026).
