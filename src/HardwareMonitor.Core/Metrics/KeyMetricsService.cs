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
        float? ramLoad = null, ramUsed = null, ramAvailable = null;

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
            }
        }

        return new KeyMetrics(
            CpuLoadPercent: cpuLoad,
            CpuPackageTempC: cpuPackageTemp ?? cpuCoreMaxTemp,
            CpuMaxClockMhz: cpuClock,
            CpuPackagePowerW: cpuPower,
            GpuLoadPercent: null,
            GpuTempC: null,
            GpuHotspotTempC: null,
            GpuMemoryUsedMb: null,
            GpuMemoryTotalMb: null,
            GpuPowerW: null,
            RamLoadPercent: ramLoad,
            RamUsedGb: ramUsed,
            RamAvailableGb: ramAvailable,
            Disks: Array.Empty<DiskMetrics>(),
            Fans: Array.Empty<FanMetrics>());
    }
}
