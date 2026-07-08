using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void FanName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: FanRowViewModel row })
        {
            row.BeginEdit();
        }
    }

    private void FanName_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FanRowViewModel row })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            row.CommitEdit();
        }
        else if (e.Key == Key.Escape)
        {
            row.CancelEdit();
        }
    }

    private void FanName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FanRowViewModel row })
        {
            row.CommitEdit();
        }
    }

    private void FanName_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && (bool)e.NewValue)
        {
            box.Dispatcher.BeginInvoke(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }
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
