using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class PointSelectionContext
{
    public PointBusinessSummaryState? CurrentSummary { get; private set; }

    public void Update(PointBusinessSummaryState summary, string consumer)
    {
        CurrentSummary = summary;

        MapPointSourceDiagnostics.WriteLines("PointSelectionContext", [
            $"selectedPointSummary final source = {summary.SourceType}",
            $"selectedPointSummary consumer = {consumer}, pointId = {summary.PointId}, deviceCode = {summary.DeviceCode}, deviceName = {summary.DeviceName}, online = {summary.OnlineStatus}, fault = {summary.FaultType}, lastSync = {summary.LastSyncTime}"
        ]);
    }
}
