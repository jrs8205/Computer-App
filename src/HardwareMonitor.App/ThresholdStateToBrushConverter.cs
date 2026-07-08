using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HardwareMonitor.Core.Analysis;

namespace HardwareMonitor.App;

/// <summary>
/// ThresholdState -> väri: Normal vihreä, Warning oranssi, Critical punainen.
/// Parametrilla "border" Normal on läpinäkyvä (overlayn reunus näkyy vain hälyttäessä).
/// </summary>
public sealed class ThresholdStateToBrushConverter : IValueConverter
{
    private static readonly Brush NormalBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA5, 0xD6, 0xA7)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)));
    private static readonly Brush CriticalBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool border = parameter as string == "border";
        return value is ThresholdState state
            ? state switch
            {
                ThresholdState.Critical => CriticalBrush,
                ThresholdState.Warning => WarningBrush,
                _ => border ? Brushes.Transparent : NormalBrush,
            }
            : border ? Brushes.Transparent : NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
