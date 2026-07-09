namespace HardwareMonitor.Core.WindowsEvents;

/// <summary>
/// Windowsin tapahtumalokin lukulähde. Tuotannossa SystemEventReader
/// (EventLogReader); testeissä fake.
/// </summary>
public interface IWindowsEventSource
{
    /// <summary>
    /// Palauttaa tapahtumat, joiden EventRecordID on suurempi kuin
    /// <paramref name="lastRecordId"/> ja jotka ovat enintään
    /// <paramref name="maxAge"/> vanhoja.
    /// </summary>
    IReadOnlyList<WindowsLogEvent> ReadSince(long lastRecordId, TimeSpan maxAge);
}
