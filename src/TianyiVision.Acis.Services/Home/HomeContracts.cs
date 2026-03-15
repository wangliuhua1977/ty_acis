using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Devices;

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
    bool IsInRecentFaultList,
    PointBusinessSummaryModel? BusinessSummary = null)
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
        bool isInRecentFaultList,
        PointBusinessSummaryModel? businessSummary = null)
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
            isInRecentFaultList,
            businessSummary)
    {
    }
}

public sealed record HomeHeaderMetricsModel(
    string InspectionTasks,
    string Faults,
    string Outstanding,
    string PendingReview,
    string PendingDispatch);

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
    IReadOnlyList<HomeRecentFaultModel> RecentFaults,
    HomeHeaderMetricsModel HeaderMetrics);

public interface IHomeDashboardService
{
    ServiceResponse<HomeDashboardSnapshot> GetDashboard();
}
