using HardwareMonitor.Core.Settings;
using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.Insights;

/// <summary>
/// MachineInsightsBuilderin syötteet yhtenä recordina, jotta signatuuri
/// ei kasva sisällön laajentuessa. Stats7d on trendivertailua varten.
/// </summary>
public sealed record MachineInsightsInput(
    DateTimeOffset Now,
    MachineSpec Spec,
    SampleStats Stats30d,
    SampleStats Stats7d,
    IReadOnlyList<EventRow> Events,
    ThresholdSettings Limits);
