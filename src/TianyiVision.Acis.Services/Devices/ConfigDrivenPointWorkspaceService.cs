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

            points.Add(BuildPoint(device, latestFaultByPoint.GetValueOrDefault(device.PointId), detail));
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
        DevicePointDetailModel detail)
    {
        var hasFault = fault is not null || !detail.IsOnline;
        var currentFaultType = fault?.FaultType
            ?? (!detail.IsOnline ? "设备离线" : "无故障");
        var currentFaultSummary = fault?.FaultSummary
            ?? (!detail.IsOnline
                ? "设备当前离线，待补充故障摘要。"
                : detail.DetailSummary);

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
            fault?.LatestDetectedAt,
            currentFaultType,
            currentFaultSummary,
            hasFault,
            fault?.EntersDispatchPool ?? false,
            detail.DetailSummary,
            detail.SourceTag);
    }

    private static DevicePointDetailModel CreateFallbackDetail(DevicePoolItemModel device)
    {
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
            device.OnlineStatusText,
            "待接视频巡检",
            "待接 AI 判定",
            "当前点位详情暂未完整同步，先展示目录摘要信息。",
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
            new PointCoordinateModel(0d, 0d, PointCoordinateStatus.Missing, false, "未配置经纬度"),
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
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

    private static string NormalizeReason(string? message, string fallbackReason)
    {
        return string.IsNullOrWhiteSpace(message) ? fallbackReason : message.Trim();
    }
}
