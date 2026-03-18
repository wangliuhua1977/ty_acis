using System.Collections.Generic;
using TianyiVision.Acis.Services.Inspection;

namespace TianyiVision.Acis.UI.States;

public sealed record InspectionPointDetailState(
    string TaskId,
    string PointId,
    string DeviceCode,
    string PointName,
    string UnitName,
    string CurrentHandlingUnit,
    string CurrentStatus,
    string CoordinateStatusText,
    string LastSyncTime,
    string OnlineStatus,
    string PlaybackStatus,
    string ImageStatus,
    string FaultType,
    string FaultDescription,
    bool IsPreviewAvailable,
    bool IsCoordinatePending,
    string LastFaultTime,
    string DispatchPoolEntry,
    string LastInspectionConclusion)
{
    public Uri? PreviewHostUri { get; init; }

    public string PreviewHostKind { get; init; } = string.Empty;

    public string PreviewBusinessTitle { get; init; } = string.Empty;

    public string PreviewBusinessDescription { get; init; } = string.Empty;

    public string OnlineCheckResult { get; init; } = string.Empty;

    public string StreamUrlAcquireResult { get; init; } = string.Empty;

    public string FinalPlaybackResult { get; init; } = string.Empty;

    public string PreviewFailureClassification { get; init; } = InspectionPreviewFailureClassifications.None;

    public int PlaybackAttemptCount { get; init; }

    public bool ProtocolFallbackUsed { get; init; }

    public int ScreenshotPlannedCount { get; init; }

    public int ScreenshotIntervalSeconds { get; init; }

    public int ScreenshotSuccessCount { get; init; }

    public string EvidenceCaptureState { get; init; } = InspectionEvidenceValueKeys.CaptureStateNone;

    public string EvidenceSummary { get; init; } = string.Empty;

    public bool AllowManualSupplementScreenshot { get; init; }

    public string EvidenceRetentionMode { get; init; } = string.Empty;

    public int EvidenceRetentionDays { get; init; }

    public string AiAnalysisStatus { get; init; } = InspectionEvidenceValueKeys.AiAnalysisReserved;

    public string AiAnalysisSummary { get; init; } = string.Empty;

    public bool IsAiAbnormalDetected { get; init; }

    public string AiRecognitionSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> AiAbnormalTags { get; init; } = [];

    public double AiConfidence { get; init; }

    public string AiSuggestedAction { get; init; } = string.Empty;

    public bool RouteToReviewWallReserved { get; init; }

    public bool RouteToDispatchPoolReserved { get; init; }

    public bool ManualReviewRequiredReserved { get; init; }

    public string ReviewWallEntryStatus { get; init; } = string.Empty;

    public string DispatchCandidateEntryStatus { get; init; } = string.Empty;

    public string ManualSupplementEntryStatus { get; init; } = string.Empty;

    public string BusinessRoutingDescription { get; init; } = string.Empty;

    public string ManualSupplementEntryActionText { get; init; } = string.Empty;

    public bool HasGeneratedEvidence => ScreenshotSuccessCount > 0;

    public string ScreenshotReserved { get; init; } = "reserved";

    public string EvidenceReserved { get; init; } = "reserved";

    public string AiAnalysisReserved { get; init; } = "reserved";
}
