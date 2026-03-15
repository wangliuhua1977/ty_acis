using System.Diagnostics;
using System.Text;
using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Diagnostics;

public static class MapPointSourceDiagnostics
{
    private static readonly object SyncRoot = new();

    public static string LogFilePath
        => Path.Combine(new AcisLocalDataPaths().RootDirectory, "logs", "map-point-source.log");

    public static void Write(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        Trace.WriteLine(line);

        try
        {
            lock (SyncRoot)
            {
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
}
