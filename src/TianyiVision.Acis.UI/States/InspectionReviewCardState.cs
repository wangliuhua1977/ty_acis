using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionReviewCardState : ViewModelBase
{
    private bool _isSelected;
    private InspectionReviewStatus _reviewStatus;
    private string _reviewStatusText;

    public InspectionReviewCardState(
        string pointId,
        string pointName,
        string unitName,
        string screenshotTitle,
        string inspectionResult,
        string faultDescription,
        string dispatchPoolEntry,
        string faultType,
        string onlineStatus,
        string playbackStatus,
        string imageStatus,
        string lastFaultTime,
        string lastInspectionConclusion,
        string reviewNotePlaceholder,
        bool isFault,
        bool entersDispatchPool,
        DateTime inspectedAt,
        InspectionReviewStatus reviewStatus,
        string reviewStatusText)
    {
        PointId = pointId;
        PointName = pointName;
        UnitName = unitName;
        ScreenshotTitle = screenshotTitle;
        InspectionResult = inspectionResult;
        FaultDescription = faultDescription;
        DispatchPoolEntry = dispatchPoolEntry;
        FaultType = faultType;
        OnlineStatus = onlineStatus;
        PlaybackStatus = playbackStatus;
        ImageStatus = imageStatus;
        LastFaultTime = lastFaultTime;
        LastInspectionConclusion = lastInspectionConclusion;
        ReviewNotePlaceholder = reviewNotePlaceholder;
        IsFault = isFault;
        EntersDispatchPool = entersDispatchPool;
        InspectedAt = inspectedAt;
        _reviewStatus = reviewStatus;
        _reviewStatusText = reviewStatusText;
    }

    public string PointId { get; }

    public string PointName { get; }

    public string UnitName { get; }

    public string ScreenshotTitle { get; }

    public string InspectionResult { get; }

    public string FaultDescription { get; }

    public string DispatchPoolEntry { get; }

    public string FaultType { get; }

    public string OnlineStatus { get; }

    public string PlaybackStatus { get; }

    public string ImageStatus { get; }

    public string LastFaultTime { get; }

    public string LastInspectionConclusion { get; }

    public string ReviewNotePlaceholder { get; }

    public bool IsFault { get; }

    public bool EntersDispatchPool { get; }

    public DateTime InspectedAt { get; }

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

    public string ReviewStatusText
    {
        get => _reviewStatusText;
        set => SetProperty(ref _reviewStatusText, value);
    }

    public bool IsReviewed => ReviewStatus == InspectionReviewStatus.Reviewed;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
