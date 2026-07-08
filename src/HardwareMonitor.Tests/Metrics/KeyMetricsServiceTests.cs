using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Sensors;
using Xunit;

namespace HardwareMonitor.Tests.Metrics;

public class KeyMetricsServiceTests
{
    private static SensorReading Reading(
        string hwName, string hwType, string sensorName, string sensorType, float? value) =>
        new(hwName, hwType, sensorName, sensorType, value, "",
            $"/{hwType}/{hwName}/{sensorType}/{sensorName}".ToLowerInvariant());

    private static HardwareGroup Group(
        string name, string type, SensorReading[] sensors, HardwareGroup[]? subs = null) =>
        new(name, type, sensors, subs ?? Array.Empty<HardwareGroup>());

    [Fact]
    public void Extract_TyhjaLista_PalauttaaNullArvotEikaHeitaPoikkeusta()
    {
        KeyMetrics m = KeyMetricsService.Extract(Array.Empty<HardwareGroup>());

        Assert.Null(m.CpuLoadPercent);
        Assert.Null(m.CpuPackageTempC);
        Assert.Null(m.RamLoadPercent);
        Assert.Empty(m.Disks);
        Assert.Empty(m.Fans);
    }

    [Fact]
    public void Extract_PoimiiCpuArvot()
    {
        var cpu = Group("Intel Core i9-9900K", "Cpu", new[]
        {
            Reading("i9", "Cpu", "CPU Total", "Load", 42f),
            Reading("i9", "Cpu", "CPU Core #1 Thread #1", "Load", 80f),
            Reading("i9", "Cpu", "CPU Package", "Temperature", 65f),
            Reading("i9", "Cpu", "CPU Core #1", "Clock", 4700f),
            Reading("i9", "Cpu", "CPU Core #2", "Clock", 4600f),
            Reading("i9", "Cpu", "CPU Package", "Power", 112f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { cpu });

        Assert.Equal(42f, m.CpuLoadPercent);
        Assert.Equal(65f, m.CpuPackageTempC);
        Assert.Equal(4700f, m.CpuMaxClockMhz);
        Assert.Equal(112f, m.CpuPackagePowerW);
    }

    [Fact]
    public void Extract_CpuIlmanPackageLampoa_KayttaaCoreMaxia()
    {
        var cpu = Group("CPU", "Cpu", new[]
        {
            Reading("cpu", "Cpu", "Core Max", "Temperature", 71f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { cpu });

        Assert.Equal(71f, m.CpuPackageTempC);
    }

    [Fact]
    public void Extract_PoimiiRamArvot()
    {
        var ram = Group("Generic Memory", "Memory", new[]
        {
            Reading("ram", "Memory", "Memory", "Load", 19.1f),
            Reading("ram", "Memory", "Memory Used", "Data", 12.2f),
            Reading("ram", "Memory", "Memory Available", "Data", 51.7f),
            Reading("ram", "Memory", "Virtual Memory", "Load", 99f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { ram });

        Assert.Equal(19.1f, m.RamLoadPercent);
        Assert.Equal(12.2f, m.RamUsedGb);
        Assert.Equal(51.7f, m.RamAvailableGb);
    }
}
