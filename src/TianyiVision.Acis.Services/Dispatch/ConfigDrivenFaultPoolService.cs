using TianyiVision.Acis.Services.Alerts;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Diagnostics;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed class ConfigDrivenFaultPoolService : IFaultPoolService
{
    private readonly IDeviceWorkspaceService _deviceWorkspaceService;
    private readonly IAlertQueryService _alertQueryService;
    private readonly IDispatchResponsibilityService _responsibilityService;
    private readonly TimeSpan _lookbackWindow;
    private readonly TimeSpan _aiFaultIdleWindow;
    private readonly TimeSpan _snapshotCacheLifetime;
    private readonly TimeSpan _minimumRefreshInterval;
    private readonly object _snapshotCacheSyncRoot = new();
    private CachedFaultPoolResponse? _snapshotCache;

    public ConfigDrivenFaultPoolService(
        IDeviceWorkspaceService deviceWorkspaceService,
        IAlertQueryService alertQueryService,
        IDispatchResponsibilityService responsibilityService,
        TimeSpan? lookbackWindow = null,
        TimeSpan? aiFaultIdleWindow = null,
        TimeSpan? snapshotCacheLifetime = null,
        TimeSpan? minimumRefreshInterval = null)
    {
        _deviceWorkspaceService = deviceWorkspaceService;
        _alertQueryService = alertQueryService;
        _responsibilityService = responsibilityService;
        _lookbackWindow = lookbackWindow ?? TimeSpan.FromHours(72);
        _aiFaultIdleWindow = aiFaultIdleWindow ?? TimeSpan.FromHours(2);
        _snapshotCacheLifetime = snapshotCacheLifetime ?? TimeSpan.FromSeconds(30);
        _minimumRefreshInterval = minimumRefreshInterval ?? TimeSpan.FromSeconds(15);
    }

    public ServiceResponse<IReadOnlyList<FaultPoolItemModel>> GetFaultPool()
    {
        if (TryGetCachedFaultPool(out var cachedResponse))
        {
            return cachedResponse;
        }

        var devicePoolResponse = _deviceWorkspaceService.GetDevicePool();
        if (!devicePoolResponse.IsSuccess || devicePoolResponse.Data.Count == 0)
        {
            var failedResponse = ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], devicePoolResponse.Message);
            CacheFaultPool(failedResponse, TimeSpan.FromSeconds(10));
            return failedResponse;
        }

        var devices = devicePoolResponse.Data
            .GroupBy(device => device.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
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

        var deviceAlertResponses = devices
            .Select(device => _alertQueryService.GetDeviceAlerts(new DeviceAlertQueryDto(
                device.DeviceCode,
                startTime,
                endTime,
                null,
                null,
                1,
                20)))
            .ToList();
        var deviceAlerts = deviceAlertResponses
            .SelectMany(response => response.Data)
            .ToList();

        var allAlerts = aiAlertsResponse.Data
            .Concat(deviceAlerts)
            .Where(alert => !string.IsNullOrWhiteSpace(alert.PointId))
            .ToList();
        var hasSuccessfulAlertQuery = aiAlertsResponse.IsSuccess || deviceAlertResponses.Any(response => response.IsSuccess);
        if (!hasSuccessfulAlertQuery)
        {
            var failureMessages = deviceAlertResponses
                .Where(response => !string.IsNullOrWhiteSpace(response.Message))
                .Select(response => response.Message)
                .ToList();
            if (!string.IsNullOrWhiteSpace(aiAlertsResponse.Message))
            {
                failureMessages.Insert(0, aiAlertsResponse.Message);
            }

            var failedResponse = ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], string.Join(" ", failureMessages).Trim());
            CacheFaultPool(failedResponse, TimeSpan.FromSeconds(10));
            return failedResponse;
        }

        var activeAlerts = ResolveActiveAlerts(allAlerts, endTime);
        var devicesByCode = devices.ToDictionary(device => device.DeviceCode, StringComparer.Ordinal);
        var faultPool = activeAlerts
            .GroupBy(alert => new { alert.PointId, alert.FaultType })
            .Select(group => MapGroup(group.ToList(), devicesByCode))
            .OrderBy(item => item.WorkOrderStatus == DispatchWorkOrderStatusModel.PendingDispatch ? 0 : 1)
            .ThenBy(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered ? 0 : 1)
            .ThenByDescending(item => item.LatestDetectedAt)
            .ToList();

        var messages = new List<string>();
        if (!aiAlertsResponse.IsSuccess && !string.IsNullOrWhiteSpace(aiAlertsResponse.Message))
        {
            messages.Add(aiAlertsResponse.Message);
        }

        if (faultPool.Count == 0)
        {
            messages.Add("No active CTYun faults matched the current reconciliation rules.");
        }

        var response = ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Success(faultPool, string.Join(" ", messages).Trim());
        CacheFaultPool(response, _snapshotCacheLifetime);
        MapPointSourceDiagnostics.Write(
            "FaultPool",
            $"fault pool refreshed: rawDeviceCount = {devicePoolResponse.Data.Count}, uniqueDeviceCount = {devices.Count}, deviceAlarmQueries = {devices.Count}, aiAlertQuerySuccess = {aiAlertsResponse.IsSuccess}, deviceAlertQuerySuccessCount = {deviceAlertResponses.Count(result => result.IsSuccess)}, faultPoolCount = {faultPool.Count}");
        return response;
    }

    private bool TryGetCachedFaultPool(out ServiceResponse<IReadOnlyList<FaultPoolItemModel>> response)
    {
        lock (_snapshotCacheSyncRoot)
        {
            if (_snapshotCache is not null && _snapshotCache.IsReusable(DateTime.Now, _minimumRefreshInterval))
            {
                response = _snapshotCache.Response;
                MapPointSourceDiagnostics.Write(
                    "FaultPool",
                    $"fault pool cache hit: cachedAt = {_snapshotCache.CachedAt:yyyy-MM-dd HH:mm:ss}, expiresAt = {_snapshotCache.ExpiresAt:yyyy-MM-dd HH:mm:ss}, itemCount = {response.Data.Count}, isSuccess = {response.IsSuccess}");
                return true;
            }

            _snapshotCache = null;
        }

        response = ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], string.Empty);
        return false;
    }

    private void CacheFaultPool(ServiceResponse<IReadOnlyList<FaultPoolItemModel>> response, TimeSpan cacheLifetime)
    {
        lock (_snapshotCacheSyncRoot)
        {
            _snapshotCache = new CachedFaultPoolResponse(
                response,
                DateTime.Now,
                DateTime.Now.Add(cacheLifetime));
        }
    }

    private IReadOnlyList<FaultAlertDto> ResolveActiveAlerts(
        IReadOnlyList<FaultAlertDto> alerts,
        DateTime now)
    {
        if (alerts.Count == 0)
        {
            return [];
        }

        var alertsByPoint = alerts
            .GroupBy(alert => alert.PointId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FaultAlertDto>)group.OrderByDescending(item => item.LatestDetectedAt).ToList(),
                StringComparer.Ordinal);

        return alerts
            .Where(IsActionableFaultAlert)
            .GroupBy(alert => new { alert.PointId, alert.FaultType })
            .Where(group => IsStillActive(
                group.OrderByDescending(item => item.LatestDetectedAt).First(),
                alertsByPoint,
                now))
            .SelectMany(group => group)
            .ToList();
    }

    private bool IsStillActive(
        FaultAlertDto alert,
        IReadOnlyDictionary<string, IReadOnlyList<FaultAlertDto>> alertsByPoint,
        DateTime now)
    {
        if (IsOfflineFault(alert))
        {
            return !alertsByPoint.GetValueOrDefault(alert.PointId, Array.Empty<FaultAlertDto>())
                .Any(candidate => IsRecoverySignal(candidate) && candidate.LatestDetectedAt > alert.LatestDetectedAt);
        }

        return alert.LatestDetectedAt >= now.Subtract(_aiFaultIdleWindow);
    }

    private static bool IsActionableFaultAlert(FaultAlertDto alert)
    {
        return IsOfflineFault(alert) || IsAiFault(alert);
    }

    private static bool IsOfflineFault(FaultAlertDto alert)
    {
        return alert.FaultType.Contains("离线", StringComparison.Ordinal);
    }

    private static bool IsRecoverySignal(FaultAlertDto alert)
    {
        return alert.FaultType.Contains("上线", StringComparison.Ordinal);
    }

    private static bool IsAiFault(FaultAlertDto alert)
    {
        return !string.Equals(alert.AlertSource, "设备告警", StringComparison.Ordinal)
            && !IsRecoverySignal(alert);
    }

    private FaultPoolItemModel MapGroup(
        IReadOnlyList<FaultAlertDto> alerts,
        IReadOnlyDictionary<string, DevicePoolItemModel> devicesByCode)
    {
        var latest = alerts.OrderByDescending(item => item.LatestDetectedAt).First();
        devicesByCode.TryGetValue(latest.PointId, out var device);

        var currentHandlingUnit = device?.UnitName ?? "Pending Assignment";
        var responsibilityResponse = _responsibilityService.Resolve(new DispatchResponsibilityQueryDto(
            latest.PointId,
            latest.PointName,
            currentHandlingUnit));
        var responsibility = responsibilityResponse.IsSuccess
            ? responsibilityResponse.Data
            : CreatePlaceholderResponsibility(currentHandlingUnit);
        var dispatchMethod = IsOfflineFault(latest)
            ? DispatchMethodModel.Automatic
            : DispatchMethodModel.Manual;

        return new FaultPoolItemModel(
            $"{latest.PointId}:{latest.FaultType}",
            latest.PointId,
            latest.PointName,
            latest.FaultType,
            "CTYun Live Fault Pool",
            responsibility.CurrentHandlingUnit,
            device?.AreaName ?? currentHandlingUnit,
            latest.Content,
            "Fault",
            $"{latest.FaultType} snapshot",
            latest.AlertSource,
            true,
            latest.LatestDetectedAt.Date == DateTime.Today,
            dispatchMethod,
            DispatchWorkOrderStatusModel.PendingDispatch,
            DispatchRecoveryStatusModel.Unrecovered,
            responsibility,
            new DispatchNotificationRecordModel("--", "待发送", "--", "--", "--", "待发送", []),
            alerts.Min(item => item.FirstDetectedAt),
            alerts.Max(item => item.LatestDetectedAt),
            alerts.Count,
            alerts.Select(item => item.AlertSource).Distinct(StringComparer.Ordinal).ToList());
    }

    private static DispatchResponsibilityModel CreatePlaceholderResponsibility(string currentHandlingUnit)
    {
        return new DispatchResponsibilityModel(
            currentHandlingUnit,
            "Unassigned Maintainer",
            "--",
            "Unassigned Supervisor",
            "--",
            "default",
            "FaultPool.Placeholder");
    }

    private sealed record CachedFaultPoolResponse(
        ServiceResponse<IReadOnlyList<FaultPoolItemModel>> Response,
        DateTime CachedAt,
        DateTime ExpiresAt)
    {
        public bool IsReusable(DateTime now, TimeSpan minimumRefreshInterval)
        {
            return ExpiresAt > now || CachedAt.Add(minimumRefreshInterval) > now;
        }
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
            response = ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Failure([], $"Real fault pool call failed: {ex.Message}");
        }

        if (response.IsSuccess)
        {
            return response;
        }

        var fallback = _fallback.GetFaultPool();
        return fallback.IsSuccess
            ? ServiceResponse<IReadOnlyList<FaultPoolItemModel>>.Success(
                fallback.Data,
                string.IsNullOrWhiteSpace(response.Message)
                    ? "Fell back to the demo fault pool."
                    : $"{response.Message} Fell back to the demo fault pool.")
            : fallback;
    }
}
