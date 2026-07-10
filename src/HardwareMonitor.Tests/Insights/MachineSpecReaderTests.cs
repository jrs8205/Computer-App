using HardwareMonitor.Core.Insights;
using HardwareMonitor.Core.Sensors;
using Xunit;

namespace HardwareMonitor.Tests.Insights;

public class MachineSpecReaderTests
{
    private static HardwareGroup Group(
        string name, string type, params SensorReading[] sensors) =>
        new(name, type, sensors, Array.Empty<HardwareGroup>());

    private static SensorReading Data(string hw, string sensor, float? value) =>
        new(hw, "Memory", sensor, "Data", value, "GB", $"/ram/{sensor}");

    [Fact]
    public void PoimiiNimetLaitetyypeittain()
    {
        var groups = new[]
        {
            Group("ASUS ROG STRIX Z390-F GAMING", "Motherboard"),
            Group("Intel Core i9-9900K", "Cpu"),
            Group("NVIDIA GeForce RTX 2060", "GpuNvidia"),
            Group("Samsung SSD 860 EVO 1TB", "Storage"),
            Group("Samsung SSD 970 EVO Plus 1TB", "Storage"),
        };

        MachineSpec spec = MachineSpecReader.Read(groups, "Windows 11 (build 26200)", "");

        Assert.Equal("Intel Core i9-9900K", spec.CpuName);
        Assert.Equal("NVIDIA GeForce RTX 2060", spec.GpuName);
        Assert.Equal("ASUS ROG STRIX Z390-F GAMING", spec.MotherboardName);
        Assert.Equal(
            new[] { "Samsung SSD 860 EVO 1TB", "Samsung SSD 970 EVO Plus 1TB" },
            spec.DiskNames);
        Assert.Equal("Windows 11 (build 26200)", spec.OsDescription);
    }

    [Fact]
    public void LaskeeRamKokonaismaaranJaPyoristaa()
    {
        var groups = new[]
        {
            Group("Generic Memory", "Memory",
                Data("Generic Memory", "Memory Used", 31.2f),
                Data("Generic Memory", "Memory Available", 32.7f),
                Data("Generic Memory", "Virtual Memory Used", 40f)),
        };

        MachineSpec spec = MachineSpecReader.Read(groups, "", "");

        Assert.Equal(64, spec.RamTotalGb); // 31.2 + 32.7 = 63.9 → 64; Virtual ohitetaan
    }

    [Fact]
    public void PuuttuvatLaitteet_PalauttaaNullitJaTyhjanLevylistan()
    {
        MachineSpec spec = MachineSpecReader.Read(
            Array.Empty<HardwareGroup>(), "", "");

        Assert.Null(spec.CpuName);
        Assert.Null(spec.GpuName);
        Assert.Null(spec.MotherboardName);
        Assert.Null(spec.RamTotalGb);
        Assert.Empty(spec.DiskNames);
    }

    [Fact]
    public void ValittaaLisatiedotSellaisenaan()
    {
        MachineSpec spec = MachineSpecReader.Read(
            Array.Empty<HardwareGroup>(), "", "AIO-vesijäähdytys");

        Assert.Equal("AIO-vesijäähdytys", spec.UserNotes);
    }
}
