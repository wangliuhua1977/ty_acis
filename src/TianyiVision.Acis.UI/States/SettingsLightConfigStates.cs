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

    public ResponsibilityMappingConfigState(
        string id,
        string deviceCode,
        string pointName,
        string currentHandlingUnit,
        string maintainer,
        string maintainerPhone,
        string supervisor,
        string supervisorPhone,
        string notificationChannelId,
        string sourceLabel)
    {
        Id = id;
        DeviceCode = deviceCode;
        PointName = pointName;
        CurrentHandlingUnit = currentHandlingUnit;
        Maintainer = maintainer;
        MaintainerPhone = maintainerPhone;
        Supervisor = supervisor;
        SupervisorPhone = supervisorPhone;
        NotificationChannelId = notificationChannelId;
        SourceLabel = sourceLabel;
    }

    public string Id { get; }
    public string DeviceCode { get; }
    public string PointName { get; }
    public string CurrentHandlingUnit { get; }
    public string Maintainer { get; }
    public string MaintainerPhone { get; }
    public string Supervisor { get; }
    public string SupervisorPhone { get; }
    public string NotificationChannelId { get; }
    public string SourceLabel { get; }
    public string PrimaryLabel => string.IsNullOrWhiteSpace(DeviceCode) ? CurrentHandlingUnit : DeviceCode;
    public string SecondaryLabel => string.IsNullOrWhiteSpace(PointName) ? $"{Maintainer} / {Supervisor}" : PointName;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ResponsibilityMappingEditorState : ViewModelBase
{
    private string _deviceCode = string.Empty;
    private string _pointName = string.Empty;
    private string _currentHandlingUnit = string.Empty;
    private string _maintainer = string.Empty;
    private string _maintainerPhone = string.Empty;
    private string _supervisor = string.Empty;
    private string _supervisorPhone = string.Empty;
    private string _notificationChannelId = string.Empty;

    public string EditorMode { get; set; } = "新增映射";

    public string DeviceCode
    {
        get => _deviceCode;
        set => SetProperty(ref _deviceCode, value);
    }

    public string PointName
    {
        get => _pointName;
        set => SetProperty(ref _pointName, value);
    }

    public string CurrentHandlingUnit
    {
        get => _currentHandlingUnit;
        set => SetProperty(ref _currentHandlingUnit, value);
    }

    public string Maintainer
    {
        get => _maintainer;
        set => SetProperty(ref _maintainer, value);
    }

    public string MaintainerPhone
    {
        get => _maintainerPhone;
        set => SetProperty(ref _maintainerPhone, value);
    }

    public string Supervisor
    {
        get => _supervisor;
        set => SetProperty(ref _supervisor, value);
    }

    public string SupervisorPhone
    {
        get => _supervisorPhone;
        set => SetProperty(ref _supervisorPhone, value);
    }

    public string NotificationChannelId
    {
        get => _notificationChannelId;
        set => SetProperty(ref _notificationChannelId, value);
    }

    public ResponsibilityMappingEditorState Clone()
    {
        return new ResponsibilityMappingEditorState
        {
            EditorMode = EditorMode,
            DeviceCode = DeviceCode,
            PointName = PointName,
            CurrentHandlingUnit = CurrentHandlingUnit,
            Maintainer = Maintainer,
            MaintainerPhone = MaintainerPhone,
            Supervisor = Supervisor,
            SupervisorPhone = SupervisorPhone,
            NotificationChannelId = NotificationChannelId
        };
    }
}

public sealed class ResponsibilityDefaultEditorState : ViewModelBase
{
    private string _currentHandlingUnit = string.Empty;
    private string _maintainer = string.Empty;
    private string _maintainerPhone = string.Empty;
    private string _supervisor = string.Empty;
    private string _supervisorPhone = string.Empty;
    private string _notificationChannelId = string.Empty;

    public string CurrentHandlingUnit
    {
        get => _currentHandlingUnit;
        set => SetProperty(ref _currentHandlingUnit, value);
    }

    public string Maintainer
    {
        get => _maintainer;
        set => SetProperty(ref _maintainer, value);
    }

    public string MaintainerPhone
    {
        get => _maintainerPhone;
        set => SetProperty(ref _maintainerPhone, value);
    }

    public string Supervisor
    {
        get => _supervisor;
        set => SetProperty(ref _supervisor, value);
    }

    public string SupervisorPhone
    {
        get => _supervisorPhone;
        set => SetProperty(ref _supervisorPhone, value);
    }

    public string NotificationChannelId
    {
        get => _notificationChannelId;
        set => SetProperty(ref _notificationChannelId, value);
    }
}

public sealed class NotificationChannelConfigState : ViewModelBase
{
    private bool _isSelected;

    public NotificationChannelConfigState(
        string id,
        string channelId,
        string displayName,
        string webhookUrl,
        bool isEnabled,
        bool isDefault)
    {
        Id = id;
        ChannelId = channelId;
        DisplayName = displayName;
        WebhookUrl = webhookUrl;
        IsEnabled = isEnabled;
        IsDefault = isDefault;
    }

    public string Id { get; }
    public string ChannelId { get; }
    public string DisplayName { get; }
    public string WebhookUrl { get; }
    public bool IsEnabled { get; }
    public bool IsDefault { get; }

    public string EnabledLabel => IsEnabled ? "已启用" : "已停用";
    public string DefaultLabel => IsDefault ? "默认通道" : "非默认";
    public string MaskedWebhook => string.IsNullOrWhiteSpace(WebhookUrl)
        ? "未配置 Webhook"
        : $"{WebhookUrl[..Math.Min(24, WebhookUrl.Length)]}{(WebhookUrl.Length > 24 ? "..." : string.Empty)}";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class NotificationChannelEditorState : ViewModelBase
{
    private string _channelId = string.Empty;
    private string _displayName = string.Empty;
    private string _webhookUrl = string.Empty;
    private bool _isEnabled = true;
    private bool _isDefault;

    public string EditorMode { get; set; } = "新增通道";

    public string ChannelId
    {
        get => _channelId;
        set => SetProperty(ref _channelId, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string WebhookUrl
    {
        get => _webhookUrl;
        set => SetProperty(ref _webhookUrl, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }

    public NotificationChannelEditorState Clone()
    {
        return new NotificationChannelEditorState
        {
            EditorMode = EditorMode,
            ChannelId = ChannelId,
            DisplayName = DisplayName,
            WebhookUrl = WebhookUrl,
            IsEnabled = IsEnabled,
            IsDefault = IsDefault
        };
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
