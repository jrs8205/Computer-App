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

    /// <summary>
    /// Lokin uusimman tapahtuman EventRecordID, tai null jos loki on tyhjä.
    /// Jos tämä on pienempi kuin talletettu kirjanmerkki, loki on tyhjennetty
    /// ja numerointi alkanut alusta.
    /// </summary>
    long? ReadNewestRecordId();
}
