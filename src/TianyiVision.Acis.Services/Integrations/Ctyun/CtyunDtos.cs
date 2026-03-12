namespace TianyiVision.Acis.Services.Integrations.Ctyun;

public sealed record CtyunDeviceDetailDto(
    string DeviceCode,
    string DeviceName,
    string DeviceModel,
    int DeviceType,
    bool IsCloudCamera,
    string? Longitude,
    string? Latitude);

public sealed record CtyunInspectionResultDto(
    string DeviceCode,
    DateTime InspectTime,
    bool IsOnline,
    bool IsPlayable,
    bool IsImageAbnormal,
    string FaultType,
    string FaultDescription);

public sealed record CtyunFaultAlertDto(
    string AlertId,
    string DeviceCode,
    string DeviceName,
    int AlertType,
    string Content,
    DateTime CreateTime,
    DateTime? UpdateTime,
    string AlertSource);

public sealed record CtyunNotificationRequestDto(
    string EnterpriseUser,
    string PointName,
    string FaultType,
    DateTime FaultDetectedAt,
    string CurrentHandlingUnit,
    string MaintainerName,
    string MaintainerPhone,
    string SupervisorName,
    string SupervisorPhone);

public sealed record CtyunReportQueryDto(
    DateTime StartTime,
    DateTime EndTime,
    string? DeviceCode,
    string? AlertTypeList,
    int? PageNo,
    int? PageSize);
