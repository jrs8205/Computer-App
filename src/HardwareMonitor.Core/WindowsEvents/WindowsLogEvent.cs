namespace HardwareMonitor.Core.WindowsEvents;

/// <summary>
/// Yksi Windowsin tapahtumalokin rivi pelkistettynä (määrittelyn luku 18).
/// WindowsLevel: 1=Critical, 2=Error, 3=Warning, 4=Information.
/// RecordId on System-lokin monotoninen EventRecordID, jota käytetään
/// kirjanmerkkinä duplikaattien estoon.
/// </summary>
public sealed record WindowsLogEvent(
    DateTimeOffset Time,
    string Provider,
    int EventId,
    byte WindowsLevel,
    long RecordId);
