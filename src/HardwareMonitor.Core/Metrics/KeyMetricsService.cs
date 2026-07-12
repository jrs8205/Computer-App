using HardwareMonitor.Core.Sensors;

namespace HardwareMonitor.Core.Metrics;

/// <summary>
/// Poimii raakasensoripuusta (HardwareGroup-lista) tärkeimmät arvot KeyMetrics-olioksi.
/// Poiminta perustuu sensorityyppiin ja LibreHardwareMonitorin vakiintuneisiin
/// sensorinimiin (esim. "CPU Package", "GPU Hot Spot"). Puhdas funktio: ei tilaa,
/// ei poikkeuksia puuttuvasta datasta.
/// </summary>
public static class KeyMetricsService
{
    public static KeyMetrics Extract(IReadOnlyList<HardwareGroup> groups)
    {
        float? cpuLoad = null, cpuPackageTemp = null, cpuCoreMaxTemp = null,
               cpuClock = null, cpuPower = null;
        float? gpuLoad = null, gpuTemp = null, gpuHotspot = null,
               vramUsed = null, vramTotal = null, gpuPower = null;
        float? ramLoad = null, ramUsed = null, ramAvailable = null;
        var disks = new List<DiskMetrics>();
        var fans = new List<FanMetrics>();
        var gpuGroups = new List<HardwareGroup>();

        foreach (HardwareGroup group in groups)
        {
            switch (group.HardwareType)
            {
                case "Cpu":
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Load" when s.SensorName == "CPU Total":
                                cpuLoad = s.Value;
                                break;
                            case "Temperature" when s.SensorName == "CPU Package":
                                cpuPackageTemp = s.Value;
                                break;
                            case "Temperature" when s.SensorName == "Core Max":
                                cpuCoreMaxTemp = s.Value;
                                break;
                            case "Clock" when s.SensorName.StartsWith("CPU Core", StringComparison.Ordinal):
                                if (s.Value is { } clock && (cpuClock is not { } max || clock > max))
                                {
                                    cpuClock = clock;
                                }
                                break;
                            case "Power" when s.SensorName is "CPU Package" or "Package":
                                cpuPower = s.Value;
                                break;
                        }
                    }
                    break;

                case "Memory":
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Load" when s.SensorName == "Memory":
                                ramLoad = s.Value;
                                break;
                            case "Data" when s.SensorName == "Memory Used":
                                ramUsed = s.Value;
                                break;
                            case "Data" when s.SensorName == "Memory Available":
                                ramAvailable = s.Value;
                                break;
                        }
                    }
                    break;

                case "GpuNvidia" or "GpuAmd" or "GpuIntel":
                    // Hybridikoneessa (iGPU + dGPU) kentät poimitaan vain
                    // yhdestä laitteesta — sekoitus näyttäisi esim. iGPU:n
                    // lämmön dGPU:n kuorman vieressä. Valinta silmukan jälkeen.
                    gpuGroups.Add(group);
                    break;

                case "Storage":
                    float? diskTemp = null, diskTempFallback = null,
                           readActivity = null, writeActivity = null;
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Temperature" when s.SensorName == "Temperature":
                                diskTemp = s.Value;
                                break;
                            // NVMe-levyillä ei aina ole pääsensoria, vaan vain
                            // "Temperature #1", "#2" jne. — ensimmäinen on levyn
                            // yleislämpö, loput ovat ohjaimen sisäisiä pisteitä.
                            case "Temperature" when s.SensorName.StartsWith("Temperature", StringComparison.Ordinal):
                                diskTempFallback ??= s.Value;
                                break;
                            case "Load" when s.SensorName == "Read Activity":
                                readActivity = s.Value;
                                break;
                            case "Load" when s.SensorName == "Write Activity":
                                writeActivity = s.Value;
                                break;
                        }
                    }

                    diskTemp ??= diskTempFallback;

                    // "Total Activity" näyttää joillain levyillä aina 100 % — käytetään
                    // read/write-maksimia, joka kuvaa todellista aktiivisuutta.
                    float? activity = (readActivity, writeActivity) switch
                    {
                        ({ } r, { } w) => Math.Max(r, w),
                        ({ } r, null) => r,
                        (null, { } w) => w,
                        _ => null,
                    };
                    disks.Add(new DiskMetrics(group.Name, diskTemp, activity));
                    break;
            }

            CollectFans(group, fans);
        }

        if (SelectPrimaryGpu(gpuGroups) is { } gpu)
        {
            foreach (SensorReading s in gpu.Sensors)
            {
                switch (s.SensorType)
                {
                    case "Load" when s.SensorName == "GPU Core":
                        gpuLoad = s.Value;
                        break;
                    case "Temperature" when s.SensorName == "GPU Core":
                        gpuTemp = s.Value;
                        break;
                    case "Temperature" when s.SensorName == "GPU Hot Spot":
                        gpuHotspot = s.Value;
                        break;
                    case "SmallData" when s.SensorName == "GPU Memory Used":
                        vramUsed = s.Value;
                        break;
                    case "SmallData" when s.SensorName == "GPU Memory Total":
                        vramTotal = s.Value;
                        break;
                    case "Power" when s.SensorName == "GPU Package":
                        gpuPower = s.Value;
                        break;
                }
            }
        }

        return new KeyMetrics(
            CpuLoadPercent: cpuLoad,
            CpuPackageTempC: cpuPackageTemp ?? cpuCoreMaxTemp,
            CpuMaxClockMhz: cpuClock,
            CpuPackagePowerW: cpuPower,
            GpuLoadPercent: gpuLoad,
            GpuTempC: gpuTemp,
            GpuHotspotTempC: gpuHotspot,
            GpuMemoryUsedMb: vramUsed,
            GpuMemoryTotalMb: vramTotal,
            GpuPowerW: gpuPower,
            RamLoadPercent: ramLoad,
            RamUsedGb: ramUsed,
            RamAvailableGb: ramAvailable,
            Disks: disks,
            Fans: fans);
    }

    /// <summary>
    /// Ensisijainen GPU: erillisnäytönohjain (Nvidia/AMD) ennen Inteliä,
    /// tasapelissä eniten sensoreita tarjoava (erilliskortti on rikkaampi).
    /// </summary>
    private static HardwareGroup? SelectPrimaryGpu(List<HardwareGroup> gpuGroups) =>
        gpuGroups
            .OrderBy(g => g.HardwareType == "GpuIntel" ? 1 : 0)
            .ThenByDescending(g => g.Sensors.Count)
            .FirstOrDefault();

    /// <summary>Kerää Fan-tyyppiset sensorit laitteesta ja sen alalaitteista rekursiivisesti.</summary>
    private static void CollectFans(HardwareGroup group, List<FanMetrics> fans)
    {
        foreach (SensorReading s in group.Sensors)
        {
            if (s.SensorType == "Fan")
            {
                fans.Add(new FanMetrics(s.SensorName, s.Value, s.Identifier));
            }
        }

        foreach (HardwareGroup sub in group.SubHardware)
        {
            CollectFans(sub, fans);
        }
    }
}
