using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HardwareMonitor.App.Localization;
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

            // Viite nollataan ennen sulkemista, ettei overlayn Closed-
            // käsittelijä tulkitse tätä odottamattomaksi ja luo uutta ikkunaa.
            OverlayWindow? overlay = _overlay;
            _overlay = null;
            overlay?.Close();

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
        menu.Items.Add(UiStrings.Tray_Show, null, (_, _) => RestoreFromTray());
        var overlayItem = new System.Windows.Forms.ToolStripMenuItem("Overlay")
        {
            CheckOnClick = true,
            Checked = _viewModel.OverlayEnabled,
        };
        overlayItem.CheckedChanged += (_, _) => _viewModel.OverlayEnabled = overlayItem.Checked;
        menu.Items.Add(overlayItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(UiStrings.Tray_Exit, null, (_, _) =>
        {
            _reallyExiting = true;
            Close();
        });
        menu.Opening += (_, _) => overlayItem.Checked = _viewModel.OverlayEnabled;
        _trayIcon.ContextMenuStrip = menu;
    }

    /// <summary>Näyttää pääikkunan; myös toinen instanssi pyytää tätä (App).</summary>
    public void RestoreFromTray()
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
                OverlayWindow created = _overlay;
                _overlay.Closed += (_, _) => OnOverlayClosed(created);
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

            // Viite nollataan ennen sulkemista = ohjattu sulkeminen.
            OverlayWindow closing = _overlay;
            _overlay = null;
            closing.Close();
        }
    }

    /// <summary>
    /// Itsekorjaus: jos overlay-ikkuna sulkeutuu ilman että sovellus sulki sen
    /// (esim. ulkopuolinen WM_CLOSE — havaittu 11.7.2026), luodaan se uudelleen
    /// kun asetus on yhä päällä.
    /// </summary>
    private void OnOverlayClosed(OverlayWindow window)
    {
        if (!ReferenceEquals(_overlay, window))
        {
            return; // ohjattu sulkeminen tai jo korvattu ikkuna
        }

        _overlay = null;
        if (_viewModel.OverlayEnabled)
        {
            _viewModel.LogOverlayUnexpectedClose();
            ApplyOverlaySettings();
        }
    }

    private void MoveOverlay_Checked(object sender, RoutedEventArgs e) =>
        _overlay?.SetMoveMode(true);

    private void MoveOverlay_Unchecked(object sender, RoutedEventArgs e) =>
        _overlay?.SetMoveMode(false);

    private void CreateReport_Click(object sender, RoutedEventArgs e) =>
        SaveAndOpen(
            content: _viewModel.BuildReport(),
            fileName: $"{UiStrings.Dlg_ReportFileName}-{DateTime.Now:yyyy-MM-dd}",
            filter: UiStrings.Dlg_ReportFilter,
            emptyMessage: UiStrings.Dlg_ReportEmpty);

    private void ExportCsv_Click(object sender, RoutedEventArgs e) =>
        SaveAndOpen(
            content: _viewModel.BuildCsv(),
            fileName: $"{UiStrings.Dlg_CsvFileName}-{DateTime.Now:yyyy-MM-dd}",
            filter: "CSV (Excel) (*.csv)|*.csv",
            emptyMessage: UiStrings.Dlg_CsvEmpty);

    /// <summary>Kopioi konetuntemus-lokin leikepöydälle tekoälychattiin liitettäväksi.</summary>
    private void CopyInsights_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.BuildMachineInsights() is not { } content)
        {
            MessageBox.Show(this, UiStrings.Dlg_InsightsEmpty, "Hardware Monitor",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Clipboard.SetText(content);
            MessageBox.Show(this, UiStrings.Dlg_InsightsCopied, "Hardware Monitor",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // Leikepöytä voi olla toisen prosessin varaama — kerrotaan syy.
            MessageBox.Show(this, string.Format(UiStrings.Dlg_SaveFailed, ex.Message),
                "Hardware Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveInsights_Click(object sender, RoutedEventArgs e) =>
        SaveAndOpen(
            content: _viewModel.BuildMachineInsights(),
            fileName: $"{UiStrings.Dlg_InsightsFileName}-{DateTime.Now:yyyy-MM-dd}",
            filter: UiStrings.Dlg_InsightsFilter,
            emptyMessage: UiStrings.Dlg_InsightsEmpty);

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
            MessageBox.Show(this, string.Format(UiStrings.Dlg_SaveFailed, ex.Message),
                "Hardware Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
