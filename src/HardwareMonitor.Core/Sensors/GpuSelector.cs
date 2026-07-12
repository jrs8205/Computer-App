namespace HardwareMonitor.Core.Sensors;

/// <summary>
/// Ensisijaisen GPU:n valinta hybridikoneissa (iGPU + dGPU). Sekä
/// KeyMetricsService (mittaukset) että MachineSpecReader (kokoonpanon
/// GPU-nimi) käyttävät tätä, jotta raportin GPU-nimi vastaa aina samaa
/// laitetta kuin sen mittaukset.
/// </summary>
public static class GpuSelector
{
    private static bool IsGpu(HardwareGroup g) =>
        g.HardwareType.StartsWith("Gpu", StringComparison.Ordinal);

    /// <summary>
    /// Ensisijainen GPU: erillisnäytönohjain (Nvidia/AMD) ennen Inteliä,
    /// tasapelissä eniten sensoreita tarjoava (erilliskortti on rikkaampi).
    /// Palauttaa null jos GPU:ita ei ole.
    /// </summary>
    public static HardwareGroup? SelectPrimary(IReadOnlyList<HardwareGroup> groups) =>
        groups
            .Where(IsGpu)
            .OrderBy(g => g.HardwareType == "GpuIntel" ? 1 : 0)
            .ThenByDescending(g => g.Sensors.Count)
            .FirstOrDefault();
}
