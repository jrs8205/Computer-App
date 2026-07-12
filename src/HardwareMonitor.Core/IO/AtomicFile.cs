using System.Text;

namespace HardwareMonitor.Core.IO;

/// <summary>
/// Atominen tiedostonkirjoitus: sisältö kirjoitetaan ensin väliaikais-
/// tiedostoon samaan hakemistoon ja korvataan kohde File.Movella. Näin
/// levytila loppuessa tai prosessin kaatuessa aiempi kelvollinen tiedosto
/// säilyy — kohteeksi ei jää tyhjää tai osittaista sisältöä (last_state.json
/// ja machine-insights.md ovat juuri kaatumisen jälkeisen tarkastuksen
/// lähteitä, joten ne eivät saa korruptoitua kesken kirjoituksen).
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content, Encoding encoding)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, encoding);
        File.Move(tempPath, path, overwrite: true);
    }
}
