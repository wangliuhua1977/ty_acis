using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class DispatchWorkOrderDetailState : ViewModelBase
{
    private DispatchWorkOrderStatus _workOrderStatus;
    private string _workOrderStatusText;
    private DispatchRecoveryStatus _recoveryStatus;
    private string _recoveryStatusText;
    private bool _isSelected;

    public DispatchWorkOrderDetailState(
        string workOrderId,
        string pointId,
        string pointName,
        string faultType,
        string inspectionGroupName,
        string mapLocationPlaceholder,
        string screenshotTitle,
        string screenshotSubtitle,
        string faultSummary,
        string latestInspectionConclusion,
        bool entersDispatchPool,
        string dispatchPoolEntryText,
        bool isTodayNew,
        DispatchMethod dispatchMethod,
        string dispatchMethodText,
        DispatchWorkOrderStatus workOrderStatus,
        string workOrderStatusText,
        DispatchRecoveryStatus recoveryStatus,
        string recoveryStatusText,
        DispatchResponsibilityState responsibility,
        DispatchNotificationRecordState notificationRecord,
        DispatchRepeatFaultState repeatFault)
    {
        WorkOrderId = workOrderId;
        PointId = pointId;
        PointName = pointName;
        FaultType = faultType;
        InspectionGroupName = inspectionGroupName;
        MapLocationPlaceholder = mapLocationPlaceholder;
        ScreenshotTitle = screenshotTitle;
        ScreenshotSubtitle = screenshotSubtitle;
        FaultSummary = faultSummary;
        LatestInspectionConclusion = latestInspectionConclusion;
        EntersDispatchPool = entersDispatchPool;
        DispatchPoolEntryText = dispatchPoolEntryText;
        IsTodayNew = isTodayNew;
        DispatchMethod = dispatchMethod;
        DispatchMethodText = dispatchMethodText;
        _workOrderStatus = workOrderStatus;
        _workOrderStatusText = workOrderStatusText;
        _recoveryStatus = recoveryStatus;
        _recoveryStatusText = recoveryStatusText;
        Responsibility = responsibility;
        NotificationRecord = notificationRecord;
        RepeatFault = repeatFault;
    }

    public string WorkOrderId { get; }

    public string PointId { get; }

    public string PointName { get; }

    public string FaultType { get; }

    public string InspectionGroupName { get; }

    public string MapLocationPlaceholder { get; }

    public string ScreenshotTitle { get; }

    public string ScreenshotSubtitle { get; }

    public string FaultSummary { get; }

    public string LatestInspectionConclusion { get; }

    public bool EntersDispatchPool { get; }

    public string DispatchPoolEntryText { get; }

    public bool IsTodayNew { get; }

    public DispatchMethod DispatchMethod { get; }

    public string DispatchMethodText { get; }

    public DispatchWorkOrderStatus WorkOrderStatus
    {
        get => _workOrderStatus;
        set => SetProperty(ref _workOrderStatus, value);
    }

    public string WorkOrderStatusText
    {
        get => _workOrderStatusText;
        set => SetProperty(ref _workOrderStatusText, value);
    }

    public DispatchRecoveryStatus RecoveryStatus
    {
        get => _recoveryStatus;
        set => SetProperty(ref _recoveryStatus, value);
    }

    public string RecoveryStatusText
    {
        get => _recoveryStatusText;
        set => SetProperty(ref _recoveryStatusText, value);
    }

    public DispatchResponsibilityState Responsibility { get; }

    public DispatchNotificationRecordState NotificationRecord { get; }

    public DispatchRepeatFaultState RepeatFault { get; }

    public bool IsRepeated => RepeatFault.IsRepeated;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
