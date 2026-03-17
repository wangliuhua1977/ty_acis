using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Inspection;

public static class InspectionEvidencePathBuilder
{
    public static string GetPointTaskDirectory(string taskId, string pointId)
    {
        var paths = new AcisLocalDataPaths();
        return Path.Combine(
            paths.InspectionDirectory,
            "evidence",
            SanitizeSegment(pointId),
            SanitizeSegment(taskId));
    }

    public static string BuildPlaybackScreenshotPath(string taskId, string pointId, int sequence, DateTime captureTime)
    {
        return Path.Combine(
            GetPointTaskDirectory(taskId, pointId),
            $"{captureTime:yyyyMMddHHmmssfff}-playback-{Math.Max(1, sequence):D2}.png");
    }

    public static string BuildFailureSnapshotPath(string taskId, string pointId, DateTime captureTime)
    {
        return Path.Combine(
            GetPointTaskDirectory(taskId, pointId),
            $"{captureTime:yyyyMMddHHmmssfff}-failure.png");
    }

    private static string SanitizeSegment(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            normalized = normalized.Replace(invalidChar, '_');
        }

        return normalized;
    }
}
