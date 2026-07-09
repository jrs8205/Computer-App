using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Analysis;

/// <summary>Selkokielinen kokonaisarvio koneen tilasta (määrittelyn luku 19).</summary>
public sealed record RiskAssessment(
    ThresholdState Level,
    string Status,
    string RiskLevel,
    int Score,
    IReadOnlyList<string> Observations,
    string? Recommendation);

/// <summary>
/// Riskipisteytys ja havainnot (luvut 19 ja 31): nykyiset mittaritilat,
/// viimeisen 24 h tapahtumat ja huiput sekä edellisen istunnon kaatuminen
/// muunnetaan yhdeksi tilaksi (Hyvä/Varoitus/Kriittinen), riskitasoksi
/// (Matala/Kohonnut/Korkea) ja selkokielisiksi havainnoiksi.
/// Puhdas funktio — kaikki data annetaan parametreina.
/// </summary>
public static class RiskAnalyzer
{
    // Windows-lokin levyprovider erottaa Windows-levyvirheen (sensor=provider)
    // oman raja-arvovalvonnan lämpötapahtumasta (sensor=levyn nimi).
    private static readonly HashSet<string> WindowsDiskProviders =
        new(StringComparer.OrdinalIgnoreCase) { "disk", "Ntfs", "storahci", "stornvme" };

    /// <summary>Kaatumistapahtuman sensor-merkintä — ei pisteytetä lipun lisäksi.</summary>
    public const string LastStateSensor = "last_state";

    public static RiskAssessment Assess(
        MetricStates states,
        KeyMetrics metrics,
        ThresholdSettings limits,
        IReadOnlyList<EventRow> recentEvents,
        SampleStats? dayStats,
        bool previousSessionCrashed)
    {
        var observations = new List<string>();

        // Pisteet kategorioittain — suositus valitaan suurimman mukaan.
        int thermal = 0, whea = 0, system = 0, winDisk = 0, gpuDriver = 0, crash = 0;

        if (previousSessionCrashed)
        {
            crash += 10;
            observations.Add("Edellinen istunto päättyi yllättäen");
        }

        thermal += CurrentStateScore(states.CpuTemp);
        AddExceedance(observations, states.CpuTemp, "CPU-lämpötila",
            metrics.CpuPackageTempC, limits.CpuWarningTemp, limits.CpuCriticalTemp, "°C");

        thermal += CurrentStateScore(states.GpuTemp);
        AddExceedance(observations, states.GpuTemp, "GPU-lämpötila",
            metrics.GpuTempC, limits.GpuWarningTemp, limits.GpuCriticalTemp, "°C");

        thermal += CurrentStateScore(states.GpuHotspot);
        AddExceedance(observations, states.GpuHotspot, "GPU hotspot -lämpötila",
            metrics.GpuHotspotTempC, limits.GpuHotspotWarningTemp, limits.GpuHotspotCriticalTemp, "°C");

        thermal += CurrentStateScore(states.RamLoad);
        AddExceedance(observations, states.RamLoad, "RAM-käyttö",
            metrics.RamLoadPercent, limits.RamWarningPercent, limits.RamCriticalPercent, "%");

        for (int i = 0; i < states.Disks.Count; i++)
        {
            thermal += CurrentStateScore(states.Disks[i]);
            string name = i < metrics.Disks.Count ? metrics.Disks[i].Name : $"Levy {i + 1}";
            float? temp = i < metrics.Disks.Count ? metrics.Disks[i].TemperatureC : null;
            AddExceedance(observations, states.Disks[i], $"Levyn {name} lämpötila",
                temp, limits.NvmeWarningTemp, limits.NvmeCriticalTemp, "°C");
        }

        // Tuuletinsääntö näkyy vain Worst-tilassa (ei omana mittarina).
        ThresholdState individualWorst = states.Disks
            .Append(states.CpuTemp).Append(states.GpuTemp)
            .Append(states.GpuHotspot).Append(states.RamLoad).Max();
        if (states.Worst > individualWorst)
        {
            thermal += CurrentStateScore(states.Worst);
            observations.Add("Tuuletinongelma: istunnossa pyörinyt tuuletin on pysähtynyt CPU:n ollessa kuuma");
        }

        AddDayPeaks(observations, dayStats);

        int wheaCount = 0, systemCount = 0, gpuDriverCount = 0, winDiskCount = 0, thresholdCount = 0;
        foreach (EventRow e in recentEvents)
        {
            if (e.Level == "INFO" || e.Sensor == LastStateSensor)
            {
                continue;
            }

            bool critical = e.Level == "CRITICAL";
            switch (e.Component)
            {
                case "Laitteisto":
                    whea += critical ? 15 : 6;
                    wheaCount++;
                    break;
                case "Järjestelmä":
                    system += critical ? 15 : 6;
                    systemCount++;
                    break;
                case "GPU-ajuri":
                    gpuDriver += 6;
                    gpuDriverCount++;
                    break;
                case "Levy" when e.Sensor is { } s && WindowsDiskProviders.Contains(s):
                    winDisk += 10;
                    winDiskCount++;
                    break;
                default: // oman valvonnan raja-arvotapahtumat (CPU/GPU/RAM/Levy/Tuuletin)
                    thermal += critical ? 6 : 2;
                    thresholdCount++;
                    break;
            }
        }

        observations.Add(wheaCount > 0
            ? $"WHEA-rautavirheitä 24 h: {wheaCount}"
            : "Ei WHEA-virheitä (24 h)");
        observations.Add(systemCount > 0
            ? $"Yllättäviä sammutuksia tai kaatumisia 24 h: {systemCount}"
            : "Ei yllättäviä sammutuksia (24 h)");

        if (gpuDriverCount > 0)
        {
            observations.Add($"Näyttöajurivirheitä 24 h: {gpuDriverCount}");
        }

        if (winDiskCount > 0)
        {
            observations.Add($"Levyvirheitä Windows-lokissa 24 h: {winDiskCount}");
        }

        if (thresholdCount > 0)
        {
            observations.Add($"Raja-arvoylityksiä 24 h: {thresholdCount}");
        }

        int score = thermal + whea + system + winDisk + gpuDriver + crash;
        ThresholdState level = score >= 15 ? ThresholdState.Critical
            : score >= 5 ? ThresholdState.Warning
            : ThresholdState.Normal;

        string? recommendation = level == ThresholdState.Normal
            ? null
            : PickRecommendation(thermal, whea, system + crash, winDisk, gpuDriver);

        return new RiskAssessment(
            level,
            level switch
            {
                ThresholdState.Critical => "Kriittinen",
                ThresholdState.Warning => "Varoitus",
                _ => "Hyvä",
            },
            level switch
            {
                ThresholdState.Critical => "Korkea",
                ThresholdState.Warning => "Kohonnut",
                _ => "Matala",
            },
            score,
            observations,
            recommendation);
    }

