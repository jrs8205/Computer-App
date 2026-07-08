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
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Load" when s.SensorName == "GPU Core":
                                gpuLoad ??= s.Value;
                                break;
                            case "Temperature" when s.SensorName == "GPU Core":
                                gpuTemp ??= s.Value;
                                break;
                            case "Temperature" when s.SensorName == "GPU Hot Spot":
                                gpuHotspot ??= s.Value;
                                break;
                            case "SmallData" when s.SensorName == "GPU Memory Used":
                                vramUsed ??= s.Value;
                                break;
                            case "SmallData" when s.SensorName == "GPU Memory Total":
                                vramTotal ??= s.Value;
                                break;
                            case "Power" when s.SensorName == "GPU Package":
                                gpuPower ??= s.Value;
                                break;
                        }
                    }
                    break;

                case "Storage":
                    float? diskTemp = null, readActivity = null, writeActivity = null;
                    foreach (SensorReading s in group.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case "Temperature" when s.SensorName == "Temperature":
                                diskTemp = s.Value;
                                break;
                            case "Load" when s.SensorName == "Read Activity":
                                readActivity = s.Value;
                                break;
                            case "Load" when s.SensorName == "Write Activity":
                                writeActivity = s.Value;
                                break;
                        }
                    }

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

    /// <summary>Kerää Fan-tyyppiset sensorit laitteesta ja sen alalaitteista rekursiivisesti.</summary>
    private static void CollectFans(HardwareGroup group, List<FanMetrics> fans)
    {
        foreach (SensorReading s in group.Sensors)
        {
            if (s.SensorType == "Fan")
            {
                fans.Add(new FanMetrics(s.SensorName, s.Value));
            }
        }

        foreach (HardwareGroup sub in group.SubHardware)
        {
            CollectFans(sub, fans);
        }
    }
}
