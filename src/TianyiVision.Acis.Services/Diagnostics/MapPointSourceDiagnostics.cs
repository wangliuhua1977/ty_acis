using System.Diagnostics;
using System.Text;
using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Diagnostics;

public static class MapPointSourceDiagnostics
{
    private static readonly object SyncRoot = new();
    private static readonly string SessionStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private static readonly string SessionStartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    private static bool _headerWritten;

    public static string LogFilePath
        => Path.Combine(new AcisLocalDataPaths().RootDirectory, "logs", $"map-point-diagnostic-{SessionStamp}.log");

    public static void Write(string message)
    {
        Write("General", message);
    }

    public static void Write(string stage, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalizedStage = string.IsNullOrWhiteSpace(stage) ? "General" : stage.Trim();
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{normalizedStage}] {message}";
        Trace.WriteLine(line);

        try
        {
            lock (SyncRoot)
            {
                EnsureHeader();
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must not break the real device catalog pipeline.
        }
    }

    public static void WriteLines(string stage, IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            Write(stage, message);
        }
    }

    public static string MaskValue(string? value, int visibleSuffix = 4)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(missing)";
        }

        var normalized = value.Trim();
        if (normalized.Length <= visibleSuffix)
        {
            return new string('*', normalized.Length);
        }

        return $"{new string('*', Math.Max(4, normalized.Length - visibleSuffix))}{normalized[^visibleSuffix..]}";
    }

    public static string ClassifySourceTag(string? sourceTag)
    {
        if (string.IsNullOrWhiteSpace(sourceTag))
        {
            return "unknown";
        }

        return sourceTag.Trim().ToUpperInvariant() switch
        {
            "CTYUN" => "real",
            "DEMO" => "demo",
            _ => sourceTag.Trim()
        };
    }

    public static string SummarizeCounts(IEnumerable<KeyValuePair<string, int>> counts)
    {
        var entries = counts
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}")
            .ToList();

        return entries.Count == 0 ? "none" : string.Join(", ", entries);
    }

    private static void EnsureHeader()
    {
        if (_headerWritten)
        {
            return;
        }

        var directory = Path.GetDirectoryName(LogFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var header = new StringBuilder()
            .AppendLine("=== TianyiVision Map Point Diagnostic Session ===")
            .AppendLine($"StartedAt: {SessionStartedAt}")
            .AppendLine($"Machine: {Environment.MachineName}")
            .AppendLine($"Process: {AppDomain.CurrentDomain.FriendlyName}")
            .AppendLine($"LogFile: {LogFilePath}")
            .AppendLine("================================================");

        File.AppendAllText(LogFilePath, header.ToString(), Encoding.UTF8);
        _headerWritten = true;
    }
}
