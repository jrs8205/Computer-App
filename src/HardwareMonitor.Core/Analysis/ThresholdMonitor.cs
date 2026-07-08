using HardwareMonitor.Core.Metrics;
using HardwareMonitor.Core.Settings;

namespace HardwareMonitor.Core.Analysis;

/// <summary>Mittarin välitön tila UI-värikoodausta varten.</summary>
public enum ThresholdState
{
    Normal,
    Warning,
    Critical,
}

/// <summary>Nostettu raja-arvotapahtuma (kirjataan events-tauluun).</summary>
public sealed record ThresholdEvent(
    string Level,
    string Component,
    string Sensor,
    double? Value,
    double? Threshold,
    string Message);

/// <summary>Välittömät tilat värikoodausta varten; Disks samassa järjestyksessä kuin KeyMetrics.Disks.</summary>
public sealed record MetricStates(
    ThresholdState CpuTemp,
    ThresholdState GpuTemp,
    ThresholdState GpuHotspot,
    ThresholdState RamLoad,
    IReadOnlyList<ThresholdState> Disks,
    ThresholdState Worst);

public sealed record ThresholdResult(MetricStates States, IReadOnlyList<ThresholdEvent> Events);

/// <summary>
/// Raja-arvovalvonta (määrittelyn luvut 16, 26, 31). Välitön tila muuttuu heti
/// rajan ylittyessä (käyttäjän pitää nähdä kuuma arvo), mutta tapahtuma
/// kirjataan vasta yhtäjaksoisen keston jälkeen — yksittäinen piikki ei ole
/// ongelma. Cooldown estää saman säännön toistuvan kirjautumisen; palautuessa
/// kirjataan INFO, jossa ylityksen kesto ja huippuarvo.
/// Puhdas tilakone: kello annetaan parametrina, joten kaikki on testattavissa.
/// </summary>
public sealed class ThresholdMonitor
{
    private readonly ThresholdSettings _s;
    private readonly Dictionary<string, RuleState> _rules = new();
    private readonly HashSet<string> _fansThatSpun = new();

    private sealed class RuleState
    {
        public DateTimeOffset? ExcursionStart;
        public DateTimeOffset? AboveWarnSince;
        public DateTimeOffset? AboveCritSince;
        public float Peak;
        public ThresholdState RaisedLevel;
        public DateTimeOffset? LastWarningEvent;
        public DateTimeOffset? LastCriticalEvent;
    }

    public ThresholdMonitor(ThresholdSettings settings) => _s = settings;

    public ThresholdResult Update(
        KeyMetrics m, DateTimeOffset now, IReadOnlyDictionary<string, string> fanLabels)
    {
        var events = new List<ThresholdEvent>();

        ThresholdState cpu = Check("cpu_temp", m.CpuPackageTempC,
            _s.CpuWarningTemp, _s.CpuCriticalTemp,
            "CPU", "CPU Package", "CPU-lämpötila", "°C", now, events);
        ThresholdState gpu = Check("gpu_temp", m.GpuTempC,
            _s.GpuWarningTemp, _s.GpuCriticalTemp,
            "GPU", "GPU Core", "GPU-lämpötila", "°C", now, events);
        ThresholdState hotspot = Check("gpu_hotspot", m.GpuHotspotTempC,
            _s.GpuHotspotWarningTemp, _s.GpuHotspotCriticalTemp,
            "GPU", "GPU Hot Spot", "GPU hotspot -lämpötila", "°C", now, events);
        ThresholdState ram = Check("ram_load", m.RamLoadPercent,
            _s.RamWarningPercent, _s.RamCriticalPercent,
            "RAM", "Memory", "RAM-käyttö", "%", now, events);

        var disks = new List<ThresholdState>();
        for (int i = 0; i < m.Disks.Count; i++)
        {
            DiskMetrics disk = m.Disks[i];
            disks.Add(Check($"disk:{i}", disk.TemperatureC,
                _s.NvmeWarningTemp, _s.NvmeCriticalTemp,
                "Levy", disk.Name, $"Levyn {disk.Name} lämpötila", "°C", now, events));
        }

        ThresholdState fans = CheckFans(m, now, fanLabels, events);

        ThresholdState worst = disks.Append(cpu).Append(gpu).Append(hotspot)
            .Append(ram).Append(fans).Max();

        return new ThresholdResult(new MetricStates(cpu, gpu, hotspot, ram, disks, worst), events);
    }

