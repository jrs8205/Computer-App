using System.Text;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Reports;

/// <summary>
/// "Luo raportti" (määrittelyn luku 20). Raportti on tarkoituksella
/// selkokielinen: se luetaan Notepadissa sellaisenaan, joten ei taulukoita
/// vaan otsikot ja kokonaiset lauseet, ja jokainen arvo saa selityksen
/// ("kunnossa", "kävi lähellä varoitusrajaa"). Lopussa sanasto.
/// Puhdas funktio — kaikki data annetaan parametreina.
/// </summary>
public static class ReportBuilder
{
    /// <summary>Näin montaa yksikköä lähempänä varoitusrajaa arvo on "lähellä rajaa".</summary>
    private const double NearLimitMargin = 10;

    public static string Build(
        DateTimeOffset now,
        RiskAssessment assessment,
        KeyMetrics metrics,
        MetricStates states,
        SampleStats dayStats,
        SampleStats monthStats,
        IReadOnlyList<EventRow> dayEvents,
        ThresholdSettings limits)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Järjestelmäraportti — {now:d.M.yyyy 'klo' HH.mm}");
        sb.AppendLine();
        sb.AppendLine("Tämän raportin on tuottanut Hardware Monitor -ohjelma. Se kertoo koneen");
        sb.AppendLine("kunnon tavallisella kielellä: ensin kokonaisarvio, sitten mittarien arvot");
        sb.AppendLine("selityksineen ja lopussa sanasto, joka avaa tekniset termit.");
        sb.AppendLine();

        AppendSummary(sb, assessment);
        AppendCurrentValues(sb, metrics, states, limits);
        AppendDayPeaks(sb, dayStats, limits);
        AppendMonthLevels(sb, monthStats);
        AppendEvents(sb, dayEvents);
        AppendGlossary(sb);

