using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Dispatch;
using TianyiVision.Acis.Services.Localization;

namespace TianyiVision.Acis.Services.Reports;

public sealed class ConfigDrivenReportDataService : IReportDataService
{
    private readonly IDispatchNotificationService _dispatchNotificationService;
    private readonly ITextService _textService;
    private readonly IReportDataService _fallback;

    public ConfigDrivenReportDataService(
        IDispatchNotificationService dispatchNotificationService,
        ITextService textService,
        IReportDataService fallback)
    {
        _dispatchNotificationService = dispatchNotificationService;
        _textService = textService;
        _fallback = fallback;
    }

    public ServiceResponse<ReportsWorkspaceSnapshot> GetWorkspace(ReportQueryDto query)
    {
        var fallbackResponse = _fallback.GetWorkspace(query);
        var workOrdersResponse = _dispatchNotificationService.GetWorkOrders();

        if (!workOrdersResponse.IsSuccess || workOrdersResponse.Data.Count == 0)
        {
            return fallbackResponse.IsSuccess
                ? ServiceResponse<ReportsWorkspaceSnapshot>.Success(
                    fallbackResponse.Data,
                    string.IsNullOrWhiteSpace(workOrdersResponse.Message)
                        ? fallbackResponse.Message
                        : $"{workOrdersResponse.Message} Fell back to demo report data.")
                : ServiceResponse<ReportsWorkspaceSnapshot>.Failure(
                    fallbackResponse.Data,
                    workOrdersResponse.Message);
        }

        var inspectionRows = fallbackResponse.IsSuccess
            ? fallbackResponse.Data.InspectionExecutionRows
            : [];
        var snapshot = new ReportsWorkspaceSnapshot(
            inspectionRows,
            BuildFaultRows(workOrdersResponse.Data),
            BuildDispatchRows(workOrdersResponse.Data),
            BuildResponsibilityRows(workOrdersResponse.Data),
            BuildOutstandingRows(workOrdersResponse.Data));

        return ServiceResponse<ReportsWorkspaceSnapshot>.Success(snapshot, workOrdersResponse.Message);
    }

    private IReadOnlyList<FaultStatisticsReportModel> BuildFaultRows(IReadOnlyList<DispatchWorkOrderModel> workOrders)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return workOrders
            .GroupBy(item => new
            {
                ReportDate = today,
                item.InspectionGroupName,
                CurrentHandlingUnit = item.Responsibility.CurrentHandlingUnit,
                item.FaultType
            })
            .Select(group => new FaultStatisticsReportModel(
                group.Key.ReportDate,
                group.Key.InspectionGroupName,
                group.Key.CurrentHandlingUnit,
                group.Key.FaultType,
                group.Count(),
                group.Count(item => IsOfflineFault(item.FaultType)),
                group.Count(item => IsPlaybackFault(item.FaultType)),
                group.Count(item => IsImageFault(item.FaultType)),
                group.Count(item => item.IsTodayNew),
                group.Count(item => item.RepeatFault.RepeatCount > 1)))
            .OrderByDescending(item => item.FaultTotal)
            .ThenBy(item => item.InspectionGroupName, StringComparer.Ordinal)
            .ToList();
    }

    private IReadOnlyList<DispatchDisposalReportModel> BuildDispatchRows(IReadOnlyList<DispatchWorkOrderModel> workOrders)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return workOrders
            .GroupBy(item => new
            {
                ReportDate = today,
                item.InspectionGroupName,
                CurrentHandlingUnit = item.Responsibility.CurrentHandlingUnit,
                item.FaultType
            })
            .Select(group => new DispatchDisposalReportModel(
                group.Key.ReportDate,
                group.Key.InspectionGroupName,
                group.Key.CurrentHandlingUnit,
                group.Key.FaultType,
                group.Count(item => item.WorkOrderStatus == DispatchWorkOrderStatusModel.PendingDispatch),
                group.Count(item => item.WorkOrderStatus == DispatchWorkOrderStatusModel.Dispatched),
                group.Count(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Recovered),
                group.Count(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered),
                group.Count(item => item.DispatchMethod == DispatchMethodModel.Automatic),
                group.Count(item => item.DispatchMethod == DispatchMethodModel.Manual)))
            .OrderByDescending(item => item.UnrecoveredCount)
            .ThenByDescending(item => item.DispatchedCount)
            .ToList();
    }

    private static IReadOnlyList<ResponsibilityOwnershipReportModel> BuildResponsibilityRows(IReadOnlyList<DispatchWorkOrderModel> workOrders)
    {
        return workOrders
            .GroupBy(item => new
            {
                item.InspectionGroupName,
                CurrentHandlingUnit = item.Responsibility.CurrentHandlingUnit,
                item.Responsibility.MaintainerName,
                item.Responsibility.SupervisorName,
                item.FaultType
            })
            .Select(group => new ResponsibilityOwnershipReportModel(
                group.Key.InspectionGroupName,
                group.Key.CurrentHandlingUnit,
                group.Key.MaintainerName,
                group.Key.SupervisorName,
                group.Key.FaultType,
                group.Count(),
                group.Count(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Recovered),
                group.Count(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered)))
            .OrderByDescending(item => item.UnrecoveredCount)
            .ThenByDescending(item => item.FaultCount)
            .ToList();
    }

    private IReadOnlyList<OutstandingFaultReportModel> BuildOutstandingRows(IReadOnlyList<DispatchWorkOrderModel> workOrders)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return workOrders
            .Where(item => item.RecoveryStatus == DispatchRecoveryStatusModel.Unrecovered)
            .OrderByDescending(item => ParseDateTime(item.RepeatFault.LatestFaultTime) ?? DateTime.MinValue)
            .Select(item => new OutstandingFaultReportModel(
                today,
                item.InspectionGroupName,
                item.PointName,
                item.Responsibility.CurrentHandlingUnit,
                item.Responsibility.MaintainerName,
                item.Responsibility.SupervisorName,
                item.FaultType,
                item.RepeatFault.FirstFaultTime,
                item.RepeatFault.LatestFaultTime,
                _textService.Resolve(TextTokens.DispatchRecoveryUnrecovered)))
            .ToList();
    }

    private static bool IsOfflineFault(string faultType)
    {
        return faultType.Contains("离线", StringComparison.Ordinal);
    }

    private static bool IsPlaybackFault(string faultType)
    {
        return faultType.Contains("播放", StringComparison.Ordinal);
    }

    private static bool IsImageFault(string faultType)
    {
        return !IsOfflineFault(faultType) && !IsPlaybackFault(faultType);
    }

    private static DateTime? ParseDateTime(string rawValue)
    {
        return DateTime.TryParse(rawValue, out var parsed) ? parsed : null;
    }
}
