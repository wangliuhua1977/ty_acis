using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionTaskSummaryState : ViewModelBase
{
    private string _taskId = string.Empty;
    private string _taskName = string.Empty;
    private string _taskTypeText = string.Empty;
    private string _triggerText = string.Empty;
    private string _statusText = string.Empty;
    private string _scopePlanText = string.Empty;
    private string _currentPointText = string.Empty;
    private string _timeText = string.Empty;
    private string _summary = string.Empty;
    private bool _isSelected;

    public string TaskId
    {
        get => _taskId;
        set => SetProperty(ref _taskId, value);
    }

    public string TaskName
    {
        get => _taskName;
        set => SetProperty(ref _taskName, value);
    }

    public string TaskTypeText
    {
        get => _taskTypeText;
        set => SetProperty(ref _taskTypeText, value);
    }

    public string TriggerText
    {
        get => _triggerText;
        set => SetProperty(ref _triggerText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string ScopePlanText
    {
        get => _scopePlanText;
        set => SetProperty(ref _scopePlanText, value);
    }

    public string CurrentPointText
    {
        get => _currentPointText;
        set => SetProperty(ref _currentPointText, value);
    }

    public string TimeText
    {
        get => _timeText;
        set => SetProperty(ref _timeText, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
