using System.Text;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Insights;

/// <summary>
/// Rakentaa konetuntemus-lokin (machine-insights.md): jatkuvasti päivittyvä
/// yhteenveto koneen normaalitasoista, huipuista, tapahtumista ja
/// optimointiehdotuksista. Tarkoitettu sekä käyttäjän että tekoälyavustajan
/// (Claude) luettavaksi tulevissa istunnoissa. Puhdas funktio — testattava.
/// </summary>
public static class MachineInsightsBuilder
{
    private static readonly HashSet<string> WindowsDiskProviders =
        new(StringComparer.OrdinalIgnoreCase) { "disk", "Ntfs", "storahci", "stornvme" };

    public static string Build(
        DateTimeOffset now,
        SampleStats stats,
        IReadOnlyList<EventRow> events,
        ThresholdSettings limits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Konetuntemus-loki — Hardware Monitor");
        sb.AppendLine();
        sb.AppendLine($"Päivitetty: {now:d.M.yyyy 'klo' HH.mm}. Generoitu automaattisesti");
        sb.AppendLine("sensorihistoriasta (30 pv). Tiedosto on tarkoitettu sekä käyttäjälle");
        sb.AppendLine("että tekoälyavustajalle (Claude) koneen tuntemiseen ja optimointiin.");
        sb.AppendLine();

        if (stats.SampleCount == 0)
        {
            sb.AppendLine("Ei vielä riittävästi dataa — historia karttuu ohjelman ollessa käynnissä.");
            return sb.ToString();
        }

        AppendLevels(sb, stats, limits);
        (int whea, int crashes, int thresholds, int gpuDriver, int winDisk) = CountEvents(events);
        AppendEvents(sb, whea, crashes, thresholds, gpuDriver, winDisk);
        AppendInsights(sb, stats, limits, whea, crashes, winDisk);

        return sb.ToString();
    }

    private static void AppendLevels(StringBuilder sb, SampleStats stats, ThresholdSettings limits)
    {
        sb.AppendLine("## Normaalitasot ja huiput (30 pv)");
        sb.AppendLine();
        sb.AppendLine("| Mittari | Keskiarvo | Huippu | Varoitusraja |");
        sb.AppendLine("|---|---|---|---|");
        AppendRow(sb, "CPU-lämpötila", stats.CpuTemp, "°C", limits.CpuWarningTemp);
        AppendRow(sb, "CPU-kuorma", stats.CpuLoad, "%", null);
        AppendRow(sb, "GPU-lämpötila", stats.GpuTemp, "°C", limits.GpuWarningTemp);
        AppendRow(sb, "GPU hotspot", stats.GpuHotspot, "°C", limits.GpuHotspotWarningTemp);
        AppendRow(sb, "RAM-käyttö", stats.RamLoad, "%", limits.RamWarningPercent);
        sb.AppendLine();

        if (stats.Disks.Count > 0)
        {
            sb.AppendLine("### Levyt");
            sb.AppendLine();
            sb.AppendLine("| Levy | Keskilämpö | Huippu | Varoitusraja |");
            sb.AppendLine("|---|---|---|---|");
            foreach (DiskStat disk in stats.Disks)
            {
                sb.AppendLine(
                    $"| {disk.Name} | {Fmt(disk.TempAvg, "°C")} | {Fmt(disk.TempMax, "°C")} " +
                    $"| {limits.NvmeWarningTemp:0} °C |");
            }

            sb.AppendLine();
        }

        if (stats.Fans.Count > 0)
        {
            sb.AppendLine("### Tuulettimet");
            sb.AppendLine();
            sb.AppendLine("| Tuuletin | RPM keskimäärin | RPM huippu |");
            sb.AppendLine("|---|---|---|");
            foreach (FanStat fan in stats.Fans)
            {
                sb.AppendLine($"| {fan.Name} | {Fmt(fan.RpmAvg, "")} | {Fmt(fan.RpmMax, "")} |");
            }

            sb.AppendLine();
        }
    }

    private static void AppendRow(
        StringBuilder sb, string label, MetricStat stat, string unit, float? warnLimit)
    {
        string limit = warnLimit is { } w ? $"{w:0} {unit}" : "—";
        sb.AppendLine($"| {label} | {Fmt(stat.Avg, unit)} | {Fmt(stat.Max, unit)} | {limit} |");
    }

