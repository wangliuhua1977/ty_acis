using TianyiVision.Acis.Services.Diagnostics;

namespace TianyiVision.Acis.Services.Devices;

public sealed record PointBusinessSummaryModel(
    string PointId,
    string DeviceCode,
    string DeviceName,
    double? Longitude,
    double? Latitude,
    string SourceType,
    string CoordinateStatus,
    string OnlineStatus,
    DateTime? LastSyncTime,
    string RegionName,
    string StatusSummary,
    string FaultType,
    IReadOnlyList<string> AvailableActions);

public static class PointBusinessSummaryFactory
{
    public static PointBusinessSummaryModel Create(PointWorkspaceItemModel point)
    {
        var sourceType = MapPointSourceDiagnostics.ClassifySourceTag(point.SourceTag);
        var onlineStatus = ResolveOnlineStatus(point.IsOnline);
        var coordinateStatus = ResolveCoordinateStatus(point.Coordinate);
        var faultType = ResolveFaultType(point.FaultStatus);

        return new PointBusinessSummaryModel(
            point.PointId,
            Normalize(point.DeviceCode, point.PointId),
            Normalize(point.PointName, point.PointId),
            ResolveCoordinateValue(point.Coordinate, point.Coordinate.Longitude),
            ResolveCoordinateValue(point.Coordinate, point.Coordinate.Latitude),
            sourceType,
            coordinateStatus,
            onlineStatus,
            point.LastSyncTime,
            FirstNonEmpty(point.AreaName, point.UnitName, "暂无"),
            BuildStatusSummary(point, onlineStatus, coordinateStatus, faultType),
            faultType,
            BuildAvailableActions(point));
    }

    private static double? ResolveCoordinateValue(PointCoordinateModel coordinate, double value)
    {
        return coordinate.Status == PointCoordinateStatus.Valid ? value : null;
    }

    private static string ResolveOnlineStatus(bool? isOnline)
    {
        return isOnline switch
        {
            true => "在线",
            false => "离线",
            _ => "未知"
        };
    }

    private static string ResolveCoordinateStatus(PointCoordinateModel coordinate)
    {
        return coordinate.Status switch
        {
            PointCoordinateStatus.Valid => "坐标可落点",
            PointCoordinateStatus.Missing or PointCoordinateStatus.Incomplete => "待校验",
            _ => "坐标异常"
        };
    }

    private static string ResolveFaultType(PointFaultObservationStatus faultStatus)
    {
        return faultStatus switch
        {
            PointFaultObservationStatus.HasFault => "存在异常",
            PointFaultObservationStatus.NoFault => "无故障",
            _ => "待接入"
        };
    }

    private static string BuildStatusSummary(
        PointWorkspaceItemModel point,
        string onlineStatus,
        string coordinateStatus,
        string faultType)
    {
        var summary = $"{onlineStatus} / {coordinateStatus} / {faultType}";
        var faultDetail = NormalizeFaultDetail(point);

        return string.IsNullOrWhiteSpace(faultDetail)
            ? summary
            : $"{summary}（{faultDetail}）";
    }

    private static string? NormalizeFaultDetail(PointWorkspaceItemModel point)
    {
        if (point.FaultStatus != PointFaultObservationStatus.HasFault)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(point.CurrentFaultType)
            && !string.Equals(point.CurrentFaultType, "无故障", StringComparison.Ordinal)
            && !string.Equals(point.CurrentFaultType, "待接入", StringComparison.Ordinal))
        {
            return point.CurrentFaultType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(point.CurrentFaultSummary)
            && !string.Equals(point.CurrentFaultSummary, "无故障", StringComparison.Ordinal)
            && !string.Equals(point.CurrentFaultSummary, "待接入", StringComparison.Ordinal))
        {
            return point.CurrentFaultSummary.Trim();
        }

        return null;
    }

    private static IReadOnlyList<string> BuildAvailableActions(PointWorkspaceItemModel point)
    {
        var actions = new List<string> { "进入AI巡检" };

        if (point.FaultStatus == PointFaultObservationStatus.HasFault && point.EntersDispatchPool)
        {
            actions.Add("查看故障处置");
        }

        if (!point.Coordinate.CanRenderOnMap)
        {
            actions.Add("坐标补录预留");
        }

        actions.Add("查看报表");
        return actions;
    }

    private static string FirstNonEmpty(string? primary, string? secondary, string fallback)
    {
        return !string.IsNullOrWhiteSpace(primary)
            ? primary.Trim()
            : !string.IsNullOrWhiteSpace(secondary)
                ? secondary.Trim()
                : fallback;
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
