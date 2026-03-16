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
    string? RawLongitude,
    string? RawLatitude,
    PointCoordinateStatus CoordinateStatus,
    string MapSource,
    HomeMapPointKindModel Kind,
    double X,
    double Y,
    string StatusText,
    string FaultType,
    string Summary,
    string LatestFaultTime,
    bool IsInRecentFaultList,
    PointBusinessSummaryModel? BusinessSummary = null,
    double? MapLongitude = null,
    double? MapLatitude = null,
    double? RegisteredLongitude = null,
    double? RegisteredLatitude = null,
    CoordinateSystemKind RegisteredCoordinateSystem = CoordinateSystemKind.Unknown,
    CoordinateSystemKind MapCoordinateSystem = CoordinateSystemKind.Unknown)
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
            null,
            null,
            PointCoordinateStatus.Missing,
            "unavailable",
            kind,
            x,
            y,
            statusText,
            faultType,
            summary,
            latestFaultTime,
            isInRecentFaultList,
            businessSummary,
            null,
            null,
            null,
            null,
            CoordinateSystemKind.Unknown,
            CoordinateSystemKind.Unknown)
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
