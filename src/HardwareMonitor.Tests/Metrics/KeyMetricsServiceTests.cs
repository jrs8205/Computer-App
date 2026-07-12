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
        string name, string type, SensorReading[] sensors, HardwareGroup[]? subs = null,
        string identifier = "") =>
        new(name, type, sensors, subs ?? Array.Empty<HardwareGroup>(), identifier);

    [Fact]
    public void Extract_LevySaaLaitteenPysyvanTunnisteen()
    {
        var nvme = Group("Samsung SSD 970 EVO Plus 1TB", "Storage", new[]
        {
            Reading("nvme", "Storage", "Temperature", "Temperature", 60f),
        }, identifier: "/nvme/0");

        KeyMetrics m = KeyMetricsService.Extract(new[] { nvme });

        Assert.Equal("/nvme/0", m.Disks[0].Identifier);
    }

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

    [Fact]
    public void Extract_PoimiiGpuArvotHotspotinKanssa()
    {
        var gpu = Group("NVIDIA GeForce RTX 2060", "GpuNvidia", new[]
        {
            Reading("gpu", "GpuNvidia", "GPU Core", "Load", 33f),
            Reading("gpu", "GpuNvidia", "GPU Core", "Temperature", 49f),
            Reading("gpu", "GpuNvidia", "GPU Hot Spot", "Temperature", 62f),
            Reading("gpu", "GpuNvidia", "GPU Memory Used", "SmallData", 975f),
            Reading("gpu", "GpuNvidia", "GPU Memory Total", "SmallData", 6144f),
            Reading("gpu", "GpuNvidia", "GPU Package", "Power", 17.7f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { gpu });

        Assert.Equal(33f, m.GpuLoadPercent);
        Assert.Equal(49f, m.GpuTempC);
        Assert.Equal(62f, m.GpuHotspotTempC);
        Assert.Equal(975f, m.GpuMemoryUsedMb);
        Assert.Equal(6144f, m.GpuMemoryTotalMb);
        Assert.Equal(17.7f, m.GpuPowerW);
    }

    [Fact]
    public void Extract_GpuIlmanHotspotia_HotspotOnNull()
    {
        var gpu = Group("Intel UHD", "GpuIntel", new[]
        {
            Reading("gpu", "GpuIntel", "GPU Core", "Temperature", 40f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { gpu });

        Assert.Equal(40f, m.GpuTempC);
        Assert.Null(m.GpuHotspotTempC);
    }

    [Fact]
    public void Extract_HybridiKone_KaikkiGpuArvotSamastaLaitteesta()
    {
        // iGPU listautuu usein ensin ja tarjoaa vain osan kentistä — arvoja
        // ei saa poimia eri GPU-laitteista sekaisin (kortti näyttäisi iGPU:n
        // lämmön dGPU:n kuorman vieressä).
        var igpu = Group("Intel UHD Graphics 630", "GpuIntel", new[]
        {
            Reading("igpu", "GpuIntel", "GPU Core", "Temperature", 40f),
            Reading("igpu", "GpuIntel", "GPU Memory Total", "SmallData", 128f),
        });
        var dgpu = Group("NVIDIA GeForce RTX 2060", "GpuNvidia", new[]
        {
            Reading("dgpu", "GpuNvidia", "GPU Core", "Load", 33f),
            Reading("dgpu", "GpuNvidia", "GPU Core", "Temperature", 49f),
            Reading("dgpu", "GpuNvidia", "GPU Hot Spot", "Temperature", 62f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { igpu, dgpu });

        Assert.Equal(33f, m.GpuLoadPercent);
        Assert.Equal(49f, m.GpuTempC);      // dGPU:n lämpö, ei iGPU:n 40
        Assert.Equal(62f, m.GpuHotspotTempC);
        Assert.Null(m.GpuMemoryTotalMb);    // iGPU:n VRAM ei sekoitu mukaan
    }

    [Fact]
    public void Extract_VainIntegroituGpu_KaytetaanSita()
    {
        var igpu = Group("Intel UHD Graphics 630", "GpuIntel", new[]
        {
            Reading("igpu", "GpuIntel", "GPU Core", "Temperature", 40f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { igpu });

        Assert.Equal(40f, m.GpuTempC);
    }

    [Fact]
    public void Extract_UseaLevy_KaikkiListassaJaAktiivisuusOnReadWriteMaksimi()
    {
        var ssd1 = Group("Samsung SSD 860 EVO 1TB", "Storage", new[]
        {
            Reading("ssd1", "Storage", "Temperature", "Temperature", 28f),
            Reading("ssd1", "Storage", "Read Activity", "Load", 5f),
            Reading("ssd1", "Storage", "Write Activity", "Load", 12f),
            Reading("ssd1", "Storage", "Total Activity", "Load", 100f),
        });
        var ssd2 = Group("Samsung SSD 970 EVO Plus 1TB", "Storage", new[]
        {
            Reading("ssd2", "Storage", "Temperature", "Temperature", 62f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { ssd1, ssd2 });

        Assert.Equal(2, m.Disks.Count);
        Assert.Equal("Samsung SSD 860 EVO 1TB", m.Disks[0].Name);
        Assert.Equal(28f, m.Disks[0].TemperatureC);
        Assert.Equal(12f, m.Disks[0].ActivityPercent); // max(read, write), EI "Total Activity"
        Assert.Equal(62f, m.Disks[1].TemperatureC);
        Assert.Null(m.Disks[1].ActivityPercent);
    }

    [Fact]
    public void Extract_NvmeIlmanPaasensoria_KayttaaEnsimmaistaNumeroitua()
    {
        // Admin-tilassa NVMe-levyn sensorit voivat olla "Temperature #1", "#2" jne.
        // ilman pelkkää "Temperature"-nimistä pääsensoria.
        var nvme = Group("Samsung SSD 970 EVO Plus 1TB", "Storage", new[]
        {
            Reading("nvme", "Storage", "Temperature #1", "Temperature", 61.85f),
            Reading("nvme", "Storage", "Temperature #2", "Temperature", 82.85f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { nvme });

        Assert.Equal(61.85f, m.Disks[0].TemperatureC);
    }

    [Fact]
    public void Extract_TuulettimillaOnPysyvaTunniste()
    {
        var gpu = Group("RTX 2060", "GpuNvidia", new[]
        {
            Reading("gpu", "GpuNvidia", "GPU Fan 1", "Fan", 1200f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { gpu });

        Assert.Equal("/gpunvidia/gpu/fan/gpu fan 1", m.Fans[0].Identifier);
    }

    [Fact]
    public void Extract_KeraaTuulettimetMyosAlalaitteista()
    {
        var superIo = Group("Nuvoton NCT6798D", "SuperIO", new[]
        {
            Reading("io", "SuperIO", "Fan #1", "Fan", 579f),
            Reading("io", "SuperIO", "Fan #2", "Fan", 1942f),
        });
        var motherboard = Group("ASUS Z390-F", "Motherboard",
            Array.Empty<SensorReading>(), new[] { superIo });
        var gpu = Group("RTX 2060", "GpuNvidia", new[]
        {
            Reading("gpu", "GpuNvidia", "GPU Fan 1", "Fan", 0f),
        });

        KeyMetrics m = KeyMetricsService.Extract(new[] { motherboard, gpu });

        Assert.Equal(3, m.Fans.Count);
        Assert.Contains(m.Fans, f => f.Name == "Fan #2" && f.Rpm == 1942f);
        Assert.Contains(m.Fans, f => f.Name == "GPU Fan 1" && f.Rpm == 0f);
    }
}
