using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Devices;

public enum PointFaultObservationStatus
{
    NoFault,
    HasFault,
    Pending
}

public sealed record PointWorkspaceItemModel(
    string PointId,
    string DeviceCode,
    string PointName,
    string DeviceType,
    string UnitName,
    string AreaName,
    string CurrentHandlingUnit,
    PointCoordinateModel Coordinate,
    bool? IsOnline,
    string OnlineStatusText,
    string PlaybackStatusText,
    string ImageStatusText,
    DateTime? LastSyncTime,
    string LastSyncSource,
    DateTime? LatestFaultTime,
    string CurrentFaultType,
    string CurrentFaultSummary,
    PointFaultObservationStatus FaultStatus,
    bool HasFault,
    bool EntersDispatchPool,
    string DetailSummary,
    string SourceTag);

public interface IPointWorkspaceService
{
    ServiceResponse<IReadOnlyList<PointWorkspaceItemModel>> GetPointCollection();

    ServiceResponse<PointWorkspaceItemModel> GetPoint(string pointId);
}
