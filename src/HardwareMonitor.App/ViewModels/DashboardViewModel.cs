using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using HardwareMonitor.Core.Metrics;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Dashboard-korttien näkymämalli: KeyMetrics-arvot valmiiksi muotoiltuina
/// merkkijonoina. Puuttuva arvo näytetään aina "—":na (specin virheenkäsittely).
/// </summary>
public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private string _cpuLoad = "—", _cpuTemp = "—", _cpuClock = "—", _cpuPower = "—";
    private string _gpuLoad = "—", _gpuTemp = "—", _gpuHotspot = "—", _gpuVram = "—", _gpuPower = "—";
    private string _ramLoad = "—", _ramUsed = "—";

    public string CpuLoad { get => _cpuLoad; private set => Set(ref _cpuLoad, value, nameof(CpuLoad)); }
    public string CpuTemp { get => _cpuTemp; private set => Set(ref _cpuTemp, value, nameof(CpuTemp)); }
    public string CpuClock { get => _cpuClock; private set => Set(ref _cpuClock, value, nameof(CpuClock)); }
    public string CpuPower { get => _cpuPower; private set => Set(ref _cpuPower, value, nameof(CpuPower)); }
    public string GpuLoad { get => _gpuLoad; private set => Set(ref _gpuLoad, value, nameof(GpuLoad)); }
    public string GpuTemp { get => _gpuTemp; private set => Set(ref _gpuTemp, value, nameof(GpuTemp)); }
    public string GpuHotspot { get => _gpuHotspot; private set => Set(ref _gpuHotspot, value, nameof(GpuHotspot)); }
    public string GpuVram { get => _gpuVram; private set => Set(ref _gpuVram, value, nameof(GpuVram)); }
    public string GpuPower { get => _gpuPower; private set => Set(ref _gpuPower, value, nameof(GpuPower)); }
    public string RamLoad { get => _ramLoad; private set => Set(ref _ramLoad, value, nameof(RamLoad)); }
    public string RamUsed { get => _ramUsed; private set => Set(ref _ramUsed, value, nameof(RamUsed)); }

    public ObservableCollection<string> Disks { get; } = new();
    public ObservableCollection<string> Fans { get; } = new();

    public void Update(KeyMetrics m)
    {
        CpuLoad = Fmt(m.CpuLoadPercent, "%");
        CpuTemp = Fmt(m.CpuPackageTempC, "°C");
        CpuClock = Fmt(m.CpuMaxClockMhz, "MHz");
        CpuPower = Fmt(m.CpuPackagePowerW, "W");
        GpuLoad = Fmt(m.GpuLoadPercent, "%");
        GpuTemp = Fmt(m.GpuTempC, "°C");
        GpuHotspot = Fmt(m.GpuHotspotTempC, "°C");
        GpuVram = m.GpuMemoryUsedMb is { } used && m.GpuMemoryTotalMb is { } total
            ? $"{used:0} / {total:0} MB"
            : "—";
        GpuPower = Fmt(m.GpuPowerW, "W");
        RamLoad = Fmt(m.RamLoadPercent, "%");
        RamUsed = m.RamUsedGb is { } usedGb
            ? m.RamAvailableGb is { } availGb
                ? $"{usedGb.ToString("0.0", CultureInfo.CurrentCulture)} / {(usedGb + availGb).ToString("0.0", CultureInfo.CurrentCulture)} GB"
                : $"{usedGb.ToString("0.0", CultureInfo.CurrentCulture)} GB"
            : "—";

        SyncRows(Disks, m.Disks.Select(d =>
            $"{d.Name}   {Fmt(d.TemperatureC, "°C")}   {Fmt(d.ActivityPercent, "%")}"));
        SyncRows(Fans, m.Fans.Select(f => $"{f.Name}   {Fmt(f.Rpm, "RPM")}"));
    }

    private static string Fmt(float? value, string unit) =>
        value is { } v ? $"{v.ToString("0", CultureInfo.CurrentCulture)} {unit}" : "—";

    /// <summary>Päivittää listan rivit paikallaan, jotta UI ei vilku joka sekunti.</summary>
    private static void SyncRows(ObservableCollection<string> target, IEnumerable<string> rows)
    {
        var list = rows.ToList();
        while (target.Count > list.Count)
        {
            target.RemoveAt(target.Count - 1);
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (i >= target.Count)
            {
                target.Add(list[i]);
            }
            else if (target[i] != list[i])
            {
                target[i] = list[i];
            }
        }
    }

    private void Set(ref string field, string value, string name)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
