namespace HardwareMonitor.Core.Updates;

/// <summary>GitHub-releasen tiedot päivitysilmoitusta varten.</summary>
public sealed record UpdateInfo(
    string Version, string ReleaseUrl, string? SetupAssetUrl, string ReleaseNotes);
