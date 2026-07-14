using HardwareMonitor.Core.Maintenance;
using Xunit;

namespace HardwareMonitor.Tests.Maintenance;

public class NvidiaDriverVersionTests
{
    [Theory]
    [InlineData("32.0.15.4680", "546.80")]
    [InlineData("31.0.15.5222", "552.22")]
    [InlineData("30.0.14.7168", "471.68")]
    public void ToMarketingVersion_muuntaa_wmi_version_nvidian_muotoon(string wmi, string expected) =>
        Assert.Equal(expected, NvidiaDriverVersion.ToMarketingVersion(wmi));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4680")]
    [InlineData("1.2")]
    [InlineData("a.b.c.d")]
    public void ToMarketingVersion_kelvoton_syote_palauttaa_null(string? wmi) =>
        Assert.Null(NvidiaDriverVersion.ToMarketingVersion(wmi));
}
