using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Dispatch;

namespace TianyiVision.Acis.Services.Devices;

public sealed class ConfigDrivenPointWorkspaceService : IPointWorkspaceService
{
    private readonly IDeviceWorkspaceService _deviceWorkspaceService;
    private readonly IFaultPoolService _faultPoolService;

    public ConfigDrivenPointWorkspaceService(
        IDeviceWorkspaceService deviceWorkspaceService,
        IFaultPoolService faultPoolService)
    {
        _deviceWorkspaceService = deviceWorkspaceService;
        _faultPoolService = faultPoolService;
    }

    public ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>> GetPointCollection()
    {
        var devicePoolResponse = _deviceWorkspaceService.GetDevicePool();
        if (!devicePoolResponse.IsSuccess || devicePoolResponse.Data.Count == 0)
        {
            MapPointSourceDiagnostics.Write(
                "PointWorkspace",
                $"Point workspace generation aborted before detail stage: reason = {NormalizeReason(devicePoolResponse.Message, "设备池返回 0 条")}");
            return ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>>.Failure([], devicePoolResponse.Message);
        }

        var faultPoolResponse = _faultPoolService.GetFaultPool();
        if (!faultPoolResponse.IsSuccess)
        {
            MapPointSourceDiagnostics.Write(
                "PointWorkspace",
                $"Fault pool snapshot unavailable during point workspace generation: reason = {NormalizeReason(faultPoolResponse.Message, "故障池调用失败")}");
        }

        var latestFaultByPoint = faultPoolResponse.Data
            .GroupBy(item => item.PointId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.LatestDetectedAt).First(),
                StringComparer.Ordinal);

        var points = new List<PointWorkspaceItemModel>(devicePoolResponse.Data.Count);
        var detailFailureReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var detailSourceBreakdown = new Dictionary<string, int>(StringComparer.Ordinal);
        var coordinateFailureReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var realDetailSuccessCount = 0;

        foreach (var device in devicePoolResponse.Data)
        {
            var detailResponse = _deviceWorkspaceService.GetPointDetail(device.PointId);
            var detail = detailResponse.IsSuccess ? detailResponse.Data : CreateFallbackDetail(device);
            Increment(detailSourceBreakdown, MapPointSourceDiagnostics.ClassifySourceTag(detail.SourceTag));

            if (IsRealDetailSuccess(detailResponse, detail))
            {
                realDetailSuccessCount++;
            }
            else
            {
                Increment(detailFailureReasons, ClassifyDetailFailureReason(device, detailResponse, detail));
            }

            if (!detail.Coordinate.CanRenderOnMap)
            {
                Increment(coordinateFailureReasons, ClassifyCoordinateFailureReason(detail.Coordinate));
            }

            var point = BuildPoint(
                device,
                latestFaultByPoint.GetValueOrDefault(device.PointId),
                detail,
                faultPoolResponse.IsSuccess);
            points.Add(point);

            var businessSummary = PointBusinessSummaryFactory.Create(point);
            WriteCoordinateReconciliationLog(point, businessSummary);
            MapPointSourceDiagnostics.Write(
                "PointStatus",
                $"status field mapped: pointId = {businessSummary.PointId}, deviceCode = {businessSummary.DeviceCode}, online = {businessSummary.OnlineStatus}, coordinate = {businessSummary.CoordinateStatus}, fault = {businessSummary.FaultType}, lastSync = {(businessSummary.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "待接入")}, lastSyncSource = {NormalizeLastSyncSource(point.LastSyncSource)}, summary = {businessSummary.StatusSummary}");
        }

        points = points
            .OrderByDescending(point => point.HasFault)
            .ThenBy(point => point.Coordinate.CanRenderOnMap ? 0 : 1)
            .ThenBy(point => point.PointName, StringComparer.Ordinal)
            .ToList();

        if (points.Count == 0)
        {
            MapPointSourceDiagnostics.Write("PointWorkspace", "Point workspace generated 0 rows after detail binding.");
            return ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>>.Failure([], "点位工作区未生成可用数据。");
        }

        MapPointSourceDiagnostics.WriteLines("PointDetail", [
            $"totalDevices = {devicePoolResponse.Data.Count}",
            $"realDetailSuccessCount = {realDetailSuccessCount}",
            $"realDetailFailureCount = {devicePoolResponse.Data.Count - realDetailSuccessCount}",
            $"detailFailureReasons = {MapPointSourceDiagnostics.SummarizeCounts(detailFailureReasons)}",
            $"detailSourceBreakdown = {MapPointSourceDiagnostics.SummarizeCounts(detailSourceBreakdown)}"
        ]);

        MapPointSourceDiagnostics.WriteLines("CoordinateCleaning", [
            $"totalDevices = {points.Count}",
            $"renderablePointCount = {points.Count(point => point.Coordinate.CanRenderOnMap)}",
            $"unrenderablePointCount = {points.Count(point => !point.Coordinate.CanRenderOnMap)}",
            $"unrenderableReasons = {MapPointSourceDiagnostics.SummarizeCounts(coordinateFailureReasons)}"
        ]);

