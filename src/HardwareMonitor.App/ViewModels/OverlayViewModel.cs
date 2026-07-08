using System.ComponentModel;
using System.Globalization;
using System.Text;
using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Overlayn näkymämalli: rakentaa KeyMetrics-arvoista ja asetuksista monirivisen
/// tekstin. Yksi Text-property pitää päivityksen välkkymättömänä ja kevyenä.
/// </summary>
public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private string _text = "";
    private double _fontSize = 14;
    private double _backgroundOpacity = 0.85;

    public string Text
    {
        get => _text;
        private set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public double FontSize
    {
        get => _fontSize;
        private set
        {
            if (Math.Abs(_fontSize - value) < 0.01)
            {
                return;
            }

            _fontSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSize)));
        }
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        private set
        {
            if (Math.Abs(_backgroundOpacity - value) < 0.01)
            {
                return;
            }

            _backgroundOpacity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundOpacity)));
        }
    }

    public void Update(KeyMetrics m, OverlaySettings s)
    {
        FontSize = s.FontSize;
        BackgroundOpacity = s.Opacity;

        var sb = new StringBuilder();

        if (s.ShowCpu)
        {
            sb.AppendLine($"CPU  {Pct(m.CpuLoadPercent)}  {Temp(m.CpuPackageTempC)}  {Num(m.CpuMaxClockMhz, "MHz")}");
        }

        if (s.ShowGpu)
        {
            sb.AppendLine($"GPU  {Pct(m.GpuLoadPercent)}  {Temp(m.GpuTempC)}  hot {Temp(m.GpuHotspotTempC)}");
            if (m.GpuMemoryUsedMb is { } used && m.GpuMemoryTotalMb is { } total)
            {
                sb.AppendLine($"VRAM {used:0}/{total:0} MB");
            }
        }

        if (s.ShowRam)
        {
            string usedGb = m.RamUsedGb is { } gb
                ? gb.ToString("0.0", CultureInfo.CurrentCulture) + " GB"
                : "—";
            sb.AppendLine($"RAM  {Pct(m.RamLoadPercent)}  {usedGb}");
        }

        if (s.ShowDisks)
        {
            foreach (DiskMetrics disk in m.Disks)
            {
                if (disk.TemperatureC is { } t)
                {
                    sb.AppendLine($"{Shorten(disk.Name)}  {t:0} °C");
                }
            }
        }

        if (s.ShowFans)
        {
            foreach (FanMetrics fan in m.Fans)
            {
                if (fan.Rpm is { } rpm and > 0)
                {
                    sb.AppendLine($"{fan.Name}  {rpm:0} RPM");
                }
            }
        }

        Text = sb.ToString().TrimEnd();
    }

    private static string Pct(float? v) => v is { } x ? $"{x:0} %" : "—";

    private static string Temp(float? v) => v is { } x ? $"{x:0} °C" : "—";

    private static string Num(float? v, string unit) => v is { } x ? $"{x:0} {unit}" : "—";

    /// <summary>Lyhentää pitkät levynimet overlayn kompakteille riveille.</summary>
    private static string Shorten(string name) =>
        name.Length <= 20 ? name : name[..20].TrimEnd();

    public event PropertyChangedEventHandler? PropertyChanged;
}
