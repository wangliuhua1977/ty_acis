namespace TianyiVision.Acis.UI.States;

public sealed record InspectionReviewDetailState(
    string PointName,
    string UnitName,
    string InspectionResult,
    string OnlineStatus,
    string PlaybackStatus,
    string ImageStatus,
    string FaultType,
    string FaultDescription,
    string ScreenshotTitle,
    string DispatchPoolEntry,
    string LastFaultTime,
    string LastInspectionConclusion,
    string ReviewStatusText,
    string ReviewNotePlaceholder,
    bool IsReviewed,
    bool IsFault);
