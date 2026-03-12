using System.Collections.ObjectModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel
{
    private InspectionGroupConfigState? _selectedInspectionGroupConfig;
    private ManagedPointConfigState? _selectedManagedPointConfig;
    private ResponsibilityMappingConfigState? _selectedResponsibilityMappingConfig;
    private VideoProtocolStrategyConfigState? _selectedVideoProtocolStrategyConfig;
    private string _configWorkspaceFeedback = string.Empty;

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

    public ObservableCollection<ResponsibilityMappingConfigState> ResponsibilityMappingConfigs { get; } =
    [
        new("unit-01", "沿江运营一中心", 24, "张维", "13800138001", "刘峰", "13800138011"),
        new("unit-02", "桥梁联防中心", 18, "陈航", "13800138002", "杜川", "13800138012"),
        new("unit-03", "文旅联合中心", 15, "赵宁", "13800138003", "沈岚", "13800138013")
    ];

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

    public ICommand SelectVideoProtocolStrategyConfigCommand => new RelayCommand(parameter =>
    {
        if (parameter is VideoProtocolStrategyConfigState item)
        {
            SelectVideoProtocolStrategyConfig(item);
        }
    });

    public ICommand TriggerConfigPlaceholderActionCommand => new RelayCommand(parameter =>
    {
        if (parameter is string actionText)
        {
            ConfigWorkspaceFeedback = actionText;
        }
    });

    public string ConfigListTitle => _textService.Resolve(TextTokens.SettingsConfigListTitle);
    public string ConfigDetailsTitle => _textService.Resolve(TextTokens.SettingsConfigDetailsTitle);
    public string ConfigActionNewText => _textService.Resolve(TextTokens.SettingsConfigActionNew);
    public string ConfigActionEditText => _textService.Resolve(TextTokens.SettingsConfigActionEdit);
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
    public string ResponsibilityPageTitle => _textService.Resolve(TextTokens.SettingsResponsibilityPageTitle);
    public string ResponsibilityPageDescription => _textService.Resolve(TextTokens.SettingsResponsibilityPageDescription);
    public string ResponsibilityUnitLabel => _textService.Resolve(TextTokens.SettingsResponsibilityUnitLabel);
    public string ResponsibilityPointCountLabel => _textService.Resolve(TextTokens.SettingsResponsibilityPointCountLabel);
    public string ResponsibilityMaintainerLabel => _textService.Resolve(TextTokens.SettingsResponsibilityMaintainerLabel);
    public string ResponsibilityMaintainerPhoneLabel => _textService.Resolve(TextTokens.SettingsResponsibilityMaintainerPhoneLabel);
    public string ResponsibilitySupervisorLabel => _textService.Resolve(TextTokens.SettingsResponsibilitySupervisorLabel);
    public string ResponsibilitySupervisorPhoneLabel => _textService.Resolve(TextTokens.SettingsResponsibilitySupervisorPhoneLabel);
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
    public bool IsVideoStrategyVisible => SelectedSection?.Key == SettingsSectionKey.VideoProtocolStrategy;

    private void InitializeConfigSelections()
    {
        SelectInspectionGroupConfig(InspectionGroupConfigs.First());
        SelectManagedPointConfig(ManagedPointConfigs.First());
        SelectResponsibilityMappingConfig(ResponsibilityMappingConfigs.First());
        SelectVideoProtocolStrategyConfig(VideoProtocolStrategyConfigs.First());
        ConfigWorkspaceFeedback = _textService.Resolve(TextTokens.SettingsAppliedStatusHint);
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
