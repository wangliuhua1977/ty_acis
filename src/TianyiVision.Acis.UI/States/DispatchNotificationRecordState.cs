using System.Collections.ObjectModel;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class DispatchNotificationRecordState : ViewModelBase
{
    private string _faultNotificationSentAt;
    private string _faultNotificationStatus;
    private string _recoveryNotificationSentAt;
    private string _recoveryNotificationStatus;

    public DispatchNotificationRecordState(
        string faultNotificationSentAt,
        string faultNotificationStatus,
        string recoveryNotificationSentAt,
        string recoveryNotificationStatus,
        ObservableCollection<DispatchNotificationEntryState> timelineEntries)
    {
        _faultNotificationSentAt = faultNotificationSentAt;
        _faultNotificationStatus = faultNotificationStatus;
        _recoveryNotificationSentAt = recoveryNotificationSentAt;
        _recoveryNotificationStatus = recoveryNotificationStatus;
        TimelineEntries = timelineEntries;
    }

    public string FaultNotificationSentAt
    {
        get => _faultNotificationSentAt;
        set => SetProperty(ref _faultNotificationSentAt, value);
    }

    public string FaultNotificationStatus
    {
        get => _faultNotificationStatus;
        set => SetProperty(ref _faultNotificationStatus, value);
    }

    public string RecoveryNotificationSentAt
    {
        get => _recoveryNotificationSentAt;
        set => SetProperty(ref _recoveryNotificationSentAt, value);
    }

    public string RecoveryNotificationStatus
    {
        get => _recoveryNotificationStatus;
        set => SetProperty(ref _recoveryNotificationStatus, value);
    }

    public ObservableCollection<DispatchNotificationEntryState> TimelineEntries { get; }
}
