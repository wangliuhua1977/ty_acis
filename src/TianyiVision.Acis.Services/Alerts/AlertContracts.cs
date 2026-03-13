using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Alerts;

public sealed record AiAlertQueryDto(
    string? DeviceCode,
    DateTime? StartTime,
    DateTime? EndTime,
    string? AlertTypeList,
    int? AlertSource,
    int PageNo,
    int PageSize);

public sealed record DeviceAlertQueryDto(
    string DeviceCode,
    DateTime? StartTime,
    DateTime? EndTime,
    string? AlertTypeList,
    int? AlertSource,
    int PageNo,
    int PageSize);

public interface IAlertQueryService
{
    ServiceResponse<IReadOnlyList<FaultAlertDto>> GetAiAlerts(AiAlertQueryDto query);

    ServiceResponse<IReadOnlyList<FaultAlertDto>> GetDeviceAlerts(DeviceAlertQueryDto query);
}
