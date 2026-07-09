using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using HardwareMonitor.App.Localization;
using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Metrics;

namespace HardwareMonitor.App.ViewModels;

/// <summary>
/// Dashboard-korttien näkymämalli: KeyMetrics-arvot valmiiksi muotoiltuina
/// merkkijonoina. Puuttuva arvo näytetään aina "—":na (specin virheenkäsittely).
/// </summary>
public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private string _cpuLoad = "—", _cpuTemp = "—", _cpuClock = "—", _cpuPower = "—";
    private string _gpuLoad = "—", _gpuTemp = "—", _gpuHotspot = "—", _gpuVram = "—", _gpuPower = "—";
    private string _ramLoad = "—", _ramUsed = "—";

    public string CpuLoad { get => _cpuLoad; private set => Set(ref _cpuLoad, value, nameof(CpuLoad)); }
    public string CpuTemp { get => _cpuTemp; private set => Set(ref _cpuTemp, value, nameof(CpuTemp)); }
    public string CpuClock { get => _cpuClock; private set => Set(ref _cpuClock, value, nameof(CpuClock)); }
    public string CpuPower { get => _cpuPower; private set => Set(ref _cpuPower, value, nameof(CpuPower)); }
    public string GpuLoad { get => _gpuLoad; private set => Set(ref _gpuLoad, value, nameof(GpuLoad)); }
    public string GpuTemp { get => _gpuTemp; private set => Set(ref _gpuTemp, value, nameof(GpuTemp)); }
    public string GpuHotspot { get => _gpuHotspot; private set => Set(ref _gpuHotspot, value, nameof(GpuHotspot)); }
    public string GpuVram { get => _gpuVram; private set => Set(ref _gpuVram, value, nameof(GpuVram)); }
    public string GpuPower { get => _gpuPower; private set => Set(ref _gpuPower, value, nameof(GpuPower)); }
    public string RamLoad { get => _ramLoad; private set => Set(ref _ramLoad, value, nameof(RamLoad)); }
    public string RamUsed { get => _ramUsed; private set => Set(ref _ramUsed, value, nameof(RamUsed)); }

    private string _summaryTitle = UiStrings.Dash_InitialTitle;
    private string _summaryObservations = UiStrings.Dash_Collecting;
    private string _summaryRecommendation = "";
    private ThresholdState _summaryState;

    /// <summary>"Koneen tila: X · Riskitaso: Y" (RiskAnalyzer, luku 19).</summary>
    public string SummaryTitle { get => _summaryTitle; private set => Set(ref _summaryTitle, value, nameof(SummaryTitle)); }

    public string SummaryObservations { get => _summaryObservations; private set => Set(ref _summaryObservations, value, nameof(SummaryObservations)); }

    public string SummaryRecommendation { get => _summaryRecommendation; private set => Set(ref _summaryRecommendation, value, nameof(SummaryRecommendation)); }

    /// <summary>Kokonaistila tilapisteen väriä varten (StateBrush-konvertteri).</summary>
    public ThresholdState SummaryState { get => _summaryState; private set => SetState(ref _summaryState, value, nameof(SummaryState)); }

    /// <summary>Vie riskianalyysin tuloksen tilapaneeliin.</summary>
    public void ApplySummary(RiskAssessment assessment)
    {
        SummaryTitle = string.Format(UiStrings.Dash_SummaryTitle,
            assessment.Status, assessment.RiskLevel);
        SummaryState = assessment.Level;
        SummaryObservations = string.Join("\n", assessment.Observations.Select(o => "•  " + o));
        SummaryRecommendation = assessment.Recommendation is { } r
            ? string.Format(UiStrings.Dash_Recommendation, r)
            : "";
    }

    private ThresholdState _cpuTempState, _gpuTempState, _gpuHotspotState, _ramLoadState;

    public ThresholdState CpuTempState { get => _cpuTempState; private set => SetState(ref _cpuTempState, value, nameof(CpuTempState)); }
    public ThresholdState GpuTempState { get => _gpuTempState; private set => SetState(ref _gpuTempState, value, nameof(GpuTempState)); }
    public ThresholdState GpuHotspotState { get => _gpuHotspotState; private set => SetState(ref _gpuHotspotState, value, nameof(GpuHotspotState)); }
    public ThresholdState RamLoadState { get => _ramLoadState; private set => SetState(ref _ramLoadState, value, nameof(RamLoadState)); }

    public ObservableCollection<DiskRowViewModel> Disks { get; } = new();
    public ObservableCollection<FanRowViewModel> Fans { get; } = new();

    /// <summary>Vie raja-arvovalvonnan välittömät tilat kortteihin (kutsu Updaten jälkeen).</summary>
    public void ApplyStates(MetricStates states)
    {
        CpuTempState = states.CpuTemp;
        GpuTempState = states.GpuTemp;
        GpuHotspotState = states.GpuHotspot;
        RamLoadState = states.RamLoad;

        for (int i = 0; i < Disks.Count; i++)
        {
            Disks[i].State = i < states.Disks.Count ? states.Disks[i] : ThresholdState.Normal;
        }
    }

    /// <summary>Asetetaan MainViewModelista; välittää nimenmuutokset tallennettavaksi.</summary>
    public Action<string, string>? RenameFan { get; set; }

    public void Update(KeyMetrics m, IReadOnlyDictionary<string, string> fanLabels)
    {
        CpuLoad = Fmt(m.CpuLoadPercent, "%");
        CpuTemp = Fmt(m.CpuPackageTempC, "°C");
        CpuClock = Fmt(m.CpuMaxClockMhz, "MHz");
        CpuPower = Fmt(m.CpuPackagePowerW, "W");
        GpuLoad = Fmt(m.GpuLoadPercent, "%");
        GpuTemp = Fmt(m.GpuTempC, "°C");
        GpuHotspot = Fmt(m.GpuHotspotTempC, "°C");
        GpuVram = m.GpuMemoryUsedMb is { } used && m.GpuMemoryTotalMb is { } total
            ? $"{used:0} / {total:0} MB"
            : "—";
        GpuPower = Fmt(m.GpuPowerW, "W");
        RamLoad = Fmt(m.RamLoadPercent, "%");
        RamUsed = m.RamUsedGb is { } usedGb
            ? m.RamAvailableGb is { } availGb
                ? $"{usedGb.ToString("0.0", CultureInfo.CurrentCulture)} / {(usedGb + availGb).ToString("0.0", CultureInfo.CurrentCulture)} GB"
                : $"{usedGb.ToString("0.0", CultureInfo.CurrentCulture)} GB"
            : "—";

        SyncDisks(m.Disks);
        SyncFans(m.Fans, fanLabels);
    }

    /// <summary>Päivittää levyrivit paikallaan (järjestys = KeyMetrics.Disks).</summary>
    private void SyncDisks(IReadOnlyList<DiskMetrics> disks)
    {
        while (Disks.Count > disks.Count)
        {
            Disks.RemoveAt(Disks.Count - 1);
        }

        for (int i = 0; i < disks.Count; i++)
        {
            string text = $"{disks[i].Name}   {Fmt(disks[i].TemperatureC, "°C")}   {Fmt(disks[i].ActivityPercent, "%")}";
            if (i >= Disks.Count)
            {
                Disks.Add(new DiskRowViewModel { Text = text });
            }
            else
            {
                Disks[i].Text = text;
            }
        }
    }

    /// <summary>Päivittää tuuletinrivit paikallaan tunnisteen mukaan; ei ylikirjoita kesken muokkauksen.</summary>
    private void SyncFans(IReadOnlyList<FanMetrics> fans, IReadOnlyDictionary<string, string> labels)
    {
        while (Fans.Count > fans.Count)
        {
            Fans.RemoveAt(Fans.Count - 1);
        }

        for (int i = 0; i < fans.Count; i++)
        {
            FanMetrics fan = fans[i];
            string name = labels.TryGetValue(fan.Identifier, out string? label) && label.Length > 0
                ? label
                : fan.Name;
            string rpm = Fmt(fan.Rpm, "RPM");

            if (i >= Fans.Count || Fans[i].Identifier != fan.Identifier)
            {
                var row = new FanRowViewModel(fan.Identifier, name, (id, n) => RenameFan?.Invoke(id, n))
                {
                    Rpm = rpm,
                };
                if (i >= Fans.Count)
                {
                    Fans.Add(row);
                }
                else
                {
                    Fans[i] = row;
                }
            }
            else
            {
                if (!Fans[i].IsEditing)
                {
                    Fans[i].DisplayName = name;
                }

                Fans[i].Rpm = rpm;
            }
        }
    }

    private static string Fmt(float? value, string unit) =>
        value is { } v ? $"{v.ToString("0", CultureInfo.CurrentCulture)} {unit}" : "—";

    private void Set(ref string field, string value, string name)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void SetState(ref ThresholdState field, ThresholdState value, string name)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