    private static int CurrentStateScore(ThresholdState state) => state switch
    {
        ThresholdState.Critical => 15,
        ThresholdState.Warning => 5,
        _ => 0,
    };

    private static void AddExceedance(
        List<string> observations, ThresholdState state, string label,
        float? value, float warnLimit, float critLimit, string unit)
    {
        if (state == ThresholdState.Normal || value is not { } v)
        {
            return;
        }

        bool critical = state == ThresholdState.Critical;
        string limitText = critical ? "kriittisen rajan" : "varoitusrajan";
        float limit = critical ? critLimit : warnLimit;
        observations.Add($"{label} {v:0} {unit} ylittää {limitText} ({limit:0} {unit})");
    }

    private static void AddDayPeaks(List<string> observations, SampleStats? stats)
    {
        if (stats is not { SampleCount: > 0 })
        {
            return;
        }

        if (stats.CpuTemp.Max is { } cpu)
        {
            observations.Add($"CPU-lämpötila kävi korkeimmillaan {cpu:0} °C (24 h)");
        }

        if (stats.GpuHotspot.Max is { } hotspot)
        {
            observations.Add($"GPU hotspot kävi korkeimmillaan {hotspot:0} °C (24 h)");
        }

        if (stats.RamLoad.Max is { } ram)
        {
            observations.Add($"RAM-käyttö nousi korkeimmillaan {ram:0} % (24 h)");
        }
    }

    private static string PickRecommendation(
        int thermal, int whea, int system, int winDisk, int gpuDriver)
    {
        int max = Math.Max(thermal, Math.Max(whea, Math.Max(system, Math.Max(winDisk, gpuDriver))));

        // Tasapelissä vakavin syy ensin: rauta > levy > lämpö > järjestelmä > ajuri.
        if (whea == max)
        {
            return "Mahdollinen rautaongelma: tarkista ylikellotus/XMP-profiili, muistikammat ja ajurit.";
        }

        if (winDisk == max)
        {
            return "Ota varmuuskopiot tärkeistä tiedoista ja tarkista levyn kunto (SMART).";
        }

        if (thermal == max)
        {
            return "Tarkista jäähdytys: tuulettimien toiminta, pölyt ja kotelon ilmankierto.";
        }

        if (system == max)
        {
            return "Kone on kaatunut tai sammunut yllättäen — seuraa lämpötiloja ja tarkista virransyöttö.";
        }

        return "Päivitä näytönohjaimen ajuri puhtaalla asennuksella.";
    }
}
