using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Home;

public enum HomeMapPointKindModel
{
    Normal,
    Fault,
    Inspecting,
    Key
}

public sealed record HomeMapPointModel(
    string Id,
    string DeviceCode,
    string Name,
    string UnitName,
    double Longitude,
    double Latitude,
    bool CanRenderOnMap,
    string CoordinateStatusText,
    HomeMapPointKindModel Kind,
    double X,
    double Y,
    string StatusText,
    string FaultType,
    string Summary,
    string LatestFaultTime,
    bool IsInRecentFaultList)
{
    public HomeMapPointModel(
        string id,
        string name,
        string unitName,
        HomeMapPointKindModel kind,
        double x,
        double y,
        string statusText,
        string faultType,
        string summary,
        string latestFaultTime,
        bool isInRecentFaultList)
        : this(
            id,
            id,
            name,
            unitName,
            0d,
            0d,
            false,
            "待地图坐标",
            kind,
            x,
            y,
            statusText,
            faultType,
            summary,
            latestFaultTime,
            isInRecentFaultList)
    {
    }
}

public sealed record HomeRecentFaultModel(
    string PointId,
    string PointName,
    string FaultType,
    string LatestFaultTime);

public sealed record HomeDashboardSnapshot(
    string CurrentGroupSummary,
    string ExecutionProgress,
    string PendingReviewSummary,
    string PendingDispatchSummary,
    IReadOnlyList<HomeMapPointModel> MapPoints,
    IReadOnlyList<HomeRecentFaultModel> RecentFaults);

public interface IHomeDashboardService
{
    ServiceResponse<HomeDashboardSnapshot> GetDashboard();
}
