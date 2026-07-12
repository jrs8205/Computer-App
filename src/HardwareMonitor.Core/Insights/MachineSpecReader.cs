using HardwareMonitor.Core.Sensors;

namespace HardwareMonitor.Core.Insights;

/// <summary>
/// Johtaa koneen kokoonpanon sensoridatasta (HardwareGroup-lista).
/// OS-kuvaus ja käyttäjän lisätiedot annetaan parametreina, jotta
/// luokka pysyy puhtaana funktiona.
/// </summary>
public static class MachineSpecReader
{
    public static MachineSpec Read(
        IReadOnlyList<HardwareGroup> groups, string osDescription, string userNotes)
    {
        string? cpu = null, motherboard = null;
        int? ramGb = null;
        var disks = new List<string>();

        // GPU-nimi valitaan SAMALLA logiikalla kuin GPU-mittaukset
        // (KeyMetricsService) — hybridikoneessa raportti ei saa yhdistää iGPU:n
        // nimeä dGPU:n mittauksiin.
        string? gpu = GpuSelector.SelectPrimary(groups)?.Name.Trim();

        foreach (HardwareGroup group in groups)
        {
            // LHM:n nimissä voi olla häntävälilyöntejä (esim. "Samsung SSD 860 EVO 1TB ").
            string name = group.Name.Trim();
            switch (group.HardwareType)
            {
                // Myös "Virtual Memory" -ryhmän sensorit ovat nimeltään "Memory Used"/
                // "Memory Available" — vain fyysinen muistiryhmä kelpaa RAM-laskentaan.
                case "Memory" when !name.Contains("Virtual", StringComparison.OrdinalIgnoreCase):
                    ramGb ??= ReadRamTotalGb(group);
                    break;
                case "Cpu": cpu ??= name; break;
                case "Motherboard": motherboard ??= name; break;
                case "Storage": disks.Add(name); break;
            }
        }

        return new MachineSpec(cpu, gpu, motherboard, ramGb, disks, osDescription, userNotes);
    }

    /// <summary>
    /// Memory Used + Memory Available (GB) pyöristettynä kokonaisiin gigatavuihin.
    /// Tarkat sensorinimet, jotta Virtual Memory -sensorit eivät summaudu mukaan.
    /// </summary>
    private static int? ReadRamTotalGb(HardwareGroup memory)
    {
        float? used = null, available = null;
        foreach (SensorReading s in memory.Sensors)
        {
            if (s.SensorName == "Memory Used")
            {
                used = s.Value;
            }
            else if (s.SensorName == "Memory Available")
            {
                available = s.Value;
            }
        }

        return used is { } u && available is { } a ? (int)MathF.Round(u + a) : null;
    }
}