    private ThresholdState Check(
        string key, float? value, float warn, float crit,
        string component, string sensor, string label, string unit,
        DateTimeOffset now, List<ThresholdEvent> events)
    {
        RuleState state = GetRule(key);

        ThresholdState instant = value is { } v
            ? v >= crit ? ThresholdState.Critical
              : v >= warn ? ThresholdState.Warning
              : ThresholdState.Normal
            : ThresholdState.Normal;

        if (instant == ThresholdState.Normal)
        {
            CloseExcursion(state, component, sensor, label, unit, warn, now, events);
            return instant;
        }

        float current = value!.Value;
        state.ExcursionStart ??= now;
        state.Peak = Math.Max(state.Peak, current);
        state.AboveWarnSince = current >= warn ? state.AboveWarnSince ?? now : null;
        state.AboveCritSince = current >= crit ? state.AboveCritSince ?? now : null;

        if (state.AboveCritSince is { } critSince
            && (now - critSince).TotalSeconds >= _s.CriticalSustainSeconds
            && state.RaisedLevel < ThresholdState.Critical
            && CooldownOk(state.LastCriticalEvent, now))
        {
            events.Add(new ThresholdEvent("CRITICAL", component, sensor, current, crit,
                $"{label} ylitti kriittisen rajan: {current:0} {unit} (raja {crit:0} {unit})"));
            state.RaisedLevel = ThresholdState.Critical;
            state.LastCriticalEvent = now;
        }
        else if (state.AboveWarnSince is { } warnSince
            && (now - warnSince).TotalSeconds >= _s.WarningSustainSeconds
            && state.RaisedLevel < ThresholdState.Warning
            && CooldownOk(state.LastWarningEvent, now))
        {
            events.Add(new ThresholdEvent("WARNING", component, sensor, current, warn,
                $"{label} ylitti varoitusrajan: {current:0} {unit} (raja {warn:0} {unit})"));
            state.RaisedLevel = ThresholdState.Warning;
            state.LastWarningEvent = now;
        }

        return instant;
    }

    private ThresholdState CheckFans(
        KeyMetrics m, DateTimeOffset now,
        IReadOnlyDictionary<string, string> fanLabels, List<ThresholdEvent> events)
    {
        ThresholdState worst = ThresholdState.Normal;

        foreach (FanMetrics fan in m.Fans)
        {
            if (fan.Rpm is { } rpm && rpm > 200)
            {
                _fansThatSpun.Add(fan.Identifier);
            }

            string name = fanLabels.TryGetValue(fan.Identifier, out string? label)
                          && label.Length > 0 ? label : fan.Name;
            RuleState state = GetRule($"fan:{fan.Identifier}");

            bool stoppedWhileHot =
                _fansThatSpun.Contains(fan.Identifier)
                && fan.Rpm is { } r && r <= 0
                && m.CpuPackageTempC is { } cpuTemp && cpuTemp >= _s.FanStopCpuTemp;

            if (stoppedWhileHot)
            {
                worst = ThresholdState.Critical;
                state.ExcursionStart ??= now;
                state.AboveCritSince ??= now;
                state.Peak = Math.Max(state.Peak, m.CpuPackageTempC ?? 0);

                if ((now - state.AboveCritSince.Value).TotalSeconds >= _s.CriticalSustainSeconds
                    && state.RaisedLevel < ThresholdState.Critical
                    && CooldownOk(state.LastCriticalEvent, now))
                {
                    events.Add(new ThresholdEvent("CRITICAL", "Tuuletin", name, 0, null,
                        $"Tuuletin {name} on pysähtynyt (0 RPM) CPU:n ollessa {m.CpuPackageTempC:0} °C"));
                    state.RaisedLevel = ThresholdState.Critical;
                    state.LastCriticalEvent = now;
                }
            }
            else if (state.ExcursionStart is not null)
            {
                if (state.RaisedLevel == ThresholdState.Critical)
                {
                    events.Add(new ThresholdEvent("INFO", "Tuuletin", name, fan.Rpm, null,
                        $"Tuuletin {name} pyörii taas"));
                }

                Reset(state);
            }
        }

        return worst;
    }

    private void CloseExcursion(
        RuleState state, string component, string sensor, string label, string unit,
        float warnLimit, DateTimeOffset now, List<ThresholdEvent> events)
    {
        if (state.ExcursionStart is { } start && state.RaisedLevel != ThresholdState.Normal)
        {
            events.Add(new ThresholdEvent("INFO", component, sensor, state.Peak, warnLimit,
                $"{label} palautui normaaliksi (huippu {state.Peak:0} {unit}, kesto {FormatDuration(now - start)})"));
        }

        Reset(state);
    }

    private static void Reset(RuleState state)
    {
        state.ExcursionStart = null;
        state.AboveWarnSince = null;
        state.AboveCritSince = null;
        state.Peak = 0;
        state.RaisedLevel = ThresholdState.Normal;
    }

    private RuleState GetRule(string key)
    {
        if (!_rules.TryGetValue(key, out RuleState? state))
        {
            state = new RuleState();
            _rules[key] = state;
        }

        return state;
    }

    private bool CooldownOk(DateTimeOffset? lastEvent, DateTimeOffset now) =>
        lastEvent is not { } last || (now - last).TotalMinutes >= _s.EventCooldownMinutes;

    private static string FormatDuration(TimeSpan d) =>
        d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes} min {d.Seconds} s" : $"{(int)d.TotalSeconds} s";
}
