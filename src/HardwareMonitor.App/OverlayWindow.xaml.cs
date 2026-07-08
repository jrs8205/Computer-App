using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using HardwareMonitor.App.ViewModels;
using HardwareMonitor.Core.Settings;
using Microsoft.Win32;

namespace HardwareMonitor.App;

/// <summary>
/// Läpi-klikattava always-on-top-overlay (specin Vaihe 2.5). Ikkuna ei ota
/// fokusta, ei näy Alt+Tabissa eikä tehtäväpalkissa, ja hiiren klikkaukset
/// menevät sen läpi alla olevaan sovellukseen (WS_EX_TRANSPARENT).
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private OverlaySettings _settings = new();

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        SizeChanged += (_, _) => Reposition();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    /// <summary>Vie sijainti- ja ulkoasuasetukset ikkunaan ja asemoi sen uudelleen.</summary>
    public void ApplySettings(OverlaySettings settings)
    {
        _settings = settings;
        Reposition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Läpi-klikattavuus + ei fokusta + ei Alt+Tabia. Asetetaan kerran,
        // kun ikkunakahva on olemassa.
        var handle = new WindowInteropHelper(this).Handle;
        int style = GetWindowLong(handle, GwlExStyle);
        _ = SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(Reposition);

    /// <summary>Asemoi ikkunan valittuun työalueen kulmaan (DIP-yksiköissä).</summary>
    private void Reposition()
    {
        Rect workArea = SystemParameters.WorkArea;
        double margin = _settings.MarginPx;

        Left = _settings.Corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft
            ? workArea.Left + margin
            : workArea.Right - ActualWidth - margin;

        Top = _settings.Corner is OverlayCorner.TopLeft or OverlayCorner.TopRight
            ? workArea.Top + margin
            : workArea.Bottom - ActualHeight - margin;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
