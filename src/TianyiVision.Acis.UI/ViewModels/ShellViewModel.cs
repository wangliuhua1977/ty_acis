using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Home;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Time;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly IClockService _clockService;
    private readonly IHomeDashboardService _homeDashboardService;
    private readonly DispatcherTimer _clockTimer;
    private readonly Dictionary<AppSectionId, Func<PageViewModelBase>> _pageFactories;
    private readonly Dictionary<AppSectionId, PageViewModelBase> _pageViewModels;
    private readonly ITextService _textService;
    private string _applicationName = string.Empty;
    private string _currentTimeLabel = string.Empty;
    private string _currentUserLabel = string.Empty;
    private string _currentUserValue = string.Empty;
    private string _sidebarDescription = string.Empty;
    private string _currentTimeText = string.Empty;
    private PageViewModelBase _currentPageViewModel = null!;
    private string _headerStatusFeedback = string.Empty;
    private AppSectionId _currentSection = AppSectionId.Home;

    public ShellViewModel(
        ITextService textService,
        IClockService clockService,
        IHomeDashboardService homeDashboardService,
        Dictionary<AppSectionId, Func<PageViewModelBase>> pageFactories)
    {
        _textService = textService;
        _clockService = clockService;
        _homeDashboardService = homeDashboardService;
        _pageFactories = pageFactories;
        _pageViewModels = [];

        SelectHeaderMetricCommand = new RelayCommand(parameter =>
        {
            if (parameter is ShellStatusMetricState metric)
            {
                SelectHeaderMetric(metric);
            }
        });
        HeaderMetrics = [];
        NavigationItems = [];

        RefreshShellText();
        RebuildPages(AppSectionId.Home, null);
        UpdateCurrentTime();
        if (HeaderMetrics.Count > 0)
        {
            SelectHeaderMetric(HeaderMetrics.First());
        }

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateCurrentTime();
        _clockTimer.Start();

        _textService.ProfileChanged += HandleProfileChanged;
    }

    public string ApplicationName
    {
        get => _applicationName;
        private set => SetProperty(ref _applicationName, value);
    }

    public string CurrentUserLabel
    {
        get => _currentUserLabel;
        private set => SetProperty(ref _currentUserLabel, value);
    }

    public string CurrentUserValue
    {
        get => _currentUserValue;
        private set => SetProperty(ref _currentUserValue, value);
    }

    public string CurrentTimeLabel
    {
        get => _currentTimeLabel;
        private set => SetProperty(ref _currentTimeLabel, value);
    }

    public string SidebarDescription
    {
        get => _sidebarDescription;
        private set => SetProperty(ref _sidebarDescription, value);
    }

    public ObservableCollection<ShellStatusMetricState> HeaderMetrics { get; private set; }

    public ObservableCollection<NavigationItemState> NavigationItems { get; private set; }

    public ICommand SelectHeaderMetricCommand { get; }

    public string HeaderStatusFeedback
    {
        get => _headerStatusFeedback;
        private set => SetProperty(ref _headerStatusFeedback, value);
    }

    public string CurrentTimeText
    {
        get => _currentTimeText;
        private set => SetProperty(ref _currentTimeText, value);
    }

    public PageViewModelBase CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set => SetProperty(ref _currentPageViewModel, value);
    }

    private NavigationItemState CreateNavigationItem(AppSectionId sectionId, string label, string shortCode)
    {
        return new NavigationItemState(
            sectionId,
            label,
            shortCode,
            new RelayCommand(_ => Navigate(sectionId)));
    }

    private void Navigate(AppSectionId sectionId)
    {
        _currentSection = sectionId;
        CurrentPageViewModel = _pageViewModels[sectionId];
        UpdateSelection(sectionId);
        CurrentPageViewModel.OnNavigatedTo();
    }

    private void UpdateSelection(AppSectionId activeSection)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item.SectionId == activeSection;
        }

        OnPropertyChanged(nameof(NavigationItems));
    }

    private void UpdateCurrentTime()
    {
        CurrentTimeText = _clockService.GetCurrentTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void SelectHeaderMetric(ShellStatusMetricState metric)
    {
        foreach (var item in HeaderMetrics)
        {
            item.IsSelected = item == metric;
        }

        HeaderStatusFeedback = string.Format(
            _textService.Resolve(TextTokens.ShellHeaderFeedbackPattern),
            metric.Title);
    }

    private void HandleProfileChanged(object? sender, EventArgs e)
    {
        var currentHeaderIndex = HeaderMetrics.IndexOf(HeaderMetrics.FirstOrDefault(item => item.IsSelected) ?? HeaderMetrics.First());
        var settingsSection = (CurrentPageViewModel as SettingsPageViewModel)?.SelectedSectionKey;

        RefreshShellText();
        RebuildPages(_currentSection, settingsSection);

        if (HeaderMetrics.Count > 0)
        {
            var safeIndex = Math.Clamp(currentHeaderIndex, 0, HeaderMetrics.Count - 1);
            SelectHeaderMetric(HeaderMetrics[safeIndex]);
        }
    }

    private void RefreshShellText()
    {
        ApplicationName = _textService.Resolve(TextTokens.ApplicationName);
        CurrentUserLabel = _textService.Resolve(TextTokens.ShellCurrentUserLabel);
        CurrentUserValue = _textService.Resolve(TextTokens.ShellCurrentUserValue);
        CurrentTimeLabel = _textService.Resolve(TextTokens.ShellCurrentTimeLabel);
        SidebarDescription = _textService.Resolve(TextTokens.ShellSidebarDescription);
        HeaderStatusFeedback = _textService.Resolve(TextTokens.ShellHeaderFeedbackIdle);

        var headerMetricsSnapshot = GetHeaderMetricsSnapshot();
        HeaderMetrics = new ObservableCollection<ShellStatusMetricState>
        {
            new(_textService.Resolve(TextTokens.ShellHeaderInspectionTasks), headerMetricsSnapshot.InspectionTasks),
            new(_textService.Resolve(TextTokens.ShellHeaderFaults), headerMetricsSnapshot.Faults),
            new(_textService.Resolve(TextTokens.ShellHeaderOutstanding), headerMetricsSnapshot.Outstanding),
            new(_textService.Resolve(TextTokens.ShellHeaderPendingReview), headerMetricsSnapshot.PendingReview),
            new(_textService.Resolve(TextTokens.ShellHeaderPendingDispatch), headerMetricsSnapshot.PendingDispatch)
        };
        OnPropertyChanged(nameof(HeaderMetrics));

        NavigationItems = new ObservableCollection<NavigationItemState>
        {
            CreateNavigationItem(AppSectionId.Home, _textService.Resolve(TextTokens.NavigationHome), "01"),
            CreateNavigationItem(AppSectionId.Inspection, _textService.Resolve(TextTokens.NavigationInspection), "02"),
            CreateNavigationItem(AppSectionId.Dispatch, _textService.Resolve(TextTokens.NavigationDispatch), "03"),
            CreateNavigationItem(AppSectionId.Reports, _textService.Resolve(TextTokens.NavigationReports), "04"),
            CreateNavigationItem(AppSectionId.Settings, _textService.Resolve(TextTokens.NavigationSettings), "05")
        };
        OnPropertyChanged(nameof(NavigationItems));
    }

    private void RebuildPages(AppSectionId activeSection, SettingsSectionKey? settingsSection)
    {
        _pageViewModels.Clear();

        foreach (var pair in _pageFactories)
        {
            var page = pair.Value();
            page.NavigateToSection = Navigate;
            _pageViewModels[pair.Key] = page;
        }

        if (settingsSection.HasValue
            && _pageViewModels[AppSectionId.Settings] is SettingsPageViewModel settingsPage)
        {
            settingsPage.ActivateSection(settingsSection.Value);
        }

        CurrentPageViewModel = _pageViewModels[activeSection];
        UpdateSelection(activeSection);
        CurrentPageViewModel.OnNavigatedTo();
    }

    private HomeHeaderMetricsModel GetHeaderMetricsSnapshot()
    {
        var dashboardResponse = _homeDashboardService.GetDashboard();
        if (dashboardResponse.IsSuccess)
        {
            var metrics = dashboardResponse.Data.HeaderMetrics;
            MapPointSourceDiagnostics.Write(
                "ShellHeaderMetrics",
                $"shell header metrics binding = inspectionTasks:{metrics.InspectionTasks}, faults:{metrics.Faults}, outstanding:{metrics.Outstanding}, pendingReview:{metrics.PendingReview}, pendingDispatch:{metrics.PendingDispatch}");
            return metrics;
        }

        MapPointSourceDiagnostics.Write(
            "ShellHeaderMetrics",
            $"shell header metrics unavailable, fallback to pending values: reason = {NormalizeReason(dashboardResponse.Message)}");
        return new HomeHeaderMetricsModel("待接入", "待接入", "待接入", "待接入", "待接入");
    }

    private static string NormalizeReason(string? message)
    {
        return string.IsNullOrWhiteSpace(message) ? "首页数据暂不可用" : message.Trim();
    }
}
