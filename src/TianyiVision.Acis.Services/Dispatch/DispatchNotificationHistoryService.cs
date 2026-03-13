using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed record DispatchNotificationHistoryEntry(
    string WorkOrderId,
    string PointId,
    string FaultType,
    string SendType,
    DateTime SentAt,
    string ChannelId,
    bool IsSuccess,
    string ResultText,
    string ResponseSummary,
    bool WasRealSend,
    bool UsedDemoFallback);

public sealed record DispatchNotificationHistorySnapshot(
    IReadOnlyList<DispatchNotificationHistoryEntry> Entries);

public interface IDispatchNotificationHistoryService
{
    DispatchNotificationHistorySnapshot Load();

    void Append(DispatchNotificationHistoryEntry entry);
}

public sealed class FileDispatchNotificationHistoryService : IDispatchNotificationHistoryService
{
    private const int MaxEntries = 2000;

    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FileDispatchNotificationHistoryService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public DispatchNotificationHistorySnapshot Load()
    {
        var snapshot = _documentStore.LoadOrCreate(
            _paths.DispatchNotificationHistoryFile,
            () => new DispatchNotificationHistorySnapshot([]));
        return Normalize(snapshot);
    }

    public void Append(DispatchNotificationHistoryEntry entry)
    {
        var snapshot = Load();
        var entries = snapshot.Entries.ToList();
        entries.Add(entry);
        if (entries.Count > MaxEntries)
        {
            entries = entries
                .OrderByDescending(item => item.SentAt)
                .Take(MaxEntries)
                .OrderBy(item => item.SentAt)
                .ToList();
        }

        _documentStore.Save(_paths.DispatchNotificationHistoryFile, new DispatchNotificationHistorySnapshot(entries));
    }

    private static DispatchNotificationHistorySnapshot Normalize(DispatchNotificationHistorySnapshot snapshot)
    {
        var entries = (snapshot.Entries ?? Array.Empty<DispatchNotificationHistoryEntry>())
            .Where(item => !string.IsNullOrWhiteSpace(item.WorkOrderId))
            .OrderBy(item => item.SentAt)
            .ToList();
        return new DispatchNotificationHistorySnapshot(entries);
    }
}
