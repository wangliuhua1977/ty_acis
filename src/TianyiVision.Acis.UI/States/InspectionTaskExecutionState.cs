using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionTaskExecutionState : ViewModelBase
{
    private string _executedToday = string.Empty;
    private string _currentTaskStatus = string.Empty;
    private string _nextRunTime = string.Empty;
    private string _currentProgressText = string.Empty;
    private double _currentProgressValue;
    private string _simulationNote = string.Empty;
    private bool _isEnabled;

    public string ExecutedToday
    {
        get => _executedToday;
        set => SetProperty(ref _executedToday, value);
    }

    public string CurrentTaskStatus
    {
        get => _currentTaskStatus;
        set => SetProperty(ref _currentTaskStatus, value);
    }

    public string NextRunTime
    {
        get => _nextRunTime;
        set => SetProperty(ref _nextRunTime, value);
    }

    public string CurrentProgressText
    {
        get => _currentProgressText;
        set => SetProperty(ref _currentProgressText, value);
    }

    public double CurrentProgressValue
    {
        get => _currentProgressValue;
        set => SetProperty(ref _currentProgressValue, value);
    }

    public string SimulationNote
    {
        get => _simulationNote;
        set => SetProperty(ref _simulationNote, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
