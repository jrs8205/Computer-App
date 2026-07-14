using HardwareMonitor.Core.Updates;
using Xunit;

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
