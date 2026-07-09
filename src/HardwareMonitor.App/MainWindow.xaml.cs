using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HardwareMonitor.App.ViewModels;
using HardwareMonitor.Core.Notifications;

namespace HardwareMonitor.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private OverlayWindow? _overlay;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _reallyExiting;
    private bool _monitoringStarted;

    public MainWindow(bool startInTray = false)
    {
        InitializeComponent();
        DataContext = _viewModel;
        CreateTrayIcon();

        // Trayhin käynnistyttäessä ikkunaa ei näytetä, joten Loaded ei laukea
        // — mittaus ja overlay on käynnistettävä heti tässä.
        if (startInTray)
        {
            StartMonitoring();
        }

        Loaded += (_, _) => StartMonitoring();

        // Pienennys -> trayhin (ikkuna piiloon), kun asetus on päällä.
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
            {
                Hide();
            }
        };

        // X -> trayhin, kun asetus on päällä; oikea lopetus tray-valikon kautta.
        Closing += (_, e) =>
        {
            if (_viewModel.MinimizeToTray && !_reallyExiting)
            {
                e.Cancel = true;
                Hide();
            }
        };

        Closed += (_, _) =>
        {
            _trayIcon?.Dispose();
            _overlay?.Close();
            _viewModel.Dispose();
        };
    }

    private void StartMonitoring()
    {
        if (_monitoringStarted)
        {
            return;
        }

        _monitoringStarted = true;
        _viewModel.NotificationRequested += ShowTrayNotification;
        _viewModel.Start();
        _viewModel.OverlaySettingsChanged += ApplyOverlaySettings;
        ApplyOverlaySettings();
    }

    /// <summary>Balloon-ilmoitus hälytyksestä; Windows 11 näyttää sen toast-ilmoituksena.</summary>
    private void ShowTrayNotification(TrayNotification notification)
    {
        _trayIcon?.ShowBalloonTip(
            10_000,
            notification.Title,
            notification.Message,
            notification.Severity == NotificationSeverity.Critical
                ? System.Windows.Forms.ToolTipIcon.Error
                : System.Windows.Forms.ToolTipIcon.Warning);
    }

    /// <summary>Tray-kuvake valikkoineen: Näytä / Overlay / Lopeta.</summary>
    private void CreateTrayIcon()
    {
        var iconStream = System.Windows.Application
            .GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Text = "Hardware Monitor",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Näytä", null, (_, _) => RestoreFromTray());
        var overlayItem = new System.Windows.Forms.ToolStripMenuItem("Overlay")
        {
            CheckOnClick = true,
            Checked = _viewModel.OverlayEnabled,
        };
        overlayItem.CheckedChanged += (_, _) => _viewModel.OverlayEnabled = overlayItem.Checked;
        menu.Items.Add(overlayItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Lopeta", null, (_, _) =>
        {
            _reallyExiting = true;
            Close();
        });
        menu.Opening += (_, _) => overlayItem.Checked = _viewModel.OverlayEnabled;
        _trayIcon.ContextMenuStrip = menu;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ResetThresholds_Click(object sender, RoutedEventArgs e) =>
        _viewModel.SettingsPage.ResetThresholds();

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
                // EI Owner-kytköstä: omistettu ikkuna piiloutuisi pääikkunan
                // mukana trayhin. Overlay suljetaan erikseen Closed-käsittelijässä.
                _overlay = new OverlayWindow(_viewModel.Overlay);
                _overlay.PositionChangedByUser += _viewModel.SetOverlayCustomPosition;
                _overlay.Show();

                if (MoveOverlayCheck.IsChecked == true)
                {
                    _overlay.SetMoveMode(true);
                }
            }

            _overlay.ApplySettings(_viewModel.OverlaySettings);
        }
        else if (_overlay is not null)
        {
            MoveOverlayCheck.IsChecked = false;
            _overlay.Close();
            _overlay = null;
        }
    }

    private void MoveOverlay_Checked(object sender, RoutedEventArgs e) =>
        _overlay?.SetMoveMode(true);

    private void MoveOverlay_Unchecked(object sender, RoutedEventArgs e) =>
        _overlay?.SetMoveMode(false);

    private void CreateReport_Click(object sender, RoutedEventArgs e) =>
        SaveAndOpen(
            content: _viewModel.BuildReport(),
            fileName: $"Jarjestelmaraportti-{DateTime.Now:yyyy-MM-dd}",
            filter: "Tekstitiedosto (*.txt)|*.txt|Markdown (*.md)|*.md",
            emptyMessage: "Raporttia ei voi vielä luoda — sensoridataa ei ole ehtinyt kertyä.");

    private void ExportCsv_Click(object sender, RoutedEventArgs e) =>
        SaveAndOpen(
            content: _viewModel.BuildCsv(),
            fileName: $"Sensorihistoria-24h-{DateTime.Now:yyyy-MM-dd}",
            filter: "CSV (Excel) (*.csv)|*.csv",
            emptyMessage: "Vietävää historiaa ei vielä ole — lokitus on käynnissä, yritä hetken päästä.");

    /// <summary>Kysyy tallennuspaikan, kirjoittaa tiedoston ja avaa sen oletusohjelmassa.</summary>
    private void SaveAndOpen(string? content, string fileName, string filter, string emptyMessage)
    {
        if (content is null)
        {
            MessageBox.Show(this, emptyMessage, "Hardware Monitor",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            // BOM mukaan, jotta suomalainen Excel ja Notepad tunnistavat
            // UTF-8:n ja ääkköset näkyvät oikein.
            File.WriteAllText(dialog.FileName, content, new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Tallennus epäonnistui: {ex.Message}", "Hardware Monitor",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
