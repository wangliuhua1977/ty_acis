using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.UI.States;

public static class MapPointStateFactory
{
    public static MapPointState Create(
        string pointId,
        string deviceCode,
        string pointName,
        string unitName,
        string currentHandlingUnit,
        double? mapLongitude,
        double? mapLatitude,
        double? registeredLongitude,
        double? registeredLatitude,
        CoordinateSystemKind registeredCoordinateSystem,
        CoordinateSystemKind mapCoordinateSystem,
        bool canRenderOnMap,
        string coordinateStatusText,
        string? rawLongitude,
        string? rawLatitude,
        PointCoordinateStatus coordinateStatus,
        string mapSource,
        string businessSummaryCoordinateStatus,
        double x,
        double y,
        MapPointVisualKind visualKind,
        string statusText,
        string faultType,
        string summary,
        string latestFaultTime,
        bool isPreviewAvailable,
        bool isCurrent = false)
    {
        return new MapPointState(
            pointId,
            deviceCode,
            pointName,
            unitName,
            currentHandlingUnit,
            mapLongitude,
            mapLatitude,
            registeredLongitude,
            registeredLatitude,
            registeredCoordinateSystem,
            mapCoordinateSystem,
            canRenderOnMap,
            coordinateStatusText,
            rawLongitude,
            rawLatitude,
            coordinateStatus,
            mapSource,
            businessSummaryCoordinateStatus,
            x,
            y,
            visualKind,
            statusText,
            faultType,
            summary,
            latestFaultTime,
            isPreviewAvailable)
        {
            IsCurrent = isCurrent
        };
    }
}
