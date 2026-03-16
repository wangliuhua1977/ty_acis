using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class FileInspectionTaskHistoryStore : IInspectionTaskHistoryStore
{
    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FileInspectionTaskHistoryStore(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public IReadOnlyList<InspectionTaskRecordModel> Load()
    {
        var snapshot = _documentStore.LoadOrCreate(_paths.InspectionTaskHistoryFile, () => new InspectionTaskHistoryDocument([]));
        return (snapshot.Tasks ?? Array.Empty<InspectionTaskRecordModel>())
            .OrderByDescending(task => task.CreatedAt)
            .ToList();
    }

    public void Save(IReadOnlyList<InspectionTaskRecordModel> tasks)
    {
        _documentStore.Save(
            _paths.InspectionTaskHistoryFile,
            new InspectionTaskHistoryDocument(
                tasks
                    .OrderByDescending(task => task.CreatedAt)
                    .Take(40)
                    .ToList()));
    }

    private sealed record InspectionTaskHistoryDocument(IReadOnlyList<InspectionTaskRecordModel> Tasks);
}
