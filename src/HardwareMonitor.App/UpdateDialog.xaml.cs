using System.Windows;
using HardwareMonitor.App.Localization;
using HardwareMonitor.App.Services;
using HardwareMonitor.App.ViewModels;
using HardwareMonitor.Core.Updates;

namespace HardwareMonitor.App;

/// <summary>Uuden version tiedot + Asenna nyt / Myöhemmin.</summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _update;
    private readonly Action<string> _log;

    public UpdateDialog(UpdateInfo update, Action<string> log)
    {
        InitializeComponent();
        _update = update;
        _log = log;
        VersionText.Text = string.Format(
            UiStrings.Upd_VersionLine, update.Version, MainViewModel.CurrentVersion);
        NotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? UiStrings.Upd_NoNotes
            : update.ReleaseNotes;
    }

    private async void InstallNow_Click(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        StatusText.Text = UiStrings.Upd_Downloading;

        string? error = await UpdateInstaller.DownloadAndRunAsync(_update, _log);
        if (error is null)
        {
            // Installeri sulkee sovelluksen Restart Managerilla — dialogi vain pois.
            Close();
            return;
        }

        StatusText.Text = error;
        InstallButton.IsEnabled = true;
        LaterButton.IsEnabled = true;
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
