using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
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

    private readonly OverlayViewModel _viewModel;
    private OverlaySettings _settings = new();
    private bool _moveMode;

    /// <summary>Laukeaa kun käyttäjä on raahannut overlayn uuteen paikkaan.</summary>
    public event Action<double, double>? PositionChangedByUser;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Salpalukko: ikkuna saa kasvaa muttei kutistua kesken istunnon,
        // jottei koko väpätä arvojen eläessä. Nollataan asetusmuutoksissa.
        SizeChanged += (_, _) =>
        {
            MinWidth = Math.Max(MinWidth, ActualWidth);
            MinHeight = Math.Max(MinHeight, ActualHeight);
            Reposition();
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    /// <summary>
    /// Siirtotila: läpi-klikattavuus pois, ikkunan voi raahata hiirellä.
    /// Pois kytkettäessä palautetaan läpi-klikattavuus.
    /// </summary>
    public void SetMoveMode(bool enabled)
    {
        _moveMode = enabled;
        IsHitTestVisible = enabled;
        Cursor = enabled ? Cursors.SizeAll : Cursors.Arrow;
        _viewModel.SetMoveModeVisual(enabled);

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        int style = GetWindowLong(handle, GwlExStyle);
        _ = SetWindowLong(handle, GwlExStyle, enabled
            ? style & ~(WsExTransparent | WsExNoActivate)
            : style | WsExTransparent | WsExNoActivate);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (_moveMode)
        {
            DragMove();
            PositionChangedByUser?.Invoke(Left, Top);
        }
    }

    /// <summary>Vie sijainti- ja ulkoasuasetukset ikkunaan ja asemoi sen uudelleen.</summary>
    public void ApplySettings(OverlaySettings settings)
    {
        _settings = settings;

        // Rivivalinnat ovat voineet muuttua — vapautetaan salpalukko, jotta
        // ikkuna voi myös pienentyä uuteen sisältöön.
        MinWidth = 0;
        MinHeight = 0;

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

    /// <summary>Asemoi ikkunan kulmaan tai käyttäjän raahaamaan paikkaan (DIP).</summary>
    private void Reposition()
    {
        if (_moveMode)
        {
            return; // ei taistella raahauksen kanssa
        }

        Rect workArea = SystemParameters.WorkArea;

        if (_settings.UseCustomPosition)
        {
            Left = Math.Clamp(_settings.CustomLeft, workArea.Left, Math.Max(workArea.Left, workArea.Right - ActualWidth));
            Top = Math.Clamp(_settings.CustomTop, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - ActualHeight));
            return;
        }

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