    private static (int Whea, int Crashes, int Thresholds, int GpuDriver, int WinDisk)
        CountEvents(IReadOnlyList<EventRow> events)
    {
        int whea = 0, crashes = 0, thresholds = 0, gpuDriver = 0, winDisk = 0;
        foreach (EventRow e in events)
        {
            if (e.Level == "INFO")
            {
                continue;
            }

            switch (e.Component)
            {
                case "Laitteisto": whea++; break;
                case "Järjestelmä": crashes++; break;
                case "GPU-ajuri": gpuDriver++; break;
                case "Levy" when e.Sensor is { } s && WindowsDiskProviders.Contains(s):
                    winDisk++;
                    break;
                default: thresholds++; break;
            }
        }

        return (whea, crashes, thresholds, gpuDriver, winDisk);
    }

    private static void AppendEvents(
        StringBuilder sb, int whea, int crashes, int thresholds, int gpuDriver, int winDisk)
    {
        sb.AppendLine("## Tapahtumat (30 pv)");
        sb.AppendLine();
        sb.AppendLine($"- WHEA-rautavirheitä: {whea}");
        sb.AppendLine($"- Yllättäviä sammutuksia tai kaatumisia: {crashes}");
        sb.AppendLine($"- Raja-arvoylityksiä: {thresholds}");
        sb.AppendLine($"- Näyttöajurivirheitä: {gpuDriver}");
        sb.AppendLine($"- Windowsin levyvirheitä: {winDisk}");
        sb.AppendLine();
    }

    private static void AppendInsights(
        StringBuilder sb, SampleStats stats, ThresholdSettings limits,
        int whea, int crashes, int winDisk)
    {
        sb.AppendLine("## Havainnot ja optimointiehdotukset");
        sb.AppendLine();

        var insights = new List<string>();

        if (stats.CpuTemp.Max is { } cpuMax && cpuMax >= limits.CpuWarningTemp)
        {
            insights.Add(
                $"CPU:n huippulämpö {cpuMax:0} °C on käynyt varoitusrajalla ({limits.CpuWarningTemp:0} °C) " +
                "— tarkista jäähdytys ja pölyt, harkitse tuulettimien käyrien säätöä.");
        }

        if (stats.GpuHotspot.Max is { } hotMax && hotMax >= limits.GpuHotspotWarningTemp)
        {
            insights.Add(
                $"GPU hotspot on käynyt {hotMax:0} °C:ssa (varoitusraja {limits.GpuHotspotWarningTemp:0} °C) " +
                "— tarkista kotelon ilmavirta ja GPU-tuulettimet.");
        }

        if (stats.RamLoad.Max is { } ramMax && ramMax >= limits.RamWarningPercent)
        {
            insights.Add(
                $"RAM-käyttö on käynyt {ramMax:0} %:ssa (varoitusraja {limits.RamWarningPercent:0} %) " +
                "— muisti on ajoittain vähissä.");
        }

        foreach (DiskStat disk in stats.Disks)
        {
            if (disk.TempMax is { } max && max >= limits.NvmeWarningTemp)
            {
                insights.Add(
                    $"Levyn {disk.Name} huippulämpö {max:0} °C ylittää varoitusrajan " +
                    $"({limits.NvmeWarningTemp:0} °C) — harkitse M.2-jäähdytyslevyä tai kotelon " +
                    "ilmavirran parantamista pitkien kirjoitusten ajaksi.");
            }
        }

        if (whea > 0)
        {
            insights.Add(
                $"WHEA-rautavirheitä ({whea} kpl 30 pv) — viittaa mahdolliseen rauta- tai " +
                "kellotusongelmaan: tarkista XMP-profiili, jännitteet ja ajurit.");
        }

        if (crashes > 0)
        {
            insights.Add(
                $"Yllättäviä sammutuksia tai kaatumisia ({crashes} kpl 30 pv) — seuraa " +
                "lämpötiloja kuormassa ja tarkista virransyöttö.");
        }

        if (winDisk > 0)
        {
            insights.Add(
                $"Windowsin levyvirheitä ({winDisk} kpl 30 pv) — ota varmuuskopiot ja " +
                "tarkista levyn SMART-kunto.");
        }

        if (insights.Count == 0)
        {
            insights.Add("Ei merkittäviä ongelmia havaittu — arvot ovat normaalitasolla.");
        }

        foreach (string insight in insights)
        {
            sb.AppendLine($"- {insight}");
        }
    }

    private static string Fmt(double? value, string unit) =>
        value is { } v ? $"{v:0} {unit}".TrimEnd() : "—";
}
