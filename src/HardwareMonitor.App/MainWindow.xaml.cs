using System.Windows;
using HardwareMonitor.App.ViewModels;

namespace HardwareMonitor.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private OverlayWindow? _overlay;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            _viewModel.Start();
            _viewModel.OverlaySettingsChanged += ApplyOverlaySettings;
            ApplyOverlaySettings();
        };

        Closed += (_, _) =>
        {
            _overlay?.Close();
            _viewModel.Dispose();
        };
    }

    /// <summary>Avaa, sulkee ja asemoi overlayn asetusten mukaan.</summary>
    private void ApplyOverlaySettings()
    {
        if (_viewModel.OverlayEnabled)
        {
            if (_overlay is null)
            {
                _overlay = new OverlayWindow(_viewModel.Overlay) { Owner = this };
                _overlay.Show();
            }

            _overlay.ApplySettings(_viewModel.OverlaySettings);
        }
        else if (_overlay is not null)
        {
            _overlay.Close();
            _overlay = null;
        }
    }
}
