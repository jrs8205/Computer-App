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
        string? cpu = null, gpu = null, motherboard = null;
        int? ramGb = null;
        var disks = new List<string>();

        foreach (HardwareGroup group in groups)
        {
            switch (group.HardwareType)
            {
                case "Cpu": cpu ??= group.Name; break;
                case "Motherboard": motherboard ??= group.Name; break;
                case "Storage": disks.Add(group.Name); break;
                case "Memory": ramGb ??= ReadRamTotalGb(group); break;
                default:
                    if (group.HardwareType.StartsWith("Gpu", StringComparison.Ordinal))
                    {
                        gpu ??= group.Name;
                    }

                    break;
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
