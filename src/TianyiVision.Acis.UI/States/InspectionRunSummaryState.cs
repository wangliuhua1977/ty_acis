using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionRunSummaryState : ViewModelBase
{
    private string _groupName = string.Empty;
    private string _startedAt = string.Empty;
    private string _totalPoints = string.Empty;
    private string _inspectedPoints = string.Empty;
    private string _normalCount = string.Empty;
    private string _faultCount = string.Empty;
    private string _currentPointName = string.Empty;

    public string GroupName
    {
        get => _groupName;
        set => SetProperty(ref _groupName, value);
    }

    public string StartedAt
    {
        get => _startedAt;
        set => SetProperty(ref _startedAt, value);
    }

    public string TotalPoints
    {
        get => _totalPoints;
        set => SetProperty(ref _totalPoints, value);
    }

    public string InspectedPoints
    {
        get => _inspectedPoints;
        set => SetProperty(ref _inspectedPoints, value);
    }

    public string NormalCount
    {
        get => _normalCount;
        set => SetProperty(ref _normalCount, value);
    }

    public string FaultCount
    {
        get => _faultCount;
        set => SetProperty(ref _faultCount, value);
    }

    public string CurrentPointName
    {
        get => _currentPointName;
        set => SetProperty(ref _currentPointName, value);
    }
}
