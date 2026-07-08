namespace HardwareMonitor.Core.Storage;

/// <summary>
/// Tapahtumaloki (määrittelyn luku 15): merkittävät tapahtumat tasoilla
/// INFO/WARNING/CRITICAL/ERROR. Vaihe 3 kirjaa sovelluksen elinkaaren;
/// Vaihe 4 lisää raja-arvotapahtumat samaan tauluun.
/// </summary>
public sealed class EventLogService
{
    private readonly HistoryDb _db;

    public EventLogService(HistoryDb db) => _db = db;

    public void Info(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("INFO", component, message, sensor, value, threshold);

    public void Warning(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("WARNING", component, message, sensor, value, threshold);

    public void Critical(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("CRITICAL", component, message, sensor, value, threshold);

    public void Error(string component, string message,
        string? sensor = null, double? value = null, double? threshold = null) =>
        Write("ERROR", component, message, sensor, value, threshold);

    private void Write(string level, string component, string message,
        string? sensor, double? value, double? threshold) =>
        _db.InsertEvent(DateTimeOffset.Now, level, component, sensor, value, threshold, message);
}
