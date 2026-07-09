using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.App.ViewModels;

/// <summary>Raja-arvorivi Asetukset-välilehdellä: varoitus + kriittinen kenttä.</summary>
public sealed record ThresholdRow(
    string Label, string Unit, NumericFieldViewModel Warn, NumericFieldViewModel Crit);

/// <summary>Yhden kentän rivi (kestot, lokitus); Note on harmaa lisähuomautus.</summary>
public sealed record FieldRow(
    string Label, string Unit, NumericFieldViewModel Field, string? Note = null);

/// <summary>
/// Asetukset-välilehden numerokentät validointeineen (spec
/// docs/superpowers/specs/2026-07-09-settings-page-design.md). Kelvollinen
/// arvo tallentuu ja vaikuttaa heti — ThresholdMonitor lukee samaa
/// ThresholdSettings-oliota, joten olioiden viitteet eivät vaihdu missään.
/// Checkboxit ja kulma/läpinäkyvyys sidotaan MainViewModelin propertyihin.
/// </summary>
public sealed class SettingsViewModel
{
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly List<(NumericFieldViewModel Field, Func<float> Get)> _fields = new();

    public SettingsViewModel(AppSettings settings, Action save)
    {
        _settings = settings;
        _save = save;
        ThresholdSettings t = settings.Thresholds;

        ThresholdRows = new[]
        {
            Pair("CPU-lämpötila", "°C", 20, 120,
                () => t.CpuWarningTemp, v => t.CpuWarningTemp = v,
                () => t.CpuCriticalTemp, v => t.CpuCriticalTemp = v),
            Pair("GPU-lämpötila", "°C", 20, 120,
                () => t.GpuWarningTemp, v => t.GpuWarningTemp = v,
                () => t.GpuCriticalTemp, v => t.GpuCriticalTemp = v),
            Pair("GPU hotspot", "°C", 20, 120,
                () => t.GpuHotspotWarningTemp, v => t.GpuHotspotWarningTemp = v,
                () => t.GpuHotspotCriticalTemp, v => t.GpuHotspotCriticalTemp = v),
            Pair("NVMe-levyt", "°C", 20, 120,
                () => t.NvmeWarningTemp, v => t.NvmeWarningTemp = v,
                () => t.NvmeCriticalTemp, v => t.NvmeCriticalTemp = v),
            Pair("RAM-käyttö", "%", 10, 100,
                () => t.RamWarningPercent, v => t.RamWarningPercent = v,
                () => t.RamCriticalPercent, v => t.RamCriticalPercent = v),
        };

        DurationRows = new[]
        {
            Row("Varoituksen kesto ennen tapahtumaa", "s", 1, 600,
                () => t.WarningSustainSeconds,
                v => t.WarningSustainSeconds = (int)MathF.Round(v)),
            Row("Kriittisen kesto ennen tapahtumaa", "s", 1, 600,
                () => t.CriticalSustainSeconds,
                v => t.CriticalSustainSeconds = (int)MathF.Round(v)),
            Row("Saman hälytyksen väli (cooldown)", "min", 1, 60,
                () => t.EventCooldownMinutes,
                v => t.EventCooldownMinutes = (int)MathF.Round(v)),
            Row("Tuuletinpysähdyksen CPU-raja", "°C", 20, 120,
                () => t.FanStopCpuTemp, v => t.FanStopCpuTemp = v),
        };

        LoggingRows = new[]
        {
            Row("Koosteväli", "s", 1, 60,
                () => _settings.Logging.SensorIntervalSeconds,
                v => _settings.Logging.SensorIntervalSeconds = (int)MathF.Round(v),
                "vaikuttaa seuraavasta käynnistyksestä"),
            Row("Historian säilytys", "pv", 1, 365,
                () => _settings.Logging.KeepHistoryDays,
                v => _settings.Logging.KeepHistoryDays = (int)MathF.Round(v)),
        };

        OverlayFontSize = Register(
            new NumericFieldViewModel((float)settings.Overlay.FontSize, 8, 32,
                Apply(v => settings.Overlay.FontSize = v)),
            () => (float)settings.Overlay.FontSize);
    }

    public IReadOnlyList<ThresholdRow> ThresholdRows { get; }

    public IReadOnlyList<FieldRow> DurationRows { get; }

    public IReadOnlyList<FieldRow> LoggingRows { get; }

    public NumericFieldViewModel OverlayFontSize { get; }

    private static readonly string[] LanguageByIndex = { "", "fi", "en" };

    /// <summary>Kieli-ComboBoxin indeksi: 0 = automaattinen, 1 = fi, 2 = en. Vaikuttaa uudelleenkäynnistyksessä.</summary>
    public int LanguageIndex
    {
        get => Math.Max(0, Array.IndexOf(LanguageByIndex, _settings.Language));
        set
        {
            if (value < 0 || value >= LanguageByIndex.Length
                || _settings.Language == LanguageByIndex[value])
            {
                return;
            }

            _settings.Language = LanguageByIndex[value];
            _save();
        }
    }

    /// <summary>Palauttaa raja-arvot ja kestot oletuksiin. Viite säilyy!</summary>
    public void ResetThresholds()
    {
        var d = new ThresholdSettings();
        ThresholdSettings t = _settings.Thresholds;
        t.CpuWarningTemp = d.CpuWarningTemp;
        t.CpuCriticalTemp = d.CpuCriticalTemp;
        t.GpuWarningTemp = d.GpuWarningTemp;
        t.GpuCriticalTemp = d.GpuCriticalTemp;
        t.GpuHotspotWarningTemp = d.GpuHotspotWarningTemp;
        t.GpuHotspotCriticalTemp = d.GpuHotspotCriticalTemp;
        t.NvmeWarningTemp = d.NvmeWarningTemp;
        t.NvmeCriticalTemp = d.NvmeCriticalTemp;
        t.RamWarningPercent = d.RamWarningPercent;
        t.RamCriticalPercent = d.RamCriticalPercent;
        t.FanStopCpuTemp = d.FanStopCpuTemp;
        t.WarningSustainSeconds = d.WarningSustainSeconds;
        t.CriticalSustainSeconds = d.CriticalSustainSeconds;
        t.EventCooldownMinutes = d.EventCooldownMinutes;
        _save();

        foreach ((NumericFieldViewModel field, Func<float> get) in _fields)
        {
            field.Refresh(get());
        }
    }

    private ThresholdRow Pair(
        string label, string unit, float min, float max,
        Func<float> getWarn, Action<float> setWarn,
        Func<float> getCrit, Action<float> setCrit)
    {
        NumericFieldViewModel warn = Register(
            new NumericFieldViewModel(getWarn(), min, max, Apply(setWarn),
                v => SettingsValidator.ValidateWarnCrit(v, getCrit())),
            getWarn);
        NumericFieldViewModel crit = Register(
            new NumericFieldViewModel(getCrit(), min, max, Apply(setCrit),
                v => SettingsValidator.ValidateWarnCrit(getWarn(), v)),
            getCrit);
        return new ThresholdRow(label, unit, warn, crit);
    }

    private FieldRow Row(
        string label, string unit, float min, float max,
        Func<float> get, Action<float> set, string? note = null) =>
        new(label, unit,
            Register(new NumericFieldViewModel(get(), min, max, Apply(set)), get), note);

    private Action<float> Apply(Action<float> set) => v =>
    {
        set(v);
        _save();
    };

    private NumericFieldViewModel Register(NumericFieldViewModel field, Func<float> get)
    {
        _fields.Add((field, get));
        return field;
    }
}