        return sb.ToString();
    }

    private static void AppendSummary(StringBuilder sb, RiskAssessment a)
    {
        sb.AppendLine("## Yhteenveto");
        sb.AppendLine();
        sb.AppendLine($"Koneen tila: {a.Status}");
        sb.AppendLine($"Riskitaso: {a.RiskLevel}");
        sb.AppendLine();
        sb.AppendLine("Mitä tasot tarkoittavat: Hyvä = kaikki arvot ovat normaalilla tasolla.");
        sb.AppendLine("Varoitus = jokin arvo on ylittänyt varoitusrajan tai koneessa on ollut");
        sb.AppendLine("huomiota vaativia tapahtumia. Kriittinen = jokin asia vaatii toimia heti.");
        sb.AppendLine();
        sb.AppendLine("Havainnot:");
        foreach (string observation in a.Observations)
        {
            sb.AppendLine($"- {observation}");
        }

        if (a.Recommendation is { } recommendation)
        {
            sb.AppendLine();
            sb.AppendLine($"Suositus: {recommendation}");
        }

        sb.AppendLine();
    }

    private static void AppendCurrentValues(
        StringBuilder sb, KeyMetrics m, MetricStates states, ThresholdSettings limits)
    {
        sb.AppendLine("## Arvot juuri nyt");
        sb.AppendLine();

        AppendValueLine(sb, "CPU-lämpötila", m.CpuPackageTempC, "°C",
            states.CpuTemp, limits.CpuWarningTemp);
        AppendValueLine(sb, "GPU-lämpötila", m.GpuTempC, "°C",
            states.GpuTemp, limits.GpuWarningTemp);
        AppendValueLine(sb, "GPU hotspot", m.GpuHotspotTempC, "°C",
            states.GpuHotspot, limits.GpuHotspotWarningTemp);
        AppendValueLine(sb, "RAM-käyttö", m.RamLoadPercent, "%",
            states.RamLoad, limits.RamWarningPercent);

        for (int i = 0; i < m.Disks.Count; i++)
        {
            ThresholdState state = i < states.Disks.Count
                ? states.Disks[i]
                : ThresholdState.Normal;
            AppendValueLine(sb, $"Levy {m.Disks[i].Name.Trim()}", m.Disks[i].TemperatureC, "°C",
                state, limits.NvmeWarningTemp);
        }

        sb.AppendLine();
    }

    private static void AppendValueLine(
        StringBuilder sb, string label, float? value, string unit,
        ThresholdState state, float warnLimit)
    {
        if (value is not { } v)
        {
            sb.AppendLine($"- {label}: ei lukemaa");
            return;
        }

        string verdict = state switch
        {
            ThresholdState.Critical => "KRIITTINEN: kriittinen raja on ylittynyt",
            ThresholdState.Warning => "VAROITUS: varoitusraja on ylittynyt",
            _ => $"kunnossa (varoitusraja {warnLimit:0} {unit})",
        };
        sb.AppendLine($"- {label}: {v:0} {unit} — {verdict}");
    }

    private static void AppendDayPeaks(StringBuilder sb, SampleStats stats, ThresholdSettings limits)
    {
        sb.AppendLine("## Viimeiset 24 tuntia");
        sb.AppendLine();

        if (stats.SampleCount == 0)
        {
            sb.AppendLine("Historiaa ei ole vielä kertynyt tältä ajalta.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("Korkeimmat lukemat:");
        AppendPeakLine(sb, "CPU-lämpötila", stats.CpuTemp.Max, "°C", limits.CpuWarningTemp);
        AppendPeakLine(sb, "GPU-lämpötila", stats.GpuTemp.Max, "°C", limits.GpuWarningTemp);
        AppendPeakLine(sb, "GPU hotspot", stats.GpuHotspot.Max, "°C", limits.GpuHotspotWarningTemp);
        AppendPeakLine(sb, "RAM-käyttö", stats.RamLoad.Max, "%", limits.RamWarningPercent);
        foreach (DiskStat disk in stats.Disks)
        {
            AppendPeakLine(sb, $"Levy {disk.Name.Trim()}", disk.TempMax, "°C", limits.NvmeWarningTemp);
        }

        sb.AppendLine();
    }

    private static void AppendPeakLine(
        StringBuilder sb, string label, double? max, string unit, float warnLimit)
    {
        if (max is not { } v)
        {
            return;
        }

        string comparison = v >= warnLimit
            ? $"ylitti varoitusrajan ({warnLimit:0} {unit})"
            : warnLimit - v <= NearLimitMargin
                ? $"kävi lähellä varoitusrajaa ({warnLimit:0} {unit})"
                : $"jäi selvästi alle varoitusrajan ({warnLimit:0} {unit})";
        sb.AppendLine($"- {label}: enimmillään {v:0} {unit} — {comparison}");
    }

    private static void AppendMonthLevels(StringBuilder sb, SampleStats stats)
    {
        sb.AppendLine("## Viimeiset 30 päivää (normaalitaso)");
        sb.AppendLine();

        if (stats.SampleCount == 0)
        {
            sb.AppendLine("Historiaa ei ole vielä kertynyt.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("Kone on tyypillisesti toiminut näissä lukemissa:");
        AppendLevelLine(sb, "CPU-lämpötila", stats.CpuTemp, "°C");
        AppendLevelLine(sb, "GPU-lämpötila", stats.GpuTemp, "°C");
        AppendLevelLine(sb, "GPU hotspot", stats.GpuHotspot, "°C");
        AppendLevelLine(sb, "RAM-käyttö", stats.RamLoad, "%");
        foreach (DiskStat disk in stats.Disks)
        {
            if (disk.TempAvg is { } avg && disk.TempMax is { } max)
            {
                sb.AppendLine($"- Levy {disk.Name.Trim()}: keskimäärin {avg:0} °C, korkeimmillaan {max:0} °C");
            }
        }

        sb.AppendLine();
    }

    private static void AppendLevelLine(StringBuilder sb, string label, MetricStat stat, string unit)
    {
        if (stat.Avg is { } avg && stat.Max is { } max)
        {
            sb.AppendLine($"- {label}: keskimäärin {avg:0} {unit}, korkeimmillaan {max:0} {unit}");
        }
    }

    private static void AppendEvents(StringBuilder sb, IReadOnlyList<EventRow> events)
    {
        sb.AppendLine("## Tapahtumat viimeisen 24 tunnin ajalta");
        sb.AppendLine();

        List<EventRow> important = events.Where(e => e.Level != "INFO").ToList();
        if (important.Count == 0)
        {
            sb.AppendLine("Ei varoituksia eikä kriittisiä tapahtumia — hyvä merkki.");
        }
        else
        {
            foreach (EventRow e in important)
            {
                string level = e.Level switch
                {
                    "CRITICAL" => "KRIITTINEN",
                    "WARNING" => "VAROITUS",
                    _ => "VIRHE",
                };
                sb.AppendLine($"- {e.Timestamp.ToLocalTime():d.M. 'klo' HH.mm} [{level}] {e.Message}");
            }

            sb.AppendLine();
            sb.AppendLine("(Tavalliset tiedotusrivit, kuten ohjelman käynnistykset, on jätetty pois.)");
        }

        sb.AppendLine();
    }

    private static void AppendGlossary(StringBuilder sb)
    {
        sb.AppendLine("## Sanasto");
        sb.AppendLine();
        sb.AppendLine("- Varoitusraja ja kriittinen raja: ohjelmaan asetetut lämpö- ja");
        sb.AppendLine("  käyttörajat. Varoitusrajan ylitys kannattaa huomioida; kriittisen");
        sb.AppendLine("  rajan ylitys vaatii toimia.");
        sb.AppendLine("- GPU hotspot: näytönohjaimen kuumin yksittäinen mittauspiste — aina");
        sb.AppendLine("  korkeampi kuin GPU:n yleislämpötila, ja se saa ollakin.");
        sb.AppendLine("- WHEA: Windowsin kirjaama laitteistovirhe (esim. muisti, PCIe tai");
        sb.AppendLine("  suoritin). Yksittäinen korjattu virhe ei kaada konetta, mutta");
        sb.AppendLine("  toistuvat virheet viittaavat rauta- tai kellotusongelmaan.");
        sb.AppendLine("- Kernel-Power 41: Windowsin merkintä siitä, että kone sammui");
        sb.AppendLine("  edellisellä kerralla yllättäen (kaatuminen tai virran katkeaminen).");
        sb.AppendLine("- BSOD / BugCheck: \"sininen ruutu\" eli Windowsin kaatuminen.");
        sb.AppendLine("- TDR / näyttöajurivirhe: näytönohjaimen ajuri lakkasi hetkeksi");
        sb.AppendLine("  vastaamasta ja Windows palautti sen. Satunnaisena harmiton,");
        sb.AppendLine("  toistuvana syytä päivittää ajuri.");
        sb.AppendLine("- RPM: tuulettimen kierrosnopeus minuutissa.");
    }
}
