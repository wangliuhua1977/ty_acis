namespace TianyiVision.Acis.Services.Contracts;

public sealed record DeviceListItemDto(
    string DeviceCode,
    string DeviceName,
    string DeviceType,
    string HandlingUnit,
    double Longitude,
    double Latitude,
    bool IsOnline);

public sealed record InspectionResultDto(
    string TaskId,
    string GroupId,
    string PointId,
    DateTime InspectedAt,
    bool IsOnline,
    bool IsPlayable,
    bool IsImageAbnormal,
    string FaultType,
    string FaultDescription);

public sealed record FaultAlertDto(
    string AlertId,
    string PointId,
    string PointName,
    string FaultType,
    string AlertSource,
    string Content,
    DateTime FirstDetectedAt,
    DateTime LatestDetectedAt,
    int RepeatCount);

public sealed record DispatchNotificationRequestDto(
    string CurrentHandlingUnit,
    string MaintainerName,
    string MaintainerPhone,
    string SupervisorName,
    string SupervisorPhone,
    string PointName,
    string FaultType,
    DateTime FaultDetectedAt,
    string? ScreenshotTitle);

public sealed record ReportQueryDto(
    string TimeRangeKey,
    string? CustomRangeText,
    string? InspectionGroupName,
    string? HandlingUnit,
    string? FaultType);
