using System.Collections.ObjectModel;
using System.Windows.Threading;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.Services.Time;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly IClockService _clockService;
    private readonly DispatcherTimer _clockTimer;
    private readonly Dictionary<AppSectionId, PageViewModelBase> _pageViewModels;
    private string _currentTimeText = string.Empty;
    private PageViewModelBase _currentPageViewModel;

    public ShellViewModel(
        ITextService textService,
        IClockService clockService,
        HomePageViewModel homePage,
        InspectionPageViewModel inspectionPage,
        DispatchPageViewModel dispatchPage,
        ReportsPageViewModel reportsPage,
        SettingsPageViewModel settingsPage)
    {
        _clockService = clockService;
        _pageViewModels = new Dictionary<AppSectionId, PageViewModelBase>
        {
            [AppSectionId.Home] = homePage,
            [AppSectionId.Inspection] = inspectionPage,
            [AppSectionId.Dispatch] = dispatchPage,
            [AppSectionId.Reports] = reportsPage,
            [AppSectionId.Settings] = settingsPage
        };

        ApplicationName = textService.Resolve(TextTokens.ApplicationName);
        CurrentUserLabel = textService.Resolve(TextTokens.ShellCurrentUserLabel);
        CurrentUserValue = textService.Resolve(TextTokens.ShellCurrentUserValue);
        CurrentTimeLabel = textService.Resolve(TextTokens.ShellCurrentTimeLabel);
        SearchPlaceholder = textService.Resolve(TextTokens.ShellSearchPlaceholder);
        ThemeEntry = textService.Resolve(TextTokens.ShellThemeEntry);
        SettingsEntry = textService.Resolve(TextTokens.ShellSettingsEntry);

        HeaderMetrics =
        [
            new MetricCardState(textService.Resolve(TextTokens.ShellHeaderInspectionTasks), "--", "待接入统计"),
            new MetricCardState(textService.Resolve(TextTokens.ShellHeaderFaults), "--", "待接入统计"),
            new MetricCardState(textService.Resolve(TextTokens.ShellHeaderOutstanding), "--", "待接入统计"),
            new MetricCardState(textService.Resolve(TextTokens.ShellHeaderRecovered), "--", "待接入统计")
        ];

        NavigationItems =
        [
            CreateNavigationItem(AppSectionId.Home, textService.Resolve(TextTokens.NavigationHome), "01"),
            CreateNavigationItem(AppSectionId.Inspection, textService.Resolve(TextTokens.NavigationInspection), "02"),
            CreateNavigationItem(AppSectionId.Dispatch, textService.Resolve(TextTokens.NavigationDispatch), "03"),
            CreateNavigationItem(AppSectionId.Reports, textService.Resolve(TextTokens.NavigationReports), "04"),
            CreateNavigationItem(AppSectionId.Settings, textService.Resolve(TextTokens.NavigationSettings), "05")
        ];

        _currentPageViewModel = homePage;
        UpdateSelection(AppSectionId.Home);
        UpdateCurrentTime();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateCurrentTime();
        _clockTimer.Start();
    }

    public string ApplicationName { get; }

    public string CurrentUserLabel { get; }

    public string CurrentUserValue { get; }

    public string CurrentTimeLabel { get; }

    public string SearchPlaceholder { get; }

    public string ThemeEntry { get; }

    public string SettingsEntry { get; }

    public ObservableCollection<MetricCardState> HeaderMetrics { get; }

    public ObservableCollection<NavigationItemState> NavigationItems { get; }

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
        CurrentPageViewModel = _pageViewModels[sectionId];
        UpdateSelection(sectionId);
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
}
