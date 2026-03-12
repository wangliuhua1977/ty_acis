using System.Collections.ObjectModel;
using System.Windows.Input;
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
    private readonly ITextService _textService;
    private string _currentTimeText = string.Empty;
    private PageViewModelBase _currentPageViewModel;
    private string _headerStatusFeedback = string.Empty;

    public ShellViewModel(
        ITextService textService,
        IClockService clockService,
        HomePageViewModel homePage,
        InspectionPageViewModel inspectionPage,
        DispatchPageViewModel dispatchPage,
        ReportsPageViewModel reportsPage,
        SettingsPageViewModel settingsPage)
    {
        _textService = textService;
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
        HeaderStatusFeedback = textService.Resolve(TextTokens.ShellHeaderFeedbackIdle);

        SelectHeaderMetricCommand = new RelayCommand(parameter =>
        {
            if (parameter is ShellStatusMetricState metric)
            {
                SelectHeaderMetric(metric);
            }
        });

        HeaderMetrics =
        [
            new ShellStatusMetricState(textService.Resolve(TextTokens.ShellHeaderInspectionTasks), "18"),
            new ShellStatusMetricState(textService.Resolve(TextTokens.ShellHeaderFaults), "7"),
            new ShellStatusMetricState(textService.Resolve(TextTokens.ShellHeaderOutstanding), "4"),
            new ShellStatusMetricState(textService.Resolve(TextTokens.ShellHeaderPendingReview), "2"),
            new ShellStatusMetricState(textService.Resolve(TextTokens.ShellHeaderPendingDispatch), "5")
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
        SelectHeaderMetric(HeaderMetrics.First());

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

    public ObservableCollection<ShellStatusMetricState> HeaderMetrics { get; }

    public ObservableCollection<NavigationItemState> NavigationItems { get; }

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
}
