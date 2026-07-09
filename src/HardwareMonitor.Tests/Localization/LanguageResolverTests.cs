using System.Globalization;
using HardwareMonitor.Core.Localization;
using Xunit;

namespace HardwareMonitor.Tests.Localization;

public class LanguageResolverTests
{
    private static readonly CultureInfo FinnishOs = CultureInfo.GetCultureInfo("fi-FI");
    private static readonly CultureInfo EnglishOs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo SwedishOs = CultureInfo.GetCultureInfo("sv-SE");

    [Fact]
    public void Fi_AntaaSuomen()
    {
        Assert.Equal("fi-FI", LanguageResolver.Resolve("fi", EnglishOs).Name);
    }

    [Fact]
    public void En_AntaaEnglannin()
    {
        Assert.Equal("en-US", LanguageResolver.Resolve("en", FinnishOs).Name);
    }

    [Fact]
    public void Automaattinen_SuomiKoneella_AntaaSuomen()
    {
        Assert.Equal("fi-FI", LanguageResolver.Resolve("", FinnishOs).Name);
    }

    [Fact]
    public void Automaattinen_MuuKieliKoneella_AntaaEnglannin()
    {
        Assert.Equal("en-US", LanguageResolver.Resolve("", EnglishOs).Name);
        Assert.Equal("en-US", LanguageResolver.Resolve("", SwedishOs).Name);
    }

    [Fact]
    public void TuntematonArvo_ToimiiKutenAutomaattinen()
    {
        Assert.Equal("fi-FI", LanguageResolver.Resolve("xyz", FinnishOs).Name);
    }
}
