using HardwareMonitor.Core.Settings;
using Xunit;

namespace HardwareMonitor.Tests.Settings;

public class SettingsValidatorTests
{
    [Fact]
    public void PilkkuDesimaali_Kelpaa()
    {
        ParseResult r = SettingsValidator.ParseNumber("85,5", 20, 120);
        Assert.True(r.Ok);
        Assert.Equal(85.5f, r.Value);
    }

    [Fact]
    public void PisteDesimaali_Kelpaa()
    {
        ParseResult r = SettingsValidator.ParseNumber("85.5", 20, 120);
        Assert.True(r.Ok);
        Assert.Equal(85.5f, r.Value);
    }

    [Fact]
    public void ValilyonnitTrimmataan()
    {
        Assert.True(SettingsValidator.ParseNumber(" 85 ", 20, 120).Ok);
    }

    [Fact]
    public void TyhjaSyote_AntaaVirheen()
    {
        ParseResult r = SettingsValidator.ParseNumber("", 20, 120);
        Assert.False(r.Ok);
        Assert.Equal("Anna numero", r.Error);
    }

    [Fact]
    public void Roskasyote_AntaaVirheen()
    {
        Assert.Equal("Anna numero", SettingsValidator.ParseNumber("abc", 20, 120).Error);
    }

    [Fact]
    public void NaN_AntaaVirheen()
    {
        // float.TryParse hyväksyy "NaN"-merkkijonon .NET 8:ssa, ja NaN läpäisisi
        // molemmat raja-arvovertailut — hälytysraja mykistyisi huomaamatta.
        ParseResult r = SettingsValidator.ParseNumber("NaN", 20, 120);
        Assert.False(r.Ok);
        Assert.Equal("Anna numero", r.Error);
    }

    [Fact]
    public void AlleMinimin_KertooSallitunValin()
    {
        Assert.Equal("Sallittu väli on 20–120",
            SettingsValidator.ParseNumber("10", 20, 120).Error);
    }

    [Fact]
    public void YliMaksimin_KertooSallitunValin()
    {
        Assert.Equal("Sallittu väli on 20–120",
            SettingsValidator.ParseNumber("150", 20, 120).Error);
    }

    [Fact]
    public void RajatKelpaavat()
    {
        Assert.True(SettingsValidator.ParseNumber("20", 20, 120).Ok);
        Assert.True(SettingsValidator.ParseNumber("120", 20, 120).Ok);
    }

    [Fact]
    public void VaroitusrajanOltavaPienempiKuinKriittisen()
    {
        Assert.NotNull(SettingsValidator.ValidateWarnCrit(95, 95));
        Assert.NotNull(SettingsValidator.ValidateWarnCrit(96, 95));
        Assert.Null(SettingsValidator.ValidateWarnCrit(85, 95));
    }
}
