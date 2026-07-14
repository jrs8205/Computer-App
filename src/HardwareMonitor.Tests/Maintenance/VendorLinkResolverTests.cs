using HardwareMonitor.Core.Maintenance;
using Xunit;

namespace HardwareMonitor.Tests.Maintenance;

public class VendorLinkResolverTests
{
    [Fact]
    public void Asus_emolevy_ohjautuu_kielen_mukaiselle_tukisivulle()
    {
        Assert.Equal("https://www.asus.com/fi/support/",
            VendorLinkResolver.Resolve("ASUS ROG STRIX Z390-F GAMING", "fi"));
        Assert.Equal("https://www.asus.com/support/",
            VendorLinkResolver.Resolve("ASUS ROG STRIX Z390-F GAMING", "xx"));
    }

    [Fact]
    public void Nvidia_gpu_ohjautuu_globaalille_ajurihakusivulle()
    {
        // NVIDIAlla ei ole suomenkielistä sivustoa — fi-fi/drivers palauttaa
        // 404 (käyttäjän havainto 14.7.2026), joten kaikki kielet ohjataan
        // globaalille ajurihakusivulle.
        Assert.Equal("https://www.nvidia.com/Download/index.aspx",
            VendorLinkResolver.Resolve("NVIDIA GeForce RTX 2060", "fi"));
        Assert.Equal("https://www.nvidia.com/Download/index.aspx",
            VendorLinkResolver.Resolve("NVIDIA GeForce RTX 2060", "xx"));
    }

    [Fact]
    public void Samsung_ssd_ohjautuu_globaalille_tyokalusivulle()
    {
        Assert.Equal("https://semiconductor.samsung.com/consumer-storage/support/tools/",
            VendorLinkResolver.Resolve("Samsung SSD 970 EVO Plus 1TB", "fi"));
    }

    [Fact]
    public void Tuntematon_valmistaja_tai_tyhja_nimi_ei_saa_linkkia()
    {
        Assert.Null(VendorLinkResolver.Resolve("Kingston A400", "fi"));
        Assert.Null(VendorLinkResolver.Resolve(null, "fi"));
        Assert.Null(VendorLinkResolver.Resolve("  ", "fi"));
    }

    [Fact]
    public void Hantavalilyonnit_ja_kirjainkoko_eivat_haittaa()
    {
        Assert.Equal("https://semiconductor.samsung.com/consumer-storage/support/tools/",
            VendorLinkResolver.Resolve("SAMSUNG ssd 860 EVO 1TB ", "fi"));
    }
}
