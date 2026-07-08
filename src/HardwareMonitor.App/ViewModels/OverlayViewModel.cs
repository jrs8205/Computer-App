using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using HardwareMonitor.Core.Analysis;
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

    private static readonly Brush WarningBorder = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)));
    private static readonly Brush CriticalBorder = Frozen(new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)));
    private static readonly Brush MoveModeBorder = Frozen(new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)));

    private Brush _borderBrush = Brushes.Transparent;
    private ThresholdState _worstState;
    private bool _moveModeVisual;

    /// <summary>Paneelin reunus: näkyvissä vain kun jokin mittari hälyttää.</summary>
    public Brush BorderBrush
    {
        get => _borderBrush;
        private set
        {
            if (ReferenceEquals(_borderBrush, value))
            {
                return;
            }

            _borderBrush = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderBrush)));
        }
    }

    public void SetWorstState(ThresholdState state)
    {
        _worstState = state;
        UpdateBorder();
    }

    /// <summary>
    /// Siirtotilan syaani reunus kulkee saman sidonnan kautta kuin hälytysvärit —
    /// paikallinen arvo Border-elementissä tuhoaisi sidonnan pysyvästi (WPF).
    /// </summary>
    public void SetMoveModeVisual(bool enabled)
    {
        _moveModeVisual = enabled;
        UpdateBorder();
    }

    private void UpdateBorder() =>
        BorderBrush = _moveModeVisual
            ? MoveModeBorder
            : _worstState switch
            {
                ThresholdState.Critical => CriticalBorder,
                ThresholdState.Warning => WarningBorder,
                _ => Brushes.Transparent,
            };

    private static Brush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    public void Update(KeyMetrics m, AppSettings settings)
    {
        OverlaySettings s = settings.Overlay;
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
            // Kokonaismäärä = käytetty + vapaa (LibreHardwareMonitor ei anna sitä suoraan).
            string ram = m.RamUsedGb is { } used
                ? m.RamAvailableGb is { } avail
                    ? $"{used.ToString("0.0", CultureInfo.CurrentCulture)}/{(used + avail).ToString("0", CultureInfo.CurrentCulture)} GB"
                    : $"{used.ToString("0.0", CultureInfo.CurrentCulture)} GB"
                : "—";
            sb.AppendLine($"RAM  {Pct(m.RamLoadPercent)}  {ram}");
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
                    string name = settings.FanLabels.TryGetValue(fan.Identifier, out string? label)
                                  && label.Length > 0 ? label : fan.Name;
                    sb.AppendLine($"{name}  {rpm,4:0} RPM");
                }
            }
        }

        Text = sb.ToString().TrimEnd();
    }

    // Kiinteät kenttäleveydet + tasalevyinen fontti = rivien leveys ei väpätä
    // arvojen eläessä (esim. "9 %" vs "11 %").
    private static string Pct(float? v) => v is { } x ? $"{x,3:0} %" : "  —";

    private static string Temp(float? v) => v is { } x ? $"{x,3:0} °C" : "  —";

    private static string Num(float? v, string unit) => v is { } x ? $"{x,4:0} {unit}" : "   —";

    /// <summary>Lyhentää pitkät levynimet overlayn kompakteille riveille.</summary>
    private static string Shorten(string name) =>
        name.Length <= 20 ? name : name[..20].TrimEnd();

    public event PropertyChangedEventHandler? PropertyChanged;
}
