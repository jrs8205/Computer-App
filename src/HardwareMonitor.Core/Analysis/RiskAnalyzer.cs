using HardwareMonitor.Core.Localization;
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
            observations.Add(Strings.Risk_PrevSessionCrashed);
        }

        thermal += CurrentStateScore(states.CpuTemp);
        AddExceedance(observations, states.CpuTemp, Strings.Threshold_LabelCpuTemp,
            metrics.CpuPackageTempC, limits.CpuWarningTemp, limits.CpuCriticalTemp, "°C");

        thermal += CurrentStateScore(states.GpuTemp);
        AddExceedance(observations, states.GpuTemp, Strings.Threshold_LabelGpuTemp,
            metrics.GpuTempC, limits.GpuWarningTemp, limits.GpuCriticalTemp, "°C");

        thermal += CurrentStateScore(states.GpuHotspot);
        AddExceedance(observations, states.GpuHotspot, Strings.Threshold_LabelGpuHotspot,
            metrics.GpuHotspotTempC, limits.GpuHotspotWarningTemp, limits.GpuHotspotCriticalTemp, "°C");

        thermal += CurrentStateScore(states.RamLoad);
        AddExceedance(observations, states.RamLoad, Strings.Threshold_LabelRamLoad,
            metrics.RamLoadPercent, limits.RamWarningPercent, limits.RamCriticalPercent, "%");

        for (int i = 0; i < states.Disks.Count; i++)
        {
            thermal += CurrentStateScore(states.Disks[i]);
            string name = i < metrics.Disks.Count
                ? metrics.Disks[i].Name
                : string.Format(Strings.Risk_DiskFallbackName, i + 1);
            float? temp = i < metrics.Disks.Count ? metrics.Disks[i].TemperatureC : null;
            AddExceedance(observations, states.Disks[i],
                string.Format(Strings.Threshold_LabelDiskTemp, name),
                temp, limits.NvmeWarningTemp, limits.NvmeCriticalTemp, "°C");
        }

        // Tuuletinsääntö näkyy vain Worst-tilassa (ei omana mittarina).
        ThresholdState individualWorst = states.Disks
            .Append(states.CpuTemp).Append(states.GpuTemp)
            .Append(states.GpuHotspot).Append(states.RamLoad).Max();
        if (states.Worst > individualWorst)
        {
            thermal += CurrentStateScore(states.Worst);
            observations.Add(Strings.Risk_FanProblem);
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
            ? string.Format(Strings.Risk_WheaCount, wheaCount)
            : Strings.Risk_NoWhea);
        observations.Add(systemCount > 0
            ? string.Format(Strings.Risk_CrashCount, systemCount)
            : Strings.Risk_NoCrashes);

        if (gpuDriverCount > 0)
        {
            observations.Add(string.Format(Strings.Risk_GpuDriverCount, gpuDriverCount));
        }

        if (winDiskCount > 0)
        {
            observations.Add(string.Format(Strings.Risk_WinDiskCount, winDiskCount));
        }

        if (thresholdCount > 0)
        {
            observations.Add(string.Format(Strings.Risk_ThresholdCount, thresholdCount));
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
                ThresholdState.Critical => Strings.Risk_StatusCritical,
                ThresholdState.Warning => Strings.Risk_StatusWarning,
                _ => Strings.Risk_StatusGood,
            },
            level switch
            {
                ThresholdState.Critical => Strings.Risk_LevelHigh,
                ThresholdState.Warning => Strings.Risk_LevelElevated,
                _ => Strings.Risk_LevelLow,
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
        float limit = critical ? critLimit : warnLimit;
        observations.Add(string.Format(
            critical ? Strings.Risk_ExceedsCritical : Strings.Risk_ExceedsWarning,
            label, v, unit, limit));
    }

    private static void AddDayPeaks(List<string> observations, SampleStats? stats)
    {
        if (stats is not { SampleCount: > 0 })
        {
            return;
        }

        if (stats.CpuTemp.Max is { } cpu)
        {
            observations.Add(string.Format(Strings.Risk_PeakCpu, cpu));
        }

        if (stats.GpuHotspot.Max is { } hotspot)
        {
            observations.Add(string.Format(Strings.Risk_PeakHotspot, hotspot));
        }

        if (stats.RamLoad.Max is { } ram)
        {
            observations.Add(string.Format(Strings.Risk_PeakRam, ram));
        }
    }

    private static string PickRecommendation(
        int thermal, int whea, int system, int winDisk, int gpuDriver)
    {
        int max = Math.Max(thermal, Math.Max(whea, Math.Max(system, Math.Max(winDisk, gpuDriver))));

        // Tasapelissä vakavin syy ensin: rauta > levy > lämpö > järjestelmä > ajuri.
        if (whea == max)
        {
            return Strings.Risk_RecommendWhea;
        }

        if (winDisk == max)
        {
            return Strings.Risk_RecommendDisk;
        }

        if (thermal == max)
        {
            return Strings.Risk_RecommendThermal;
        }

        if (system == max)
        {
            return Strings.Risk_RecommendSystem;
        }

        return Strings.Risk_RecommendGpuDriver;
    }
}
