using HardwareMonitor.Core.Analysis;
using HardwareMonitor.Core.Localization;

namespace HardwareMonitor.Core.Notifications;

/// <summary>Ilmoituksen vakavuus — App-kerros valitsee tämän mukaan balloon-ikonin.</summary>
public enum NotificationSeverity
{
    Warning,
    Critical,
}

/// <summary>Ilmaisinalueen balloon-ilmoituksen sisältö.</summary>
public sealed record TrayNotification(string Title, string Message, NotificationSeverity Severity);

/// <summary>
/// Muuntaa raja-arvotapahtumat yhdeksi tray-ilmoitukseksi (Vaihe 8).
/// Puhdas, ei UI-riippuvuuksia. INFO-tapahtumat (palautumiset) eivät laukaise
/// ilmoitusta. Saman kierroksen hälytykset yhdistetään yhteen ilmoitukseen,
/// koska NotifyIcon näyttää vain yhden balloonin kerrallaan; toistosuojana
/// toimii ThresholdMonitorin oma kesto + cooldown -logiikka.
/// </summary>
public static class NotificationBuilder
{
    /// <summary>NotifyIcon.BalloonTipText sietää enintään 255 merkkiä.</summary>
    private const int MaxMessageLength = 255;

    public static TrayNotification? Build(IReadOnlyList<ThresholdEvent> events, bool enabled)
    {
        if (!enabled)
        {
            return null;
        }

        List<ThresholdEvent> alerts = events.Where(e => e.Level is "WARNING" or "CRITICAL").ToList();
        if (alerts.Count == 0)
        {
            return null;
        }

        bool critical = alerts.Any(e => e.Level == "CRITICAL");
        string title = alerts.Count > 1
            ? string.Format(Strings.Notify_Multiple, alerts.Count)
            : critical ? Strings.Notify_Critical : Strings.Notify_Warning;

        string message = string.Join(Environment.NewLine, alerts.Select(e => e.Message));
        if (message.Length > MaxMessageLength)
        {
            message = message[..(MaxMessageLength - 1)] + "…";
        }

        return new TrayNotification(
            title, message, critical ? NotificationSeverity.Critical : NotificationSeverity.Warning);
    }
}
