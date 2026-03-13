using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Demo;

public sealed class DemoAlertQueryService : IAlertQueryService
{
    private static readonly IReadOnlyList<FaultAlertDto> AiAlerts =
    [
        new("ai-101", "p-104", "城市阳台主广场", "画面异常", "AI", "检测到画面异常巡检告警。", new DateTime(2026, 3, 12, 7, 54, 0), new DateTime(2026, 3, 12, 7, 54, 0), 1),
        new("ai-102", "p-111", "演艺广场东门", "画面异常", "AI", "检测到画面模糊与遮挡疑似告警。", new DateTime(2026, 3, 12, 10, 8, 0), new DateTime(2026, 3, 12, 10, 8, 0), 1)
    ];

    private static readonly IReadOnlyList<FaultAlertDto> DeviceAlerts =
    [
        new("dev-101", "p-102", "轮渡码头北口", "设备离线", "Device", "设备离线", new DateTime(2026, 3, 12, 8, 42, 0), new DateTime(2026, 3, 12, 8, 42, 0), 1),
        new("dev-102", "p-108", "江心灯塔监看点", "设备离线", "Device", "设备离线", new DateTime(2026, 3, 12, 6, 51, 0), new DateTime(2026, 3, 12, 6, 51, 0), 1),
        new("dev-103", "p-102", "轮渡码头北口", "画面变动", "Device", "画面变动告警", new DateTime(2026, 3, 12, 8, 39, 0), new DateTime(2026, 3, 12, 8, 41, 0), 2)
    ];

    public ServiceResponse<IReadOnlyList<FaultAlertDto>> GetAiAlerts(AiAlertQueryDto query)
    {
        var items = FilterByWindow(AiAlerts, query.StartTime, query.EndTime)
            .Where(item => string.IsNullOrWhiteSpace(query.DeviceCode) || item.PointId == query.DeviceCode)
            .ToList();

        return ServiceResponse<IReadOnlyList<FaultAlertDto>>.Success(items);
    }

    public ServiceResponse<IReadOnlyList<FaultAlertDto>> GetDeviceAlerts(DeviceAlertQueryDto query)
    {
        var items = FilterByWindow(DeviceAlerts, query.StartTime, query.EndTime)
            .Where(item => item.PointId == query.DeviceCode)
            .ToList();

        return ServiceResponse<IReadOnlyList<FaultAlertDto>>.Success(items);
    }

    private static IEnumerable<FaultAlertDto> FilterByWindow(IEnumerable<FaultAlertDto> alerts, DateTime? startTime, DateTime? endTime)
    {
        return alerts.Where(alert =>
            (!startTime.HasValue || alert.LatestDetectedAt >= startTime.Value)
            && (!endTime.HasValue || alert.FirstDetectedAt <= endTime.Value));
    }
}
