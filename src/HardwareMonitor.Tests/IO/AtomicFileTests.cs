using System.Text;
using HardwareMonitor.Core.IO;
using Xunit;

namespace HardwareMonitor.Tests.IO;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "HardwareMonitorTests", Guid.NewGuid().ToString("N"));

    public AtomicFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void WriteAllText_LuoTiedostonSisallolla()
    {
        string path = Path.Combine(_dir, "a.txt");

        AtomicFile.WriteAllText(path, "hei", Encoding.UTF8);

        Assert.Equal("hei", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_KorvaaAiemmanSisallon()
    {
        string path = Path.Combine(_dir, "a.txt");
        File.WriteAllText(path, "vanha pidempi sisältö");

        AtomicFile.WriteAllText(path, "uusi", Encoding.UTF8);

        Assert.Equal("uusi", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_EiJataTempTiedostoa()
    {
        string path = Path.Combine(_dir, "a.txt");

        AtomicFile.WriteAllText(path, "hei", Encoding.UTF8);

        Assert.Single(Directory.GetFiles(_dir)); // vain kohde, ei .tmp-jäännettä
    }
}
