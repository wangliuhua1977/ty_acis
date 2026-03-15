namespace TianyiVision.Acis.UI.States;

public static class MapPointStateFactory
{
    public static MapPointState Create(
        string pointId,
        string deviceCode,
        string pointName,
        string unitName,
        string currentHandlingUnit,
        double longitude,
        double latitude,
        bool canRenderOnMap,
        string coordinateStatusText,
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
            longitude,
            latitude,
            canRenderOnMap,
            coordinateStatusText,
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
