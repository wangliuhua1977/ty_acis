using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionReviewTaskSummaryState : ViewModelBase
{
    private string _groupName = string.Empty;
    private string _startedAt = string.Empty;
    private string _finishedAt = string.Empty;
    private string _totalPoints = string.Empty;
    private string _normalCount = string.Empty;
    private string _faultCount = string.Empty;
    private string _reviewedCount = string.Empty;
    private string _reviewStatusText = string.Empty;
    private string _transitionHint = string.Empty;
    private InspectionReviewStatus _reviewStatus;

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

    public string FinishedAt
    {
        get => _finishedAt;
        set => SetProperty(ref _finishedAt, value);
    }

    public string TotalPoints
    {
        get => _totalPoints;
        set => SetProperty(ref _totalPoints, value);
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

    public string ReviewedCount
    {
        get => _reviewedCount;
        set => SetProperty(ref _reviewedCount, value);
    }

    public string ReviewStatusText
    {
        get => _reviewStatusText;
        set => SetProperty(ref _reviewStatusText, value);
    }

    public string TransitionHint
    {
        get => _transitionHint;
        set => SetProperty(ref _transitionHint, value);
    }

    public InspectionReviewStatus ReviewStatus
    {
        get => _reviewStatus;
        set
        {
            if (SetProperty(ref _reviewStatus, value))
            {
                OnPropertyChanged(nameof(IsReviewed));
            }
        }
    }

    public bool IsReviewed => ReviewStatus == InspectionReviewStatus.Reviewed;
}
