using System.ComponentModel;
using HardwareMonitor.Core.Charts;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Historia-välilehden graafit (Vaihe 8.3). Data tulee puhtaalta
/// ChartHistoryBuilderilta; tämä luokka omistaa vain LiveCharts2-
/// esitysmuodon (sarjat, akselit, värit).
/// </summary>
public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private static readonly int[] RangeHoursByIndex = { 1, 24, 168, 720 };

    private static readonly SKColor[] Palette =
    {
        new(0x4F, 0xC3, 0xF7), // syaani (CPU)
        new(0x81, 0xC7, 0x84), // vihreä (GPU)
        new(0xFF, 0xB7, 0x4D), // oranssi (hotspot/RAM)
        new(0xBA, 0x68, 0xC8), // violetti
        new(0xF0, 0x62, 0x92), // pinkki
        new(0xFF, 0xD5, 0x4F), // keltainen
        new(0x4D, 0xB6, 0xAC), // teal
        new(0x90, 0xA4, 0xAE), // harmaa
    };

    private int _rangeIndex = 1; // 24 h

    /// <summary>Nostetaan kun aikaväli vaihtuu — MainViewModel hakee datan.</summary>
    public event Action? RefreshRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int RangeIndex
    {
        get => _rangeIndex;
        set
        {
            if (value == _rangeIndex || value < 0 || value >= RangeHoursByIndex.Length)
            {
                return;
            }

            _rangeIndex = value;
            OnChanged(nameof(RangeIndex));
            RefreshRequested?.Invoke();
        }
    }

    public int RangeHours => RangeHoursByIndex[_rangeIndex];

    public ISeries[] TempSeries { get; private set; } = Array.Empty<ISeries>();

    public ISeries[] LoadSeries { get; private set; } = Array.Empty<ISeries>();

    public ISeries[] FanSeries { get; private set; } = Array.Empty<ISeries>();

    public Axis[] TempXAxes { get; private set; } = new[] { TimeAxis(24) };

    public Axis[] LoadXAxes { get; private set; } = new[] { TimeAxis(24) };

    public Axis[] FanXAxes { get; private set; } = new[] { TimeAxis(24) };

    public Axis[] TempYAxes { get; } = new[] { ValueAxis(null, null) };

    public Axis[] LoadYAxes { get; } = new[] { ValueAxis(0, 100) };

    public Axis[] FanYAxes { get; } = new[] { ValueAxis(0, null) };

    public SolidColorPaint LegendPaint { get; } = new(SKColors.White) { SKTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) };

    public SolidColorPaint TooltipTextPaint { get; } = new(SKColors.White);

    public SolidColorPaint TooltipBackgroundPaint { get; } = new(new SKColor(0x25, 0x25, 0x26));

    /// <summary>Kutsutaan UI-säikeessä kun uusi data on haettu.</summary>
    public void Apply(ChartHistory history, int rangeHours)
    {
        TempSeries = ToSeries(history.Temperatures);
        LoadSeries = ToSeries(history.Loads);
        FanSeries = ToSeries(history.Fans);
        TempXAxes = new[] { TimeAxis(rangeHours) };
        LoadXAxes = new[] { TimeAxis(rangeHours) };
        FanXAxes = new[] { TimeAxis(rangeHours) };
        OnChanged(nameof(TempSeries));
        OnChanged(nameof(LoadSeries));
        OnChanged(nameof(FanSeries));
        OnChanged(nameof(TempXAxes));
        OnChanged(nameof(LoadXAxes));
        OnChanged(nameof(FanXAxes));
    }

    private static ISeries[] ToSeries(IReadOnlyList<ChartSeries> series) =>
        series.Select((s, i) => (ISeries)new LineSeries<DateTimePoint>
        {
            Name = s.Name,
            Values = s.Points
                .Select(p => new DateTimePoint(p.Timestamp.LocalDateTime, p.Value))
                .ToArray(),
            GeometrySize = 0,
            LineSmoothness = 0,
            Fill = null,
            Stroke = new SolidColorPaint(Palette[i % Palette.Length]) { StrokeThickness = 2.5f },
            // Kokonaisluku riittää tooltippiin (käyttäjän palaute: ei desimaaleja).
            YToolTipLabelFormatter = point => point.Coordinate.PrimaryValue.ToString("0"),
        }).ToArray();

    private static Axis TimeAxis(int rangeHours)
    {
        (TimeSpan unit, string format) = rangeHours switch
        {
            <= 1 => (TimeSpan.FromMinutes(10), "HH.mm"),
            <= 24 => (TimeSpan.FromHours(3), "HH.mm"),
            <= 168 => (TimeSpan.FromDays(1), "d.M."),
            _ => (TimeSpan.FromDays(5), "d.M."),
        };

        return new DateTimeAxis(unit, d => d.ToString(format))
        {
            LabelsPaint = new SolidColorPaint(SKColors.White),
            TextSize = 14,
            SeparatorsPaint = new SolidColorPaint(new SKColor(0x4A, 0x4A, 0x52))
            {
                StrokeThickness = 1,
            },
        };
    }

    private static Axis ValueAxis(double? min, double? max) => new()
    {
        MinLimit = min,
        MaxLimit = max,
        LabelsPaint = new SolidColorPaint(SKColors.White),
        TextSize = 14,
        SeparatorsPaint = new SolidColorPaint(new SKColor(0x4A, 0x4A, 0x52))
        {
            StrokeThickness = 1,
        },
    };

    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
