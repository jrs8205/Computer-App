using System.Text;
using System.Text.Json;
using HardwareMonitor.Core.IO;
using HardwareMonitor.Core.Metrics;

namespace HardwareMonitor.Core.Analysis;

public sealed record LastStateDisk(string Name, float? TempC);

/// <summary>Viimeisin tallennettu tila "ennen kaatumista" -selvitykseen (luku 17).</summary>
public sealed record LastState(
    DateTimeOffset Timestamp,
    bool CleanShutdown,
    float? CpuTempC,
    float? GpuTempC,
    float? GpuHotspotC,
    float? RamLoadPercent,
    IReadOnlyList<LastStateDisk> Disks);

/// <summary>
/// Kirjoittaa last_state.json-tiedostoa ajon aikana (lippu CleanShutdown=false)
/// ja merkitsee siistin sulkemisen. Jos käynnistyksessä edellisen istunnon
/// lippu on false, istunto päättyi yllättäen — viimeisimmät arvot kertovat
/// missä tilassa kone oli juuri ennen kaatumista.
/// </summary>
public sealed class LastStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _lock = new();
    private LastState? _current;
    private bool _shutdownMarked;

    public LastStateService(string? directory = null)
    {
        directory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HardwareMonitor",
            "data");

        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "last_state.json");
    }

    public string FilePath { get; }

    /// <summary>Edellisen istunnon tila, tai null jos tiedostoa ei ole tai se on rikki.</summary>
    public LastState? ReadPrevious()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LastState>(File.ReadAllText(FilePath));
        }
        catch
        {
            return null; // rikkinäinen tiedosto ei saa estää käynnistystä
        }
    }

    public void Write(KeyMetrics m, DateTimeOffset now)
    {
        var state = new LastState(
            Timestamp: now,
            CleanShutdown: false,
            CpuTempC: m.CpuPackageTempC,
            GpuTempC: m.GpuTempC,
            GpuHotspotC: m.GpuHotspotTempC,
            RamLoadPercent: m.RamLoadPercent,
            Disks: m.Disks.Select(d => new LastStateDisk(d.Name, d.TemperatureC)).ToList());

        lock (_lock)
        {
            // Tarkistus ja tallennus saman lukon alla: viivästynyt tausta-
            // kirjoitus ei saa liata jo merkittyä siistiä sulkemista, vaikka
            // MarkCleanShutdown ajaisi läpi kesken tämän kutsun.
            if (_shutdownMarked)
            {
                return;
            }

            // Rinnakkaiset Task.Run-kirjoitukset voivat saapua eri järjestyksessä
            // kuin käynnistyivät — vanhempi tila ei saa korvata uudempaa.
            if (_current is { } current && state.Timestamp < current.Timestamp)
            {
                return;
            }

            Save(state);
        }
    }

    /// <summary>Kutsutaan siistin sulkemisen yhteydessä (Dispose).</summary>
    public void MarkCleanShutdown()
    {
        lock (_lock)
        {
            _shutdownMarked = true;
            LastState? state = _current ?? ReadPrevious();
            if (state is not null)
            {
                Save(state with { CleanShutdown = true });
            }
        }
    }

    /// <summary>Kutsutaan vain _lock-lukon sisältä.</summary>
    private void Save(LastState state)
    {
        _current = state;
        // Atominen korvaus: kaatuminen kesken kirjoituksen ei saa jättää
        // katkaistua JSONia, jolloin edellinen tila jäisi raportoimatta.
        AtomicFile.WriteAllText(
            FilePath, JsonSerializer.Serialize(state, JsonOptions), Encoding.UTF8);
    }
}
