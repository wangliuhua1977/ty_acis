using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.UI.States;

public sealed record PointBusinessSummaryState(
    string PointId,
    string DeviceCode,
    string DeviceName,
    double? Longitude,
    double? Latitude,
    string SourceType,
    string CoordinateStatus,
    string OnlineStatus,
    string LastSyncTime,
    string RegionName,
    string StatusSummary,
    string FaultType,
    string AvailableActionsText,
    bool IsCoordinatePending)
{
    public string DisplayNameWithCode
        => string.IsNullOrWhiteSpace(DeviceCode) ? DeviceName : $"{DeviceName} ({DeviceCode})";

    public string DisplayCodeAndRegion
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DeviceCode))
            {
                return RegionName;
            }

            return string.IsNullOrWhiteSpace(RegionName)
                ? $"设备编码 {DeviceCode}"
                : $"设备编码 {DeviceCode} · {RegionName}";
        }
    }

    public static PointBusinessSummaryState FromModel(PointBusinessSummaryModel model)
    {
        return new PointBusinessSummaryState(
            model.PointId,
            model.DeviceCode,
            model.DeviceName,
            model.Longitude,
            model.Latitude,
            Normalize(model.SourceType, "unknown"),
            Normalize(model.CoordinateStatus, "未获取"),
            Normalize(model.OnlineStatus, "未知"),
            model.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "待接入",
            Normalize(model.RegionName, "暂无"),
            Normalize(model.StatusSummary, "暂无"),
            Normalize(model.FaultType, "待接入"),
            model.AvailableActions.Count == 0 ? "暂无" : string.Join(" / ", model.AvailableActions),
            model.Longitude is null || model.Latitude is null);
    }

    public static PointBusinessSummaryState CreateFallback(
        string pointId,
        string deviceCode,
        string deviceName,
        double? longitude,
        double? latitude,
        string sourceType,
        string coordinateStatus,
        string onlineStatus,
        string regionName,
        string statusSummary,
        string faultType,
        string availableActionsText,
        bool isCoordinatePending)
    {
        return new PointBusinessSummaryState(
            pointId,
            deviceCode,
            deviceName,
            longitude,
            latitude,
            Normalize(sourceType, "fallback"),
            Normalize(coordinateStatus, "未获取"),
            Normalize(onlineStatus, "未知"),
            "待接入",
            Normalize(regionName, "暂无"),
            Normalize(statusSummary, "暂无"),
            Normalize(faultType, "待接入"),
            Normalize(availableActionsText, "暂无"),
            isCoordinatePending);
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
