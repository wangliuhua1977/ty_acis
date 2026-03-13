using System.Collections.ObjectModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel
{
    private InspectionGroupConfigState? _selectedInspectionGroupConfig;
    private ManagedPointConfigState? _selectedManagedPointConfig;
    private ResponsibilityMappingConfigState? _selectedResponsibilityMappingConfig;
    private ResponsibilityMappingEditorState? _responsibilityMappingEditor;
    private NotificationChannelConfigState? _selectedNotificationChannelConfig;
    private NotificationChannelEditorState? _notificationChannelEditor;
    private VideoProtocolStrategyConfigState? _selectedVideoProtocolStrategyConfig;
    private string _configWorkspaceFeedback = string.Empty;
    private bool _notificationDemoFallbackEnabled;
    private string _notificationServiceMode = string.Empty;

    public ObservableCollection<InspectionGroupConfigState> InspectionGroupConfigs { get; } =
    [
        new("group-01", "沿江慢直播保障一组", "06:30", 4, "间隔 120 分钟", "上墙复核", "人工派单", "已启用"),
        new("group-02", "桥梁联防巡检组", "07:00", 6, "间隔 90 分钟", "直接进入派单流程", "自动派单", "已启用"),
        new("group-03", "夜景值守保障组", "18:30", 3, "间隔 180 分钟", "上墙复核", "人工派单", "已停用")
    ];

    public ObservableCollection<ManagedPointConfigState> ManagedPointConfigs { get; } =
    [
        new("point-01", "轮渡码头北口", "沿江运营一中心", "默认巡检", "在线 / 播放 / 画面", "否", "否"),
        new("point-02", "江心灯塔监看点", "航道监护中心", "离线优先", "在线 / 播放", "否", "恢复前暂停"),
        new("point-03", "城市阳台主广场", "文旅联合中心", "重点点位增强", "在线 / 播放 / 画面 / 截图", "否", "否")
    ];

    public ObservableCollection<ResponsibilityMappingConfigState> ResponsibilityMappingConfigs { get; } = [];

    public ObservableCollection<NotificationChannelConfigState> NotificationChannelConfigs { get; } = [];

    public ObservableCollection<VideoProtocolStrategyConfigState> VideoProtocolStrategyConfigs { get; } =
    [
        new("video-01", "沿江运营一中心", "GB28181", "自动优化", "2.3 秒", "96%", "2026-03-12 08:35"),
        new("video-02", "桥梁联防中心", "RTSP", "手工锁定", "3.1 秒", "91%", "2026-03-12 07:50"),
        new("video-03", "文旅联合中心", "FLV", "自动优化", "2.0 秒", "98%", "2026-03-12 08:12")
    ];

    public InspectionGroupConfigState? SelectedInspectionGroupConfig
    {
        get => _selectedInspectionGroupConfig;
        private set => SetProperty(ref _selectedInspectionGroupConfig, value);
    }

    public ManagedPointConfigState? SelectedManagedPointConfig
    {
        get => _selectedManagedPointConfig;
        private set => SetProperty(ref _selectedManagedPointConfig, value);
    }

    public ResponsibilityMappingConfigState? SelectedResponsibilityMappingConfig
    {
        get => _selectedResponsibilityMappingConfig;
        private set => SetProperty(ref _selectedResponsibilityMappingConfig, value);
    }

    public ResponsibilityMappingEditorState? ResponsibilityMappingEditor
    {
        get => _responsibilityMappingEditor;
        private set => SetProperty(ref _responsibilityMappingEditor, value);
    }

    public NotificationChannelConfigState? SelectedNotificationChannelConfig
    {
        get => _selectedNotificationChannelConfig;
        private set => SetProperty(ref _selectedNotificationChannelConfig, value);
    }

    public NotificationChannelEditorState? NotificationChannelEditor
    {
        get => _notificationChannelEditor;
        private set => SetProperty(ref _notificationChannelEditor, value);
    }

    public VideoProtocolStrategyConfigState? SelectedVideoProtocolStrategyConfig
    {
        get => _selectedVideoProtocolStrategyConfig;
        private set => SetProperty(ref _selectedVideoProtocolStrategyConfig, value);
    }

    public string ConfigWorkspaceFeedback
    {
        get => _configWorkspaceFeedback;
        private set => SetProperty(ref _configWorkspaceFeedback, value);
    }

    public bool NotificationDemoFallbackEnabled
    {
        get => _notificationDemoFallbackEnabled;
        set => SetProperty(ref _notificationDemoFallbackEnabled, value);
    }

    public string NotificationServiceMode
    {
        get => _notificationServiceMode;
        private set => SetProperty(ref _notificationServiceMode, value);
    }

    public ICommand SelectInspectionGroupConfigCommand => new RelayCommand(parameter =>
    {
        if (parameter is InspectionGroupConfigState item)
        {
            SelectInspectionGroupConfig(item);
        }
    });

    public ICommand SelectManagedPointConfigCommand => new RelayCommand(parameter =>
    {
        if (parameter is ManagedPointConfigState item)
        {
            SelectManagedPointConfig(item);
        }
    });

    public ICommand SelectResponsibilityMappingConfigCommand => new RelayCommand(parameter =>
    {
        if (parameter is ResponsibilityMappingConfigState item)
        {
            SelectResponsibilityMappingConfig(item);
        }
    });

    public ICommand NewResponsibilityMappingCommand => new RelayCommand(_ => BeginNewResponsibilityMapping());

    public ICommand SaveResponsibilityMappingCommand => new RelayCommand(_ => SaveResponsibilityMapping(), _ => ResponsibilityMappingEditor is not null);

    public ICommand DeleteResponsibilityMappingCommand => new RelayCommand(_ => DeleteResponsibilityMapping(), _ => SelectedResponsibilityMappingConfig is not null);

    public ICommand TriggerConfigPlaceholderActionCommand => new RelayCommand(parameter =>
    {
        if (parameter is string actionText)
        {
            ConfigWorkspaceFeedback = actionText;
        }
    });

    public ICommand SelectNotificationChannelConfigCommand => new RelayCommand(parameter =>
    {
        if (parameter is NotificationChannelConfigState item)
        {
            SelectNotificationChannelConfig(item);
        }
    });

    public ICommand NewNotificationChannelCommand => new RelayCommand(_ => BeginNewNotificationChannel());

    public ICommand SaveNotificationChannelCommand => new RelayCommand(_ => SaveNotificationChannel(), _ => NotificationChannelEditor is not null);

    public ICommand DeleteNotificationChannelCommand => new RelayCommand(_ => DeleteNotificationChannel(), _ => SelectedNotificationChannelConfig is not null);

    public ICommand SetDefaultNotificationChannelCommand => new RelayCommand(_ => SetDefaultNotificationChannel(), _ => SelectedNotificationChannelConfig is not null);

    public ICommand SelectVideoProtocolStrategyConfigCommand => new RelayCommand(parameter =>
    {
        if (parameter is VideoProtocolStrategyConfigState item)
        {
            SelectVideoProtocolStrategyConfig(item);
        }
    });

    public string ConfigListTitle => _textService.Resolve(TextTokens.SettingsConfigListTitle);
    public string ConfigDetailsTitle => _textService.Resolve(TextTokens.SettingsConfigDetailsTitle);
    public string ConfigActionNewText => "新建";
    public string ConfigActionEditText => "编辑";
    public string ConfigActionDeleteText => "删除";
    public string ConfigActionSaveText => "保存";
    public string ConfigActionSetDefaultText => "设为默认";
    public string ConfigActionSimulateText => _textService.Resolve(TextTokens.SettingsConfigActionSimulate);
    public string InspectionGroupsPageTitle => _textService.Resolve(TextTokens.SettingsInspectionGroupsPageTitle);
    public string InspectionGroupsPageDescription => _textService.Resolve(TextTokens.SettingsInspectionGroupsPageDescription);
    public string InspectionGroupNameLabel => _textService.Resolve(TextTokens.SettingsInspectionGroupNameLabel);
    public string InspectionGroupFirstRunLabel => _textService.Resolve(TextTokens.SettingsInspectionGroupFirstRunLabel);
    public string InspectionGroupDailyRunsLabel => _textService.Resolve(TextTokens.SettingsInspectionGroupDailyRunsLabel);
    public string InspectionGroupIntervalLabel => _textService.Resolve(TextTokens.SettingsInspectionGroupIntervalLabel);
    public string InspectionGroupResultModeLabel => _textService.Resolve(TextTokens.SettingsInspectionGroupResultModeLabel);
    public string InspectionGroupDispatchModeLabel => _textService.Resolve(TextTokens.SettingsInspectionGroupDispatchModeLabel);
    public string InspectionGroupEnabledLabel => _textService.Resolve(TextTokens.SettingsInspectionGroupEnabledLabel);
    public string PointsPageTitle => _textService.Resolve(TextTokens.SettingsPointsPageTitle);
    public string PointsPageDescription => _textService.Resolve(TextTokens.SettingsPointsPageDescription);
    public string PointNameLabel => _textService.Resolve(TextTokens.SettingsPointNameLabel);
    public string PointUnitLabel => _textService.Resolve(TextTokens.SettingsPointUnitLabel);
    public string PointControlStrategyLabel => _textService.Resolve(TextTokens.SettingsPointControlStrategyLabel);
    public string PointInspectionItemsLabel => _textService.Resolve(TextTokens.SettingsPointInspectionItemsLabel);
    public string PointSilentLabel => _textService.Resolve(TextTokens.SettingsPointSilentLabel);
    public string PointPausedLabel => _textService.Resolve(TextTokens.SettingsPointPausedLabel);
    public string ResponsibilityPageTitle => "单位与责任人映射";
    public string ResponsibilityPageDescription => "正式管理 dispatch-responsibility.json 中的设备/单位责任归属映射，保存后直接回写本地轻量文件。";
    public string ResponsibilityDeviceCodeLabel => "设备编码";
    public string ResponsibilityPointNameLabel => "点位名称";
    public string ResponsibilityUnitLabel => "当前处理单位";
    public string ResponsibilityMaintainerLabel => "排障维护人";
    public string ResponsibilityMaintainerPhoneLabel => "排障维护人手机号";
    public string ResponsibilitySupervisorLabel => "上级负责人";
    public string ResponsibilitySupervisorPhoneLabel => "上级负责人手机号";
    public string ResponsibilityChannelLabel => "通知通道 ID";
    public string ResponsibilitySourceLabel => "映射来源";
    public string NotificationChannelsPageTitle => "通知通道管理";
    public string NotificationChannelsPageDescription => "正式管理 dispatch-notification.json 中的 webhook 通道，不在普通业务页展示底层地址。";
    public string NotificationChannelIdLabel => "通道 ID";
    public string NotificationChannelNameLabel => "通道名称";
    public string NotificationChannelWebhookLabel => "Webhook 地址";
    public string NotificationChannelEnabledLabel => "是否启用";
    public string NotificationChannelDefaultLabel => "默认通道";
    public string NotificationFallbackLabel => "启用 demo fallback";
    public string NotificationServiceModeLabel => "当前通知模式";
    public string VideoStrategyPageTitle => _textService.Resolve(TextTokens.SettingsVideoStrategyPageTitle);
    public string VideoStrategyPageDescription => _textService.Resolve(TextTokens.SettingsVideoStrategyPageDescription);
    public string VideoUnitLabel => _textService.Resolve(TextTokens.SettingsVideoUnitLabel);
    public string VideoPreferredProtocolLabel => _textService.Resolve(TextTokens.SettingsVideoPreferredProtocolLabel);
    public string VideoManagementModeLabel => _textService.Resolve(TextTokens.SettingsVideoManagementModeLabel);
    public string VideoAverageLoadTimeLabel => _textService.Resolve(TextTokens.SettingsVideoAverageLoadTimeLabel);
    public string VideoSuccessRateLabel => _textService.Resolve(TextTokens.SettingsVideoSuccessRateLabel);
    public string VideoLatestVerifiedLabel => _textService.Resolve(TextTokens.SettingsVideoLatestVerifiedLabel);

    public bool IsInspectionGroupsVisible => SelectedSection?.Key == SettingsSectionKey.InspectionGroups;
    public bool IsPointsVisible => SelectedSection?.Key == SettingsSectionKey.PointManagement;
    public bool IsResponsibilityVisible => SelectedSection?.Key == SettingsSectionKey.ResponsibilityMapping;
    public bool IsNotificationChannelsVisible => SelectedSection?.Key == SettingsSectionKey.NotificationChannels;
    public bool IsVideoStrategyVisible => SelectedSection?.Key == SettingsSectionKey.VideoProtocolStrategy;

    private void InitializeConfigSelections()
    {
        SelectInspectionGroupConfig(InspectionGroupConfigs.First());
        SelectManagedPointConfig(ManagedPointConfigs.First());
        LoadResponsibilityMappings();
        LoadNotificationChannels();
        SelectVideoProtocolStrategyConfig(VideoProtocolStrategyConfigs.First());
        ConfigWorkspaceFeedback = _textService.Resolve(TextTokens.SettingsAppliedStatusHint);
    }

    private void LoadResponsibilityMappings(string? selectedId = null)
    {
        var settings = _dispatchResponsibilitySettingsService.Load();
        ResponsibilityMappingConfigs.Clear();

        foreach (var item in settings.DeviceAssignments.OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase))
        {
            ResponsibilityMappingConfigs.Add(new ResponsibilityMappingConfigState(
                $"device:{item.DeviceCode}",
                item.DeviceCode,
                item.PointName,
                item.CurrentHandlingUnit,
                item.MaintainerName,
                item.MaintainerPhone,
                item.SupervisorName,
                item.SupervisorPhone,
                item.NotificationChannelId,
                "设备映射"));
        }

        foreach (var item in settings.UnitAssignments.OrderBy(item => item.UnitName, StringComparer.OrdinalIgnoreCase))
        {
            ResponsibilityMappingConfigs.Add(new ResponsibilityMappingConfigState(
                $"unit:{item.UnitName}",
                string.Empty,
                string.Empty,
                item.CurrentHandlingUnit,
                item.MaintainerName,
                item.MaintainerPhone,
                item.SupervisorName,
                item.SupervisorPhone,
                item.NotificationChannelId,
                "单位映射"));
        }

        var selected = selectedId is null
            ? ResponsibilityMappingConfigs.FirstOrDefault()
            : ResponsibilityMappingConfigs.FirstOrDefault(item => item.Id == selectedId);
        if (selected is not null)
        {
            SelectResponsibilityMappingConfig(selected);
        }
        else
        {
            BeginNewResponsibilityMapping();
        }
    }

    private void LoadNotificationChannels(string? selectedId = null)
    {
        var settings = _notificationSettingsService.Load();
        NotificationDemoFallbackEnabled = settings.EnableDemoFallback;
        NotificationServiceMode = settings.ServiceMode;
        NotificationChannelConfigs.Clear();

        foreach (var item in settings.Channels.OrderByDescending(item => item.IsDefault).ThenBy(item => item.ChannelId, StringComparer.OrdinalIgnoreCase))
        {
            NotificationChannelConfigs.Add(new NotificationChannelConfigState(
                item.ChannelId,
                item.ChannelId,
                item.DisplayName,
                item.WebhookUrl,
                item.IsEnabled,
                item.IsDefault));
        }

        var selected = selectedId is null
            ? NotificationChannelConfigs.FirstOrDefault()
            : NotificationChannelConfigs.FirstOrDefault(item => item.Id == selectedId);
        if (selected is not null)
        {
            SelectNotificationChannelConfig(selected);
        }
        else
        {
            BeginNewNotificationChannel();
        }
    }

    private void SelectInspectionGroupConfig(InspectionGroupConfigState item)
    {
        SelectedInspectionGroupConfig = item;
        foreach (var config in InspectionGroupConfigs)
        {
            config.IsSelected = config == item;
        }
    }

    private void SelectManagedPointConfig(ManagedPointConfigState item)
    {
        SelectedManagedPointConfig = item;
        foreach (var config in ManagedPointConfigs)
        {
            config.IsSelected = config == item;
        }
    }

    private void SelectResponsibilityMappingConfig(ResponsibilityMappingConfigState item)
    {
        SelectedResponsibilityMappingConfig = item;
        foreach (var config in ResponsibilityMappingConfigs)
        {
            config.IsSelected = config == item;
        }

        ResponsibilityMappingEditor = new ResponsibilityMappingEditorState
        {
            EditorMode = $"编辑 {item.SourceLabel}",
            DeviceCode = item.DeviceCode,
            PointName = item.PointName,
            CurrentHandlingUnit = item.CurrentHandlingUnit,
            Maintainer = item.Maintainer,
            MaintainerPhone = item.MaintainerPhone,
            Supervisor = item.Supervisor,
            SupervisorPhone = item.SupervisorPhone,
            NotificationChannelId = item.NotificationChannelId
        };
    }

    private void BeginNewResponsibilityMapping()
    {
        foreach (var config in ResponsibilityMappingConfigs)
        {
            config.IsSelected = false;
        }

        SelectedResponsibilityMappingConfig = null;
        ResponsibilityMappingEditor = new ResponsibilityMappingEditorState
        {
            EditorMode = "新增映射",
            NotificationChannelId = NotificationChannelConfigs.FirstOrDefault(item => item.IsDefault)?.ChannelId ?? "default"
        };
    }

    private void SaveResponsibilityMapping()
    {
        if (ResponsibilityMappingEditor is null)
        {
            return;
        }

        var settings = _dispatchResponsibilitySettingsService.Load();
        var editor = ResponsibilityMappingEditor;
        var isDeviceMapping = !string.IsNullOrWhiteSpace(editor.DeviceCode);
        if (!isDeviceMapping && string.IsNullOrWhiteSpace(editor.CurrentHandlingUnit))
        {
            ConfigWorkspaceFeedback = "保存失败：设备编码或当前处理单位至少要填写一项。";
            return;
        }

        var deviceAssignments = settings.DeviceAssignments.ToList();
        var unitAssignments = settings.UnitAssignments.ToList();
        string selectedId;

        if (isDeviceMapping)
        {
            deviceAssignments.RemoveAll(item => string.Equals(item.DeviceCode, editor.DeviceCode, StringComparison.OrdinalIgnoreCase));
            deviceAssignments.Add(new DispatchResponsibilityAssignmentSettings(
                editor.DeviceCode.Trim(),
                editor.PointName.Trim(),
                editor.CurrentHandlingUnit.Trim(),
                editor.Maintainer.Trim(),
                editor.MaintainerPhone.Trim(),
                editor.Supervisor.Trim(),
                editor.SupervisorPhone.Trim(),
                string.IsNullOrWhiteSpace(editor.NotificationChannelId) ? "default" : editor.NotificationChannelId.Trim()));
            selectedId = $"device:{editor.DeviceCode.Trim()}";
        }
        else
        {
            unitAssignments.RemoveAll(item => string.Equals(item.UnitName, editor.CurrentHandlingUnit, StringComparison.OrdinalIgnoreCase));
            unitAssignments.Add(new DispatchResponsibilityUnitAssignmentSettings(
                editor.CurrentHandlingUnit.Trim(),
                editor.CurrentHandlingUnit.Trim(),
                editor.Maintainer.Trim(),
                editor.MaintainerPhone.Trim(),
                editor.Supervisor.Trim(),
                editor.SupervisorPhone.Trim(),
                string.IsNullOrWhiteSpace(editor.NotificationChannelId) ? "default" : editor.NotificationChannelId.Trim()));
            selectedId = $"unit:{editor.CurrentHandlingUnit.Trim()}";
        }

        _dispatchResponsibilitySettingsService.Save(settings with
        {
            DeviceAssignments = deviceAssignments,
            UnitAssignments = unitAssignments
        });

        LoadResponsibilityMappings(selectedId);
        ConfigWorkspaceFeedback = "责任归属映射已保存到本地轻量配置文件。";
    }

    private void DeleteResponsibilityMapping()
    {
        if (SelectedResponsibilityMappingConfig is null)
        {
            return;
        }

        var settings = _dispatchResponsibilitySettingsService.Load();
        if (SelectedResponsibilityMappingConfig.Id.StartsWith("device:", StringComparison.Ordinal))
        {
            var deviceAssignments = settings.DeviceAssignments
                .Where(item => !string.Equals(item.DeviceCode, SelectedResponsibilityMappingConfig.DeviceCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _dispatchResponsibilitySettingsService.Save(settings with { DeviceAssignments = deviceAssignments });
        }
        else
        {
            var unitAssignments = settings.UnitAssignments
                .Where(item => !string.Equals(item.UnitName, SelectedResponsibilityMappingConfig.CurrentHandlingUnit, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _dispatchResponsibilitySettingsService.Save(settings with { UnitAssignments = unitAssignments });
        }

        LoadResponsibilityMappings();
        ConfigWorkspaceFeedback = "责任归属映射已从本地轻量配置文件删除。";
    }

    private void SelectNotificationChannelConfig(NotificationChannelConfigState item)
    {
        SelectedNotificationChannelConfig = item;
        foreach (var config in NotificationChannelConfigs)
        {
            config.IsSelected = config == item;
        }

        NotificationChannelEditor = new NotificationChannelEditorState
        {
            EditorMode = "编辑通道",
            ChannelId = item.ChannelId,
            DisplayName = item.DisplayName,
            WebhookUrl = item.WebhookUrl,
            IsEnabled = item.IsEnabled,
            IsDefault = item.IsDefault
        };
    }

    private void BeginNewNotificationChannel()
    {
        foreach (var config in NotificationChannelConfigs)
        {
            config.IsSelected = false;
        }

        SelectedNotificationChannelConfig = null;
        NotificationChannelEditor = new NotificationChannelEditorState
        {
            EditorMode = "新增通道",
            IsEnabled = true,
            IsDefault = NotificationChannelConfigs.Count == 0
        };
    }

    private void SaveNotificationChannel()
    {
        if (NotificationChannelEditor is null)
        {
            return;
        }

        var editor = NotificationChannelEditor;
        if (string.IsNullOrWhiteSpace(editor.ChannelId))
        {
            ConfigWorkspaceFeedback = "保存失败：通道 ID 不能为空。";
            return;
        }

        var settings = _notificationSettingsService.Load();
        var channels = settings.Channels.ToList();
        channels.RemoveAll(item => string.Equals(item.ChannelId, editor.ChannelId, StringComparison.OrdinalIgnoreCase));
        channels.Add(new NotificationChannelSettings(
            editor.ChannelId.Trim(),
            string.IsNullOrWhiteSpace(editor.DisplayName) ? editor.ChannelId.Trim() : editor.DisplayName.Trim(),
            editor.WebhookUrl.Trim(),
            editor.IsEnabled,
            editor.IsDefault));
        _notificationSettingsService.Save(settings with
        {
            EnableDemoFallback = NotificationDemoFallbackEnabled,
            Channels = channels
        });

        LoadNotificationChannels(editor.ChannelId.Trim());
        ConfigWorkspaceFeedback = "通知通道配置已保存到本地轻量配置文件。";
    }

    private void DeleteNotificationChannel()
    {
        if (SelectedNotificationChannelConfig is null)
        {
            return;
        }

        var settings = _notificationSettingsService.Load();
        var channels = settings.Channels
            .Where(item => !string.Equals(item.ChannelId, SelectedNotificationChannelConfig.ChannelId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (channels.Count == 0)
        {
            channels.Add(new NotificationChannelSettings("default", "Default Dispatch Channel", string.Empty, false, true));
        }

        _notificationSettingsService.Save(settings with
        {
            EnableDemoFallback = NotificationDemoFallbackEnabled,
            Channels = channels
        });

        LoadNotificationChannels();
        ConfigWorkspaceFeedback = "通知通道已从本地轻量配置文件删除。";
    }

    private void SetDefaultNotificationChannel()
    {
        if (SelectedNotificationChannelConfig is null)
        {
            return;
        }

        var settings = _notificationSettingsService.Load();
        var channels = settings.Channels
            .Select(item => item with
            {
                IsDefault = string.Equals(item.ChannelId, SelectedNotificationChannelConfig.ChannelId, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
        _notificationSettingsService.Save(settings with
        {
            EnableDemoFallback = NotificationDemoFallbackEnabled,
            Channels = channels
        });

        LoadNotificationChannels(SelectedNotificationChannelConfig.ChannelId);
        ConfigWorkspaceFeedback = "默认通知通道已更新。";
    }

    private void SelectVideoProtocolStrategyConfig(VideoProtocolStrategyConfigState item)
    {
        SelectedVideoProtocolStrategyConfig = item;
        foreach (var config in VideoProtocolStrategyConfigs)
        {
            config.IsSelected = config == item;
        }
    }
}
