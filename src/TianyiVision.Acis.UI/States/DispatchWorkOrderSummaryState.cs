using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class DispatchWorkOrderSummaryState : ViewModelBase
{
    private bool _isSelected;

    public DispatchWorkOrderSummaryState(
        string workOrderId,
        string pointName,
        string faultType,
        string currentHandlingUnit,
        string maintainerName,
        string supervisorName,
        string latestFaultTime,
        DispatchWorkOrderStatus workOrderStatus,
        string workOrderStatusText,
        DispatchRecoveryStatus recoveryStatus,
        string recoveryStatusText,
        DispatchMethod dispatchMethod,
        string dispatchMethodText,
        int repeatCount,
        string repeatSummary,
        bool isTodayNew)
    {
        WorkOrderId = workOrderId;
        PointName = pointName;
        FaultType = faultType;
        CurrentHandlingUnit = currentHandlingUnit;
        MaintainerName = maintainerName;
        SupervisorName = supervisorName;
        LatestFaultTime = latestFaultTime;
        WorkOrderStatus = workOrderStatus;
        WorkOrderStatusText = workOrderStatusText;
        RecoveryStatus = recoveryStatus;
        RecoveryStatusText = recoveryStatusText;
        DispatchMethod = dispatchMethod;
        DispatchMethodText = dispatchMethodText;
        RepeatCount = repeatCount;
        RepeatSummary = repeatSummary;
        IsTodayNew = isTodayNew;
    }

    public string WorkOrderId { get; }

    public string PointName { get; }

    public string FaultType { get; }

    public string CurrentHandlingUnit { get; }

    public string MaintainerName { get; }

    public string SupervisorName { get; }

    public string LatestFaultTime { get; }

    public DispatchWorkOrderStatus WorkOrderStatus { get; }

    public string WorkOrderStatusText { get; }

    public DispatchRecoveryStatus RecoveryStatus { get; }

    public string RecoveryStatusText { get; }

    public DispatchMethod DispatchMethod { get; }

    public string DispatchMethodText { get; }

    public int RepeatCount { get; }

    public string RepeatSummary { get; }

    public bool IsTodayNew { get; }

    public bool IsRepeated => RepeatCount > 1;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
