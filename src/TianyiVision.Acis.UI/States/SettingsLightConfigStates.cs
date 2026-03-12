using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class InspectionGroupConfigState : ViewModelBase
{
    private bool _isSelected;

    public InspectionGroupConfigState(
        string id,
        string name,
        string firstRunTime,
        int dailyRuns,
        string interval,
        string resultMode,
        string dispatchMode,
        string enabledStatus)
    {
        Id = id;
        Name = name;
        FirstRunTime = firstRunTime;
        DailyRuns = dailyRuns;
        Interval = interval;
        ResultMode = resultMode;
        DispatchMode = dispatchMode;
        EnabledStatus = enabledStatus;
    }

    public string Id { get; }
    public string Name { get; }
    public string FirstRunTime { get; }
    public int DailyRuns { get; }
    public string Interval { get; }
    public string ResultMode { get; }
    public string DispatchMode { get; }
    public string EnabledStatus { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ManagedPointConfigState : ViewModelBase
{
    private bool _isSelected;

    public ManagedPointConfigState(string id, string name, string unit, string controlStrategy, string inspectionItems, string silentStatus, string pausedStatus)
    {
        Id = id;
        Name = name;
        Unit = unit;
        ControlStrategy = controlStrategy;
        InspectionItems = inspectionItems;
        SilentStatus = silentStatus;
        PausedStatus = pausedStatus;
    }

    public string Id { get; }
    public string Name { get; }
    public string Unit { get; }
    public string ControlStrategy { get; }
    public string InspectionItems { get; }
    public string SilentStatus { get; }
    public string PausedStatus { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ResponsibilityMappingConfigState : ViewModelBase
{
    private bool _isSelected;

    public ResponsibilityMappingConfigState(string id, string unit, int pointCount, string maintainer, string maintainerPhone, string supervisor, string supervisorPhone)
    {
        Id = id;
        Unit = unit;
        PointCount = pointCount;
        Maintainer = maintainer;
        MaintainerPhone = maintainerPhone;
        Supervisor = supervisor;
        SupervisorPhone = supervisorPhone;
    }

    public string Id { get; }
    public string Unit { get; }
    public int PointCount { get; }
    public string Maintainer { get; }
    public string MaintainerPhone { get; }
    public string Supervisor { get; }
    public string SupervisorPhone { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class VideoProtocolStrategyConfigState : ViewModelBase
{
    private bool _isSelected;

    public VideoProtocolStrategyConfigState(string id, string unit, string preferredProtocol, string managementMode, string averageLoadTime, string successRate, string latestVerifiedAt)
    {
        Id = id;
        Unit = unit;
        PreferredProtocol = preferredProtocol;
        ManagementMode = managementMode;
        AverageLoadTime = averageLoadTime;
        SuccessRate = successRate;
        LatestVerifiedAt = latestVerifiedAt;
    }

    public string Id { get; }
    public string Unit { get; }
    public string PreferredProtocol { get; }
    public string ManagementMode { get; }
    public string AverageLoadTime { get; }
    public string SuccessRate { get; }
    public string LatestVerifiedAt { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
