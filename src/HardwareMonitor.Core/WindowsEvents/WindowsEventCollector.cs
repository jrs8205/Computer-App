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
    private const string LogCreatedKey = "windows_log_created_utc";
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

        // Lokin sukupolvi: tyhjennys luo lokitiedoston uudelleen, jolloin
        // luontiaika muuttuu. Tämä huomaa tyhjennyksen myös silloin, kun
        // uusi loki on ehtinyt kasvaa vanhan kirjanmerkin ohi (RecordID-
        // vertailu ei sitä erottaisi).
        string? createdValue = _source.ReadLogCreationTimeUtc()?.Ticks.ToString();
        string? storedCreated = _db.GetMeta(LogCreatedKey);
        bool generationChanged = createdValue is not null && storedCreated != createdValue;
        if (generationChanged && storedCreated is not null)
        {
            lastRecordId = 0;
        }

        // Jos loki on tyhjennetty, RecordID:t alkavat taas pienestä — vanha
        // suuri kirjanmerkki mykistäisi valvonnan pysyvästi.
        if (lastRecordId > 0 && _source.ReadNewestRecordId() is { } newest && newest < lastRecordId)
        {
            lastRecordId = 0;
        }

        IReadOnlyList<WindowsLogEvent> events = _source.ReadSince(lastRecordId, MaxAge);

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

        // Jos mitään ei muuttunut (ei tapahtumia, sama sukupolvi, ei nollausta),
        // ei kannata kirjoittaa. Muutoin kaikki muutokset yhteen transaktioon.
        bool bookmarkChanged = maxRecordId != lastRecordId || generationChanged;
        if (rows.Count == 0 && !bookmarkChanged)
        {
            return 0;
        }

        var meta = new List<(string, string)> { (BookmarkKey, maxRecordId.ToString()) };
        if (createdValue is not null)
        {
            // Sukupolvi kirjoitetaan SAMASSA transaktiossa kuin bookmark ja
            // tapahtumat — ei enää erillistä SetMetaa, joka voisi tallentua
            // vaikka tapahtumien luku/kirjoitus epäonnistuisi.
            meta.Add((LogCreatedKey, createdValue));
        }

        _db.InsertEventsWithMeta(rows, meta);
        return rows.Count;
    }
}
