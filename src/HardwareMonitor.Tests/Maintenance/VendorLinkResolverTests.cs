using HardwareMonitor.Core.Maintenance;
using Xunit;

namespace HardwareMonitor.Tests.Maintenance;

public class VendorLinkResolverTests
{
    [Fact]
    public void Rog_emolevy_ohjautuu_mallisivulle_rog_sivustolla()
    {
        // Käyttäjän vahvistama toimiva osoite 14.7.2026 — slug johdetaan
        // LHM:n laitenimestä.
        Assert.Equal("https://rog.asus.com/fi/motherboards/rog-strix/rog-strix-z390-f-gaming-model/",
            VendorLinkResolver.Resolve("ASUS ROG STRIX Z390-F GAMING", "fi", DeviceKind.Motherboard));
        Assert.Equal("https://rog.asus.com/motherboards/rog-strix/rog-strix-z390-f-gaming-model/",
            VendorLinkResolver.Resolve("ASUS ROG STRIX Z390-F GAMING", "xx", DeviceKind.Motherboard));
    }

    [Fact]
    public void Rog_nimi_ilman_mallia_palaa_yleiselle_tukisivulle()
    {
        Assert.Equal("https://www.asus.com/fi/support/",
            VendorLinkResolver.Resolve("ASUS ROG", "fi", DeviceKind.Motherboard));
    }

    [Fact]
    public void Asus_emolevy_ilman_rogia_ohjautuu_kielen_mukaiselle_tukisivulle()
    {
        Assert.Equal("https://www.asus.com/fi/support/",
            VendorLinkResolver.Resolve("ASUS PRIME Z390-A", "fi", DeviceKind.Motherboard));
        Assert.Equal("https://www.asus.com/support/",
            VendorLinkResolver.Resolve("ASUS PRIME Z390-A", "xx", DeviceKind.Motherboard));
    }

    [Fact]
    public void Asus_rog_muu_kuin_emolevy_ei_saa_emolevysivua()
    {
        // ROG-mallisivupolku /motherboards/ pätee vain emolevyihin.
        Assert.Equal("https://www.asus.com/fi/support/",
            VendorLinkResolver.Resolve("ASUS ROG Strix GeForce RTX 2060", "fi", DeviceKind.Gpu));
    }

    [Fact]
    public void Nvidia_gpu_ohjautuu_globaalille_ajurihakusivulle()
    {
        // NVIDIAlla ei ole suomenkielistä sivustoa — fi-fi/drivers palauttaa
        // 404 (käyttäjän havainto 14.7.2026), joten kaikki kielet ohjataan
        // globaalille ajurihakusivulle.
        Assert.Equal("https://www.nvidia.com/Download/index.aspx",
            VendorLinkResolver.Resolve("NVIDIA GeForce RTX 2060", "fi", DeviceKind.Gpu));
        Assert.Equal("https://www.nvidia.com/Download/index.aspx",
            VendorLinkResolver.Resolve("NVIDIA GeForce RTX 2060", "xx", DeviceKind.Gpu));
    }

    [Fact]
    public void Samsung_ssd_ohjautuu_globaalille_tyokalusivulle()
    {
        Assert.Equal("https://semiconductor.samsung.com/consumer-storage/support/tools/",
            VendorLinkResolver.Resolve("Samsung SSD 970 EVO Plus 1TB", "fi", DeviceKind.Disk));
    }

    [Fact]
    public void Tuntematon_valmistaja_tai_tyhja_nimi_ei_saa_linkkia()
    {
        Assert.Null(VendorLinkResolver.Resolve("Kingston A400", "fi", DeviceKind.Disk));
        Assert.Null(VendorLinkResolver.Resolve(null, "fi", DeviceKind.Motherboard));
        Assert.Null(VendorLinkResolver.Resolve("  ", "fi", DeviceKind.Gpu));
    }

    [Fact]
    public void Hantavalilyonnit_ja_kirjainkoko_eivat_haittaa()
    {
        Assert.Equal("https://semiconductor.samsung.com/consumer-storage/support/tools/",
            VendorLinkResolver.Resolve("SAMSUNG ssd 860 EVO 1TB ", "fi", DeviceKind.Disk));
    }
}
