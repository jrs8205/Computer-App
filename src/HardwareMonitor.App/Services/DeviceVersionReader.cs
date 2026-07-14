using System.Management;

namespace HardwareMonitor.App.Services;

/// <summary>Laitteiden nykyversiot Ylläpito-välilehdelle.</summary>
public sealed record DeviceVersions(
    string? BiosVersion,
    DateTime? BiosDate,
    string? GpuDriverVersion,
    DateTime? GpuDriverDate,
    IReadOnlyList<(string Model, string Firmware)> DiskFirmware);

/// <summary>
/// Lukee BIOS-, näytönohjain- ja levyversiot WMI:stä. Vain luku; jokainen
/// kysely siedetään erikseen — WMI-virhe ei kaada Ylläpito-näkymää, puuttuva
/// arvo näkyy viivana.
/// </summary>
public static class DeviceVersionReader
{
    public static DeviceVersions Read(string? gpuName, Action<string> log)
    {
        string? biosVersion = null;
        DateTime? biosDate = null;
        Query("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", row =>
        {
            biosVersion ??= (row["SMBIOSBIOSVersion"] as string)?.Trim();
            biosDate ??= ToDate(row["ReleaseDate"]);
        }, log);

        // Hybridikoneessa voi olla useita ohjaimia — valitaan sama GPU kuin
        // mittauksissa (nimivertailu), muuten ensimmäinen.
        var controllers = new List<(string Name, string? Version, DateTime? Date)>();
        Query("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController", row =>
            controllers.Add((
                ((row["Name"] as string) ?? "").Trim(),
                (row["DriverVersion"] as string)?.Trim(),
                ToDate(row["DriverDate"]))), log);
        (string Name, string? Version, DateTime? Date) gpu = controllers.FirstOrDefault(c =>
            string.Equals(c.Name, gpuName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (gpu.Name is null or "" && controllers.Count > 0)
        {
            gpu = controllers[0];
        }

        var disks = new List<(string, string)>();
        Query("SELECT Model, FirmwareRevision FROM Win32_DiskDrive", row =>
        {
            string model = ((row["Model"] as string) ?? "").Trim();
            if (model.Length > 0)
            {
                disks.Add((model, ((row["FirmwareRevision"] as string) ?? "").Trim()));
            }
        }, log);

        return new DeviceVersions(biosVersion, biosDate, gpu.Version, gpu.Date, disks);
    }

    private static void Query(string wql, Action<ManagementBaseObject> onRow, Action<string> log)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementBaseObject row in rows)
            {
                using (row)
                {
                    onRow(row);
                }
            }
        }
        catch (Exception ex)
        {
            // WMI heittää mm. ManagementException- ja COMException-poikkeuksia —
            // mikään niistä ei saa estää muiden rivien näyttämistä.
            log($"WMI-kysely epäonnistui ({wql}): {ex.Message}");
        }
    }

    /// <summary>CIM_DATETIME ("20240215000000.000000+000") → DateTime tai null.</summary>
    private static DateTime? ToDate(object? cimDate)
    {
        try
        {
            return cimDate is string s && s.Length > 0
                ? ManagementDateTimeConverter.ToDateTime(s)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
