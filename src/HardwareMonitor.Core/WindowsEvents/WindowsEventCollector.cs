using HardwareMonitor.Core.Storage;

namespace HardwareMonitor.Core.WindowsEvents;

/// <summary>
/// Lukee uudet Windows-tapahtumat lähteestä, luokittelee ne ja kirjaa
/// merkittävät events-tauluun tapahtuman omalla aikaleimalla. Kirjanmerkki
/// (viimeksi käsitelty EventRecordID) talletetaan meta-tauluun, joten
/// uudelleenkäynnistys ei tuota duplikaatteja.
/// </summary>
public sealed class WindowsEventCollector
{
    private const string BookmarkKey = "windows_last_record_id";
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly IWindowsEventSource _source;
    private readonly HistoryDb _db;

    public WindowsEventCollector(IWindowsEventSource source, HistoryDb db)
    {
        _source = source;
        _db = db;
    }

    /// <summary>Palauttaa kirjattujen tapahtumien määrän.</summary>
    public int Scan()
    {
        long lastRecordId = long.TryParse(_db.GetMeta(BookmarkKey), out long parsed) ? parsed : 0;
        IReadOnlyList<WindowsLogEvent> events = _source.ReadSince(lastRecordId, MaxAge);
        if (events.Count == 0)
        {
            return 0;
        }

        int written = 0;
        long maxRecordId = lastRecordId;
        foreach (WindowsLogEvent e in events)
        {
            maxRecordId = Math.Max(maxRecordId, e.RecordId);

            WindowsEventClassification? c = WindowsEventClassifier.Classify(e);
            if (c is null)
            {
                continue;
            }

            _db.InsertEvent(e.Time, c.Level, c.Component,
                sensor: e.Provider, value: e.EventId, threshold: null, message: c.Message);
            written++;
        }

        _db.SetMeta(BookmarkKey, maxRecordId.ToString());
        return written;
    }
}
