namespace HardwareMonitor.Core.Maintenance;

/// <summary>
/// WMI:n DriverVersion (esim. "32.0.15.4680") → NVIDIAn markkinointiversio
/// ("546.80"): kahden viimeisen kentän numerot yhteen ja viisi viimeistä
/// merkkiä muodossa xxx.xx. Ilman muunnosta arvoa ei voi verrata NVIDIAn
/// sivulla näkyvään versionumeroon.
/// </summary>
public static class NvidiaDriverVersion
{
    public static string? ToMarketingVersion(string? wmiVersion)
    {
        string[] parts = (wmiVersion ?? "").Split('.');
        if (parts.Length < 4)
        {
            return null;
        }

        string digits = string.Concat(parts[^2], parts[^1]);
        if (digits.Length < 5 || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        string tail = digits[^5..];
        return $"{tail[..3]}.{tail[3..]}";
    }
}
