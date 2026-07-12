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

        // Jos loki on tyhjennetty, RecordID:t alkavat taas pienestä — vanha
        // suuri kirjanmerkki mykistäisi valvonnan pysyvästi.
        if (lastRecordId > 0 && _source.ReadNewestRecordId() is { } newest && newest < lastRecordId)
        {
            lastRecordId = 0;
        }

        IReadOnlyList<WindowsLogEvent> events = _source.ReadSince(lastRecordId, MaxAge);
        if (events.Count == 0)
        {
            return 0;
        }

        var rows = new List<EventRow>();
        long maxRecordId = lastRecordId;
        foreach (WindowsLogEvent e in events)
        {
            maxRecordId = Math.Max(maxRecordId, e.RecordId);

            WindowsEventClassification? c = WindowsEventClassifier.Classify(e);
            if (c is null)
            {
                continue;
            }

            rows.Add(new EventRow(e.Time, c.Level, c.Component,
                Sensor: e.Provider, Value: e.EventId, Threshold: null, Message: c.Message));
        }

        // Tapahtumat ja kirjanmerkki samassa transaktiossa — keskeytynyt
        // skannaus ei saa tuottaa duplikaatteja seuraavalla kierroksella.
        _db.InsertEventsWithMeta(rows, BookmarkKey, maxRecordId.ToString());
        return rows.Count;
    }
}
