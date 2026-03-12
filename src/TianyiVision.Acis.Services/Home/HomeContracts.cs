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
    string Name,
    string UnitName,
    HomeMapPointKindModel Kind,
    double X,
    double Y,
    string StatusText,
    string FaultType,
    string Summary,
    string LatestFaultTime,
    bool IsInRecentFaultList);

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
