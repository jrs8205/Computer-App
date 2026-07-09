using System.Diagnostics.Eventing.Reader;

namespace HardwareMonitor.Core.WindowsEvents;

/// <summary>
/// Lukee System-lokin rautaan liittyvät tapahtumat EventLogReaderilla.
/// Provider-suodatus tehdään jo XPath-kyselyssä, jotta koko lokia ei
/// tarvitse käydä läpi; WindowsEventClassifier tekee lopullisen päätöksen.
/// Ohut kerros ilman logiikkaa — todennetaan ajamalla, ei yksikkötesteillä.
/// </summary>
public sealed class SystemEventReader : IWindowsEventSource
{
    private static readonly string[] Providers =
    {
        "Microsoft-Windows-Kernel-Power",
        "EventLog",
        "Microsoft-Windows-WER-SystemErrorReporting",
        "Microsoft-Windows-WHEA-Logger",
        "Display",
        "nvlddmkm",
        "disk",
        "Ntfs",
        "storahci",
        "stornvme",
    };

    public IReadOnlyList<WindowsLogEvent> ReadSince(long lastRecordId, TimeSpan maxAge)
    {
        string providerFilter = string.Join(" or ", Providers.Select(p => $"@Name='{p}'"));
        string xpath =
            $"*[System[(EventRecordID > {lastRecordId})" +
            $" and Provider[{providerFilter}]" +
            $" and TimeCreated[timediff(@SystemTime) <= {(long)maxAge.TotalMilliseconds}]]]";

        var query = new EventLogQuery("System", PathType.LogName, xpath);
        using var reader = new EventLogReader(query);

        var result = new List<WindowsLogEvent>();
        for (EventRecord? record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
        {
            using (record)
            {
                if (record.RecordId is not long recordId || record.TimeCreated is not DateTime time)
                {
                    continue;
                }

                result.Add(new WindowsLogEvent(
                    Time: new DateTimeOffset(time),
                    Provider: record.ProviderName ?? "",
                    EventId: record.Id,
                    WindowsLevel: record.Level ?? 4,
                    RecordId: recordId));
            }
        }

        return result;
    }
}
