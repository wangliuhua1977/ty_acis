using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class DispatchRepeatFaultState : ViewModelBase
{
    private string _firstFaultTime;
    private string _latestFaultTime;
    private int _repeatCount;
    private string _repeatSummary;

    public DispatchRepeatFaultState(string firstFaultTime, string latestFaultTime, int repeatCount, string repeatSummary)
    {
        _firstFaultTime = firstFaultTime;
        _latestFaultTime = latestFaultTime;
        _repeatCount = repeatCount;
        _repeatSummary = repeatSummary;
    }

    public string FirstFaultTime
    {
        get => _firstFaultTime;
        set => SetProperty(ref _firstFaultTime, value);
    }

    public string LatestFaultTime
    {
        get => _latestFaultTime;
        set => SetProperty(ref _latestFaultTime, value);
    }

    public int RepeatCount
    {
        get => _repeatCount;
        set
        {
            if (SetProperty(ref _repeatCount, value))
            {
                OnPropertyChanged(nameof(IsRepeated));
            }
        }
    }

    public string RepeatSummary
    {
        get => _repeatSummary;
        set => SetProperty(ref _repeatSummary, value);
    }

    public bool IsRepeated => RepeatCount > 1;
}
