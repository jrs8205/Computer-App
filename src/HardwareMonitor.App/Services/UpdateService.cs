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
