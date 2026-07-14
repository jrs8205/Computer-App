using System.Collections.ObjectModel;
using System.ComponentModel;
using HardwareMonitor.App.Localization;
using HardwareMonitor.App.Services;
using HardwareMonitor.Core.Insights;
using HardwareMonitor.Core.Maintenance;

namespace HardwareMonitor.App.ViewModels;

/// <summary>Ylläpito-välilehden laiterivi.</summary>
public sealed class MaintenanceRowViewModel
{
    public required string Kind { get; init; }
    public required string Model { get; init; }
    public required string VersionText { get; init; }
    public string? Url { get; init; }
    public bool HasLink => Url is not null;
}

/// <summary>
/// Ylläpito-välilehti: laitteiden nykyversiot + valmistajalinkit + sovelluksen
/// päivitystarkistuksen tila. Rivit rakennetaan kerran välilehden avauksessa.
/// </summary>
public sealed class MaintenanceViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MaintenanceRowViewModel> Rows { get; } = new();

    public string AppVersionText =>
        string.Format(UiStrings.Maint_AppVersion, MainViewModel.CurrentVersion);

    private string _checkStatus = "";

    public string CheckStatus
    {
        get => _checkStatus;
        private set
        {
            _checkStatus = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckStatus)));
        }
    }

    public void ReportCheckResult(UpdateCheckOutcome outcome)
    {
        string time = DateTime.Now.ToString("HH.mm");
        CheckStatus = outcome switch
        {
            UpdateCheckOutcome.UpToDate => string.Format(UiStrings.Maint_CheckUpToDate, time),
            UpdateCheckOutcome.UpdateAvailable => string.Format(UiStrings.Maint_CheckFound, time),
            _ => string.Format(UiStrings.Maint_CheckFailed, time),
        };
    }

    public void Load(MachineSpec spec, DeviceVersions versions, string language)
    {
        Rows.Clear();

        Rows.Add(new MaintenanceRowViewModel
        {
            Kind = UiStrings.Maint_Motherboard,
            Model = spec.MotherboardName ?? "—",
            VersionText = Format(UiStrings.Maint_BiosFormat, versions.BiosVersion, versions.BiosDate),
            Url = VendorLinkResolver.Resolve(spec.MotherboardName, language),
        });

        string? marketing = NvidiaDriverVersion.ToMarketingVersion(versions.GpuDriverVersion);
        string driverText = marketing is not null
            ? $"{marketing} ({versions.GpuDriverVersion})"
            : versions.GpuDriverVersion ?? "—";
        Rows.Add(new MaintenanceRowViewModel
        {
            Kind = UiStrings.Maint_Gpu,
            Model = spec.GpuName ?? "—",
            VersionText = Format(UiStrings.Maint_DriverFormat, driverText, versions.GpuDriverDate),
            Url = VendorLinkResolver.Resolve(spec.GpuName, language),
        });

        // WMI-firmware yhdistetään LHM-levynimeen mallinimellä; samannimiset
        // levyt kuluttavat osumia järjestyksessä (2 × 860 EVO tällä koneella).
        var firmwarePool = versions.DiskFirmware.ToList();
        foreach (string disk in spec.DiskNames)
        {
            string model = disk.Trim();
            int match = firmwarePool.FindIndex(f =>
                string.Equals(f.Model, model, StringComparison.OrdinalIgnoreCase));
            string firmware = "—";
            if (match >= 0)
            {
                firmware = firmwarePool[match].Firmware;
                firmwarePool.RemoveAt(match);
            }

            Rows.Add(new MaintenanceRowViewModel
            {
                Kind = UiStrings.Maint_Disk,
                Model = model,
                VersionText = string.Format(UiStrings.Maint_FirmwareFormat, firmware),
                Url = VendorLinkResolver.Resolve(model, language),
            });
        }
    }

    /// <summary>"BIOS 1401 (23.4.2024)" — versio aina, päiväys vain jos saatavilla.</summary>
    private static string Format(string format, string? value, DateTime? date)
    {
        string text = string.Format(format, value ?? "—");
        return date is { } d ? $"{text} ({d:d.M.yyyy})" : text;
    }
}
