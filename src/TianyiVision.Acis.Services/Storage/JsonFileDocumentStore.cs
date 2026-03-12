using System.Text.Json;

namespace TianyiVision.Acis.Services.Storage;

public sealed class JsonFileDocumentStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public T LoadOrCreate<T>(string filePath, Func<T> createDefault)
    {
        if (!File.Exists(filePath))
        {
            var snapshot = createDefault();
            Save(filePath, snapshot);
            return snapshot;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<T>(stream, SerializerOptions) ?? createDefault();
        }
        catch
        {
            var snapshot = createDefault();
            Save(filePath, snapshot);
            return snapshot;
        }
    }

    public void Save<T>(string filePath, T document)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(filePath);
        JsonSerializer.Serialize(stream, document, SerializerOptions);
    }

    public void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