        return ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>>.Success(
            points,
            CombineMessages(devicePoolResponse.Message, faultPoolResponse.IsSuccess ? string.Empty : faultPoolResponse.Message));
    }

    public ServiceResponse<PointWorkspaceItemModel> GetPoint(string pointId)
    {
        var collectionResponse = GetPointCollection();
        if (!collectionResponse.IsSuccess)
        {
            return ServiceResponse<PointWorkspaceItemModel>.Failure(Empty(pointId), collectionResponse.Message);
        }

        var point = collectionResponse.Data.FirstOrDefault(item => item.PointId == pointId);
        return point is null
            ? ServiceResponse<PointWorkspaceItemModel>.Failure(Empty(pointId), $"未找到点位 {pointId}。")
            : ServiceResponse<PointWorkspaceItemModel>.Success(point, collectionResponse.Message);
    }

    private PointWorkspaceItemModel BuildPoint(
        DevicePoolItemModel device,
        FaultPoolItemModel? fault,
        DevicePointDetailModel detail,
        bool hasReliableFaultPoolSnapshot)
    {
        var hasOfflineFault = detail.IsOnline == false;
        var faultStatus = hasOfflineFault || fault is not null
            ? PointFaultObservationStatus.HasFault
            : hasReliableFaultPoolSnapshot
                ? PointFaultObservationStatus.NoFault
                : PointFaultObservationStatus.Pending;
        var hasFault = faultStatus == PointFaultObservationStatus.HasFault;
        var currentFaultType = fault?.FaultType
            ?? (faultStatus == PointFaultObservationStatus.HasFault
                ? "设备离线"
                : faultStatus == PointFaultObservationStatus.NoFault
                    ? "无故障"
                    : "待接入");
        var currentFaultSummary = ResolveFaultSummary(fault?.FaultSummary, currentFaultType, faultStatus);

        return new PointWorkspaceItemModel(
            detail.PointId,
            detail.DeviceCode,
            detail.PointName,
            detail.DeviceType,
            detail.UnitName,
            detail.AreaName,
            fault?.CurrentHandlingUnit ?? detail.UnitName,
            detail.Coordinate,
            detail.IsOnline,
            detail.OnlineStatusText,
            detail.PlaybackStatusText,
            detail.ImageStatusText,
            detail.LastSyncTime,
            detail.LastSyncSource,
            fault?.LatestDetectedAt,
            currentFaultType,
            currentFaultSummary,
            faultStatus,
            hasFault,
            fault?.EntersDispatchPool ?? false,
            detail.DetailSummary,
            detail.SourceTag);
    }

    private static DevicePointDetailModel CreateFallbackDetail(DevicePoolItemModel device)
    {
        var onlineStatusText = !string.IsNullOrWhiteSpace(device.OnlineStatusText)
            ? device.OnlineStatusText
            : DeviceWorkspaceService.ResolveOnlineStatusText(device.IsOnline);

        return new DevicePointDetailModel(
            device.PointId,
            device.DeviceCode,
            device.DeviceName,
            device.DeviceType,
            device.UnitName,
            device.AreaName,
            device.AreaName,
            device.Coordinate,
            device.IsOnline,
            onlineStatusText,
            "待接入",
            "待接入",
            null,
            "待接入",
            device.IsOnline is null ? "未知 / 待校验 / 待接入" : $"{onlineStatusText} / 待校验 / 待接入",
            device.SourceTag);
    }

    private static PointWorkspaceItemModel Empty(string pointId)
    {
        return new PointWorkspaceItemModel(
            pointId,
            pointId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            PointCoordinateParser.Missing(),
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            PointFaultObservationStatus.Pending,
            false,
            false,
            string.Empty,
            string.Empty);
    }

    private static string CombineMessages(string? primary, string? secondary)
    {
        return string.Join(
            " ",
            new[] { primary, secondary }
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!.Trim()));
    }

    private static bool IsRealDetailSuccess(
        ServiceResponse<DevicePointDetailModel> detailResponse,
        DevicePointDetailModel detail)
    {
        return detailResponse.IsSuccess
            && string.Equals(MapPointSourceDiagnostics.ClassifySourceTag(detail.SourceTag), "real", StringComparison.Ordinal);
    }

    private static string ClassifyDetailFailureReason(
        DevicePoolItemModel device,
        ServiceResponse<DevicePointDetailModel> detailResponse,
        DevicePointDetailModel detail)
    {
        if (string.Equals(MapPointSourceDiagnostics.ClassifySourceTag(device.SourceTag), "demo", StringComparison.Ordinal))
        {
            return "上游设备目录已回退 demo";
        }

        if (string.IsNullOrWhiteSpace(detail.PointName) || string.IsNullOrWhiteSpace(detail.DeviceCode))
        {
            return "字段缺失";
        }

        var message = NormalizeReason(detailResponse.Message, "点位详情未返回有效数据");
        if (message.Contains("接口异常", StringComparison.Ordinal)
            || message.Contains("调用异常", StringComparison.Ordinal)
            || message.Contains("请求异常", StringComparison.Ordinal))
        {
            return "接口异常";
        }

        if (message.Contains("解析失败", StringComparison.Ordinal))
        {
            return "解析失败";
        }

        if (message.Contains("未找到", StringComparison.Ordinal)
            || message.Contains("未返回", StringComparison.Ordinal)
            || message.Contains("无效数据", StringComparison.Ordinal)
            || message.Contains("0 条", StringComparison.Ordinal))
        {
            return "无详情";
        }

        if (string.Equals(MapPointSourceDiagnostics.ClassifySourceTag(detail.SourceTag), "demo", StringComparison.Ordinal))
        {
            return "已回退 demo";
        }

        return "其他";
    }

    private static string ClassifyCoordinateFailureReason(PointCoordinateModel coordinate)
    {
        return coordinate.Status switch
        {
            PointCoordinateStatus.Missing => "空值",
            PointCoordinateStatus.Incomplete => "空值",
            PointCoordinateStatus.ZeroOrigin => "0/0",
            PointCoordinateStatus.ConversionFailed => "转换失败",
            PointCoordinateStatus.Invalid when coordinate.StatusText.Contains("格式", StringComparison.Ordinal) => "格式异常",
            PointCoordinateStatus.Invalid when coordinate.StatusText.Contains("范围", StringComparison.Ordinal) => "越界",
            PointCoordinateStatus.Invalid => "格式异常",
            _ => coordinate.StatusText
        };
    }

    private static void Increment(IDictionary<string, int> counts, string key)
    {
        if (counts.TryGetValue(key, out var value))
        {
            counts[key] = value + 1;
            return;
        }

        counts[key] = 1;
    }

    private static string ResolveFaultSummary(
        string? rawSummary,
        string currentFaultType,
        PointFaultObservationStatus faultStatus)
    {
        if (!string.IsNullOrWhiteSpace(rawSummary))
        {
            return rawSummary.Trim();
        }

        return faultStatus switch
        {
            PointFaultObservationStatus.HasFault when string.Equals(currentFaultType, "设备离线", StringComparison.Ordinal)
                => "设备当前离线",
            PointFaultObservationStatus.HasFault => $"当前点位存在异常：{currentFaultType}",
            PointFaultObservationStatus.NoFault => "无故障",
            _ => "待接入"
        };
    }

    private static string NormalizeLastSyncSource(string? lastSyncSource)
    {
        return string.IsNullOrWhiteSpace(lastSyncSource) ? "待接入" : lastSyncSource.Trim();
    }

    private static void WriteCoordinateReconciliationLog(
        PointWorkspaceItemModel point,
        PointBusinessSummaryModel businessSummary)
    {
        var coordinate = point.Coordinate;
        MapPointSourceDiagnostics.Write(
            "PointCoordinateAudit",
            $"pointId = {NormalizeLogValue(point.PointId)}, deviceCode = {NormalizeLogValue(point.DeviceCode)}, rawLongitude = {NormalizeLogValue(coordinate.RawLongitude)}, rawLatitude = {NormalizeLogValue(coordinate.RawLatitude)}, coordinateSystem = {coordinate.RegisteredCoordinateSystem}, parsedLongitude = {FormatNullableDouble(coordinate.RegisteredCoordinate?.Longitude)}, parsedLatitude = {FormatNullableDouble(coordinate.RegisteredCoordinate?.Latitude)}, coordinateStatusEnum = {coordinate.Status}, coordinateStatusText = {NormalizeLogValue(coordinate.StatusText)}, canRenderOnMap = {coordinate.CanRenderOnMap}, mapLongitude = {FormatNullableDouble(coordinate.MapCoordinate?.Longitude)}, mapLatitude = {FormatNullableDouble(coordinate.MapCoordinate?.Latitude)}, mapSource = {NormalizeLogValue(coordinate.MapSource)}, businessSummaryCoordinateStatus = {NormalizeLogValue(businessSummary.CoordinateStatus)}, finalRenderable = {coordinate.CanRenderOnMap}, sourceTag = {NormalizeLogValue(point.SourceTag)}, mapCoordinateSystem = {coordinate.MapCoordinateSystem}, diagnostics = {NormalizeLogValue(coordinate.DiagnosticsText)}");
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
            : "null";
    }

    private static string NormalizeLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
    }

    private static string NormalizeReason(string? message, string fallbackReason)
    {
        return string.IsNullOrWhiteSpace(message) ? fallbackReason : message.Trim();
    }
}
