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
