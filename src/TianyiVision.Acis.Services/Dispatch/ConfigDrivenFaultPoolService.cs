using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed class ConfigDrivenFaultPoolService : IFaultPoolService
{
    private readonly IDeviceWorkspaceService _deviceWorkspaceService;
    private readonly IAlertQueryService _alertQueryService;
    private readonly TimeSpan _lookbackWindow;

    public ConfigDrivenFaultPoolService(
        IDeviceWorkspaceService deviceWorkspaceService,
        IAlertQueryService alertQueryService,
        TimeSpan? lookbackWindow = null)
    {
        _deviceWorkspaceService = deviceWorkspaceService;
        _alertQueryService = alertQueryService;
        _lookbackWindow = lookbackWindow ?? TimeSpan.FromHours(72);
    }

    public ServiceResponse<IReadOnlyList<FaultPoolItemModel>> GetFaultPool()
    {
        var devicePoolResponse = _deviceWorkspaceService.GetDevicePool();
        if (!devicePoolResponse.IsSuccess || devicePoolResponse.Data.Count == 0)
        {
            return ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], devicePoolResponse.Message);
        }

        var devices = devicePoolResponse.Data;
        var endTime = DateTime.Now;
        var startTime = endTime.Subtract(_lookbackWindow);

        var aiAlertsResponse = _alertQueryService.GetAiAlerts(new AiAlertQueryDto(
            null,
            startTime,
            endTime,
            null,
            null,
            1,
            50));

        var deviceAlerts = devices
            .SelectMany(device => _alertQueryService.GetDeviceAlerts(new DeviceAlertQueryDto(
                device.DeviceCode,
                startTime,
                endTime,
                null,
                null,
                1,
                20)).Data)
            .ToList();

        var alerts = aiAlertsResponse.Data
            .Concat(deviceAlerts)
            .ToList();

        if (alerts.Count == 0)
        {
            return ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], "最近时间窗内没有可聚合的 CTYun 告警。");
        }

        var devicesByCode = devices.ToDictionary(device => device.DeviceCode, StringComparer.Ordinal);
        var faultPool = alerts
            .GroupBy(alert => new { alert.PointId, alert.FaultType })
            .Select(group => MapGroup(group.ToList(), devicesByCode))
            .OrderBy(item => item.WorkOrderStatus == DispatchWorkOrderStatusModel.PendingDispatch ? 0 : 1)
            .ThenBy(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered ? 0 : 1)
            .ThenByDescending(item => item.LatestDetectedAt)
            .ToList();

        return ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Success(faultPool, devicePoolResponse.Message);
    }

    private FaultPoolItemModel MapGroup(
        IReadOnlyList<FaultAlertDto> alerts,
        IReadOnlyDictionary<string, DevicePoolItemModel> devicesByCode)
    {
        var latest = alerts.OrderByDescending(item => item.LatestDetectedAt).First();
        devicesByCode.TryGetValue(latest.PointId, out var device);

        var currentHandlingUnit = device?.UnitName ?? "待补齐所属单位";
        var responsibility = CreateResponsibility(currentHandlingUnit);
        var isOfflineFault = latest.FaultType.Contains("离线", StringComparison.Ordinal);
        var isAutomatic = isOfflineFault;

        return new FaultPoolItemModel(
            $"{latest.PointId}:{latest.FaultType}",
            latest.PointId,
            latest.PointName,
            latest.FaultType,
            "CTYun 实时目录组",
            currentHandlingUnit,
            device?.AreaName ?? currentHandlingUnit,
            latest.Content,
            "故障",
            $"{latest.FaultType}告警快照",
            latest.AlertSource,
            true,
            latest.LatestDetectedAt.Date == DateTime.Today,
            isAutomatic ? DispatchMethodModel.Automatic : DispatchMethodModel.Manual,
            DispatchWorkOrderStatusModel.PendingDispatch,
            isOfflineFault ? DispatchRecoveryStatusModel.Unrecovered : DispatchRecoveryStatusModel.Unrecovered,
            responsibility,
            new DispatchNotificationRecordModel("--", "待发送", "--", "待发送"),
            alerts.Min(item => item.FirstDetectedAt),
            alerts.Max(item => item.LatestDetectedAt),
            alerts.Count,
            alerts.Select(item => item.AlertSource).Distinct(StringComparer.Ordinal).ToList());
    }

    private static DispatchResponsibilityModel CreateResponsibility(string currentHandlingUnit)
    {
        // 当前未接入真实责任归属映射，先保留统一挂接位置，供后续派单页替换。
        return new DispatchResponsibilityModel(
            currentHandlingUnit,
            "待配置维护人",
            "--",
            "待配置负责人",
            "--");
    }
}

public sealed class DemoFaultPoolService : IFaultPoolService
{
    private readonly IDispatchNotificationService _dispatchNotificationService;

    public DemoFaultPoolService(IDispatchNotificationService dispatchNotificationService)
    {
        _dispatchNotificationService = dispatchNotificationService;
    }

    public ServiceResponse<IReadOnlyList<FaultPoolItemModel>> GetFaultPool()
    {
        var response = _dispatchNotificationService.GetWorkOrders();
        if (!response.IsSuccess || response.Data.Count == 0)
        {
            return ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], response.Message);
        }

        var items = response.Data
            .Select(item => new FaultPoolItemModel(
                item.WorkOrderId,
                item.PointId,
                item.PointName,
                item.FaultType,
                item.InspectionGroupName,
                item.Responsibility.CurrentHandlingUnit,
                item.MapLocationPlaceholder,
                item.FaultSummary,
                item.LatestInspectionConclusion,
                item.ScreenshotTitle,
                item.ScreenshotSubtitle,
                item.EntersDispatchPool,
                item.IsTodayNew,
                item.DispatchMethod,
                item.WorkOrderStatus,
                item.RecoveryStatus,
                item.Responsibility,
                item.NotificationRecord,
                ParseDateTime(item.RepeatFault.FirstFaultTime),
                ParseDateTime(item.RepeatFault.LatestFaultTime),
                item.RepeatFault.RepeatCount,
                ["Demo"]))
            .ToList();

        return ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Success(items, response.Message);
    }

    private static DateTime ParseDateTime(string rawValue)
    {
        return DateTime.TryParse(rawValue, out var parsed) ? parsed : DateTime.Now;
    }
}

public sealed class FallbackFaultPoolService : IFaultPoolService
{
    private readonly IFaultPoolService _primary;
    private readonly IFaultPoolService _fallback;

    public FallbackFaultPoolService(IFaultPoolService primary, IFaultPoolService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public ServiceResponse<IReadOnlyList<FaultPoolItemModel>> GetFaultPool()
    {
        ServiceResponse<IReadOnlyList<FaultPoolItemModel>> response;
        try
        {
            response = _primary.GetFaultPool();
        }
        catch (Exception ex)
        {
            response = ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], $"真实故障池调用异常。 {ex.Message}");
        }

        if (response.IsSuccess && response.Data.Count > 0)
        {
            return response;
        }

        var fallback = _fallback.GetFaultPool();
        return fallback.IsSuccess
            ? ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Success(
                fallback.Data,
                string.IsNullOrWhiteSpace(response.Message)
                    ? "已回退到 demo 故障池。"
                    : $"{response.Message} 已回退到 demo 故障池。")
            : fallback;
    }
}
