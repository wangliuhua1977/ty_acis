using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class ReportsPageViewModel : PageViewModelBase
{
    private const string AllOptionKey = "all";
    private const string TimeTodayKey = "today";
    private const string TimeWeekKey = "week";
    private const string TimeMonthKey = "month";
    private const string TimeCustomKey = "custom";

    private readonly ITextService _textService;
    private readonly List<InspectionExecutionReportRowState> _allInspectionRows;
    private readonly List<FaultStatisticsReportRowState> _allFaultRows;
    private readonly List<DispatchDisposalReportRowState> _allDispatchRows;
    private readonly List<ResponsibilityOwnershipReportRowState> _allResponsibilityRows;
    private readonly List<OutstandingFaultReportRowState> _allOutstandingRows;
    private bool _suspendFilterRefresh;
    private ReportFilterState _filterState = null!;
    private ObservableCollection<InspectionExecutionReportRowState> _filteredInspectionRows = [];
    private ObservableCollection<FaultStatisticsReportRowState> _filteredFaultRows = [];
    private ObservableCollection<DispatchDisposalReportRowState> _filteredDispatchRows = [];
    private ObservableCollection<ResponsibilityOwnershipReportRowState> _filteredResponsibilityRows = [];
    private ObservableCollection<OutstandingFaultReportRowState> _filteredOutstandingRows = [];
    private ReportViewKind _selectedReportView;
    private string _workspaceFeedback = string.Empty;
    private string _currentRowCountText = string.Empty;

    public ReportsPageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.ReportsTitle),
            textService.Resolve(TextTokens.ReportsDescription))
    {
        _textService = textService;
        InitializeText();

        _allInspectionRows = CreateInspectionExecutionRows();
        _allFaultRows = CreateFaultStatisticsRows();
        _allDispatchRows = CreateDispatchDisposalRows();
        _allResponsibilityRows = CreateResponsibilityRows();
        _allOutstandingRows = CreateOutstandingRows();

        FilterState = CreateFilterState();
        MetricCards =
        [
            CreateMetricCard("tasks", MetricTaskCountTitle, ReportsMetricTaskCountDescription, ReportViewKind.InspectionExecution),
            CreateMetricCard("points", MetricPointCountTitle, ReportsMetricPointCountDescription, ReportViewKind.InspectionExecution),
            CreateMetricCard("faults", MetricFaultTotalTitle, ReportsMetricFaultTotalDescription, ReportViewKind.FaultStatistics),
            CreateMetricCard("dispatched", MetricDispatchedCountTitle, ReportsMetricDispatchedCountDescription, ReportViewKind.DispatchDisposal),
            CreateMetricCard("unrecovered", MetricUnrecoveredCountTitle, ReportsMetricUnrecoveredCountDescription, ReportViewKind.OutstandingFaults),
            CreateMetricCard("recovered", MetricRecoveredCountTitle, ReportsMetricRecoveredCountDescription, ReportViewKind.DispatchDisposal)
        ];
        ReportTabs =
        [
            new ReportTabState(ReportViewKind.InspectionExecution, InspectionExecutionTabText),
            new ReportTabState(ReportViewKind.FaultStatistics, FaultStatisticsTabText),
            new ReportTabState(ReportViewKind.DispatchDisposal, DispatchDisposalTabText),
            new ReportTabState(ReportViewKind.ResponsibilityOwnership, ResponsibilityTabText),
            new ReportTabState(ReportViewKind.OutstandingFaults, OutstandingTabText)
        ];
        ExportActions =
        [
            new ReportExportActionState("excel", ExportExcelText, ExportExcelDescription),
            new ReportExportActionState("word", ExportWordText, ExportWordDescription)
        ];

        FaultTrendChart = new ReportChartSummaryState(
            textService.Resolve(TextTokens.ReportsChartTrendTitle),
            textService.Resolve(TextTokens.ReportsChartTrendDescription),
            textService.Resolve(TextTokens.ReportsChartTrendLegendFaults));
        FaultTypeChart = new ReportChartSummaryState(
            textService.Resolve(TextTokens.ReportsChartTypeTitle),
            textService.Resolve(TextTokens.ReportsChartTypeDescription),
            textService.Resolve(TextTokens.ReportsChartTypeLegendShare));
        DispatchRecoveryChart = new ReportChartSummaryState(
            textService.Resolve(TextTokens.ReportsChartRecoveryTitle),
            textService.Resolve(TextTokens.ReportsChartRecoveryDescription),
            textService.Resolve(TextTokens.ReportsChartRecoveryLegendDispatch),
            textService.Resolve(TextTokens.ReportsChartRecoveryLegendRecovery));

        ApplyFiltersCommand = new RelayCommand(_ => ApplyFilters());
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
        SelectMetricCommand = new RelayCommand(parameter =>
        {
            if (parameter is ReportMetricSummaryState metric)
            {
                SelectMetric(metric);
            }
        });
        SelectReportTabCommand = new RelayCommand(parameter =>
        {
            if (parameter is ReportTabState tab)
            {
                SelectReportView(tab.ViewKind);
                WorkspaceFeedback = tab.Label;
            }
        });
        TriggerExportCommand = new RelayCommand(parameter =>
        {
            if (parameter is ReportExportActionState action)
            {
                TriggerExport(action);
            }
        });

        SelectReportView(ReportViewKind.InspectionExecution);
        ApplyFilters();
    }

    public string FilterTitle { get; private set; } = string.Empty;
    public string FilterDescription { get; private set; } = string.Empty;
    public string FilterTimeLabel { get; private set; } = string.Empty;
    public string FilterGroupLabel { get; private set; } = string.Empty;
    public string FilterUnitLabel { get; private set; } = string.Empty;
    public string FilterFaultTypeLabel { get; private set; } = string.Empty;
    public string FilterApplyText { get; private set; } = string.Empty;
    public string FilterResetText { get; private set; } = string.Empty;
    public string FilterCustomPlaceholderText { get; private set; } = string.Empty;
    public string SummaryTitle { get; private set; } = string.Empty;
    public string SummaryDescription { get; private set; } = string.Empty;
    public string ChartsTitle { get; private set; } = string.Empty;
    public string ChartsDescription { get; private set; } = string.Empty;
    public string DetailsTitle { get; private set; } = string.Empty;
    public string DetailsDescription { get; private set; } = string.Empty;
    public string ExportTitle { get; private set; } = string.Empty;
    public string ExportDescription { get; private set; } = string.Empty;
    public string MetricTaskCountTitle { get; private set; } = string.Empty;
    public string MetricPointCountTitle { get; private set; } = string.Empty;
    public string MetricFaultTotalTitle { get; private set; } = string.Empty;
    public string MetricDispatchedCountTitle { get; private set; } = string.Empty;
    public string MetricUnrecoveredCountTitle { get; private set; } = string.Empty;
    public string MetricRecoveredCountTitle { get; private set; } = string.Empty;
    public string ReportsMetricTaskCountDescription { get; private set; } = string.Empty;
    public string ReportsMetricPointCountDescription { get; private set; } = string.Empty;
    public string ReportsMetricFaultTotalDescription { get; private set; } = string.Empty;
    public string ReportsMetricDispatchedCountDescription { get; private set; } = string.Empty;
    public string ReportsMetricUnrecoveredCountDescription { get; private set; } = string.Empty;
    public string ReportsMetricRecoveredCountDescription { get; private set; } = string.Empty;
    public string InspectionExecutionTabText { get; private set; } = string.Empty;
    public string FaultStatisticsTabText { get; private set; } = string.Empty;
    public string DispatchDisposalTabText { get; private set; } = string.Empty;
    public string ResponsibilityTabText { get; private set; } = string.Empty;
    public string OutstandingTabText { get; private set; } = string.Empty;
    public string ExportExcelText { get; private set; } = string.Empty;
    public string ExportWordText { get; private set; } = string.Empty;
    public string ExportExcelDescription { get; private set; } = string.Empty;
    public string ExportWordDescription { get; private set; } = string.Empty;
    public string EmptyStateTitle { get; private set; } = string.Empty;
    public string EmptyStateDescription { get; private set; } = string.Empty;
    public string InspectionDateLabel { get; private set; } = string.Empty;
    public string InspectionGroupLabel { get; private set; } = string.Empty;
    public string InspectionTaskRunsLabel { get; private set; } = string.Empty;
    public string InspectionPointTotalLabel { get; private set; } = string.Empty;
    public string InspectionNormalLabel { get; private set; } = string.Empty;
    public string InspectionFaultLabel { get; private set; } = string.Empty;
    public string InspectionCompletionRateLabel { get; private set; } = string.Empty;
    public string FaultDateLabel { get; private set; } = string.Empty;
    public string FaultTotalLabel { get; private set; } = string.Empty;
    public string FaultOfflineLabel { get; private set; } = string.Empty;
    public string FaultPlaybackLabel { get; private set; } = string.Empty;
    public string FaultImageLabel { get; private set; } = string.Empty;
    public string FaultNewLabel { get; private set; } = string.Empty;
    public string FaultRepeatedLabel { get; private set; } = string.Empty;
    public string DispatchDateLabel { get; private set; } = string.Empty;
    public string DispatchPendingLabel { get; private set; } = string.Empty;
    public string DispatchDispatchedLabel { get; private set; } = string.Empty;
    public string DispatchRecoveredLabel { get; private set; } = string.Empty;
    public string DispatchUnrecoveredLabel { get; private set; } = string.Empty;
    public string DispatchAutomaticLabel { get; private set; } = string.Empty;
    public string DispatchManualLabel { get; private set; } = string.Empty;
    public string ResponsibilityUnitLabel { get; private set; } = string.Empty;
    public string ResponsibilityMaintainerLabel { get; private set; } = string.Empty;
    public string ResponsibilitySupervisorLabel { get; private set; } = string.Empty;
    public string ResponsibilityFaultCountLabel { get; private set; } = string.Empty;
    public string ResponsibilityRecoveredLabel { get; private set; } = string.Empty;
    public string ResponsibilityUnrecoveredLabel { get; private set; } = string.Empty;
    public string OutstandingPointLabel { get; private set; } = string.Empty;
    public string OutstandingUnitLabel { get; private set; } = string.Empty;
    public string OutstandingMaintainerLabel { get; private set; } = string.Empty;
    public string OutstandingSupervisorLabel { get; private set; } = string.Empty;
    public string OutstandingFaultTypeLabel { get; private set; } = string.Empty;
    public string OutstandingFirstFaultLabel { get; private set; } = string.Empty;
    public string OutstandingLatestFaultLabel { get; private set; } = string.Empty;
    public string OutstandingStatusLabel { get; private set; } = string.Empty;

    public ReportFilterState FilterState
    {
        get => _filterState;
        private set
        {
            if (_filterState is not null)
            {
                _filterState.PropertyChanged -= HandleFilterStateChanged;
            }

            _filterState = value;
            _filterState.PropertyChanged += HandleFilterStateChanged;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ReportMetricSummaryState> MetricCards { get; }
    public ObservableCollection<ReportTabState> ReportTabs { get; }
    public ObservableCollection<ReportExportActionState> ExportActions { get; }
    public ReportChartSummaryState FaultTrendChart { get; }
    public ReportChartSummaryState FaultTypeChart { get; }
    public ReportChartSummaryState DispatchRecoveryChart { get; }

    public ObservableCollection<InspectionExecutionReportRowState> FilteredInspectionRows
    {
        get => _filteredInspectionRows;
        private set
        {
            if (SetProperty(ref _filteredInspectionRows, value))
            {
                OnPropertyChanged(nameof(HasInspectionRows));
            }
        }
    }

    public ObservableCollection<FaultStatisticsReportRowState> FilteredFaultRows
    {
        get => _filteredFaultRows;
        private set
        {
            if (SetProperty(ref _filteredFaultRows, value))
            {
                OnPropertyChanged(nameof(HasFaultRows));
            }
        }
    }

    public ObservableCollection<DispatchDisposalReportRowState> FilteredDispatchRows
    {
        get => _filteredDispatchRows;
        private set
        {
            if (SetProperty(ref _filteredDispatchRows, value))
            {
                OnPropertyChanged(nameof(HasDispatchRows));
            }
        }
    }

    public ObservableCollection<ResponsibilityOwnershipReportRowState> FilteredResponsibilityRows
    {
        get => _filteredResponsibilityRows;
        private set
        {
            if (SetProperty(ref _filteredResponsibilityRows, value))
            {
                OnPropertyChanged(nameof(HasResponsibilityRows));
            }
        }
    }

    public ObservableCollection<OutstandingFaultReportRowState> FilteredOutstandingRows
    {
        get => _filteredOutstandingRows;
        private set
        {
            if (SetProperty(ref _filteredOutstandingRows, value))
            {
                OnPropertyChanged(nameof(HasOutstandingRows));
            }
        }
    }

    public ReportViewKind SelectedReportView
    {
        get => _selectedReportView;
        private set
        {
            if (SetProperty(ref _selectedReportView, value))
            {
                OnPropertyChanged(nameof(IsInspectionExecutionSelected));
                OnPropertyChanged(nameof(IsFaultStatisticsSelected));
                OnPropertyChanged(nameof(IsDispatchDisposalSelected));
                OnPropertyChanged(nameof(IsResponsibilitySelected));
                OnPropertyChanged(nameof(IsOutstandingSelected));
                OnPropertyChanged(nameof(HasCurrentRows));
                OnPropertyChanged(nameof(HasNoCurrentRows));
            }
        }
    }

    public string WorkspaceFeedback
    {
        get => _workspaceFeedback;
        private set => SetProperty(ref _workspaceFeedback, value);
    }

    public string CurrentRowCountText
    {
        get => _currentRowCountText;
        private set => SetProperty(ref _currentRowCountText, value);
    }

    public bool IsCustomRangeSelected => FilterState.SelectedTimeRangeOption?.Key == TimeCustomKey;
    public bool IsInspectionExecutionSelected => SelectedReportView == ReportViewKind.InspectionExecution;
    public bool IsFaultStatisticsSelected => SelectedReportView == ReportViewKind.FaultStatistics;
    public bool IsDispatchDisposalSelected => SelectedReportView == ReportViewKind.DispatchDisposal;
    public bool IsResponsibilitySelected => SelectedReportView == ReportViewKind.ResponsibilityOwnership;
    public bool IsOutstandingSelected => SelectedReportView == ReportViewKind.OutstandingFaults;
    public bool HasInspectionRows => FilteredInspectionRows.Count > 0;
    public bool HasFaultRows => FilteredFaultRows.Count > 0;
    public bool HasDispatchRows => FilteredDispatchRows.Count > 0;
    public bool HasResponsibilityRows => FilteredResponsibilityRows.Count > 0;
    public bool HasOutstandingRows => FilteredOutstandingRows.Count > 0;
    public bool HasCurrentRows => GetCurrentRowCount() > 0;
    public bool HasNoCurrentRows => !HasCurrentRows;

    public ICommand ApplyFiltersCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand SelectMetricCommand { get; }
    public ICommand SelectReportTabCommand { get; }
    public ICommand TriggerExportCommand { get; }

    private void InitializeText()
    {
        FilterTitle = _textService.Resolve(TextTokens.ReportsFilterTitle);
        FilterDescription = _textService.Resolve(TextTokens.ReportsFilterDescription);
        FilterTimeLabel = _textService.Resolve(TextTokens.ReportsFilterTimeLabel);
        FilterGroupLabel = _textService.Resolve(TextTokens.ReportsFilterGroupLabel);
        FilterUnitLabel = _textService.Resolve(TextTokens.ReportsFilterUnitLabel);
        FilterFaultTypeLabel = _textService.Resolve(TextTokens.ReportsFilterFaultTypeLabel);
        FilterApplyText = _textService.Resolve(TextTokens.ReportsFilterApplyAction);
        FilterResetText = _textService.Resolve(TextTokens.ReportsFilterResetAction);
        FilterCustomPlaceholderText = _textService.Resolve(TextTokens.ReportsFilterCustomPlaceholder);
        SummaryTitle = _textService.Resolve(TextTokens.ReportsSummaryTitle);
        SummaryDescription = _textService.Resolve(TextTokens.ReportsSummaryDescription);
        ChartsTitle = _textService.Resolve(TextTokens.ReportsChartsTitle);
        ChartsDescription = _textService.Resolve(TextTokens.ReportsChartsDescription);
        DetailsTitle = _textService.Resolve(TextTokens.ReportsDetailsTitle);
        DetailsDescription = _textService.Resolve(TextTokens.ReportsDetailsDescription);
        ExportTitle = _textService.Resolve(TextTokens.ReportsExportTitle);
        ExportDescription = _textService.Resolve(TextTokens.ReportsExportDescription);
        MetricTaskCountTitle = _textService.Resolve(TextTokens.ReportsMetricTaskCountTitle);
        MetricPointCountTitle = _textService.Resolve(TextTokens.ReportsMetricPointCountTitle);
        MetricFaultTotalTitle = _textService.Resolve(TextTokens.ReportsMetricFaultTotalTitle);
        MetricDispatchedCountTitle = _textService.Resolve(TextTokens.ReportsMetricDispatchedCountTitle);
        MetricUnrecoveredCountTitle = _textService.Resolve(TextTokens.ReportsMetricUnrecoveredCountTitle);
        MetricRecoveredCountTitle = _textService.Resolve(TextTokens.ReportsMetricRecoveredCountTitle);
        ReportsMetricTaskCountDescription = _textService.Resolve(TextTokens.ReportsMetricTaskCountDescription);
        ReportsMetricPointCountDescription = _textService.Resolve(TextTokens.ReportsMetricPointCountDescription);
        ReportsMetricFaultTotalDescription = _textService.Resolve(TextTokens.ReportsMetricFaultTotalDescription);
        ReportsMetricDispatchedCountDescription = _textService.Resolve(TextTokens.ReportsMetricDispatchedCountDescription);
        ReportsMetricUnrecoveredCountDescription = _textService.Resolve(TextTokens.ReportsMetricUnrecoveredCountDescription);
        ReportsMetricRecoveredCountDescription = _textService.Resolve(TextTokens.ReportsMetricRecoveredCountDescription);
        InspectionExecutionTabText = _textService.Resolve(TextTokens.ReportsTabInspectionExecution);
        FaultStatisticsTabText = _textService.Resolve(TextTokens.ReportsTabFaultStatistics);
        DispatchDisposalTabText = _textService.Resolve(TextTokens.ReportsTabDispatchDisposal);
        ResponsibilityTabText = _textService.Resolve(TextTokens.ReportsTabResponsibility);
        OutstandingTabText = _textService.Resolve(TextTokens.ReportsTabOutstanding);
        ExportExcelText = _textService.Resolve(TextTokens.ReportsExportExcel);
        ExportWordText = _textService.Resolve(TextTokens.ReportsExportWord);
        ExportExcelDescription = _textService.Resolve(TextTokens.ReportsExportExcelDescription);
        ExportWordDescription = _textService.Resolve(TextTokens.ReportsExportWordDescription);
        EmptyStateTitle = _textService.Resolve(TextTokens.ReportsEmptyStateTitle);
        EmptyStateDescription = _textService.Resolve(TextTokens.ReportsEmptyStateDescription);
        InspectionDateLabel = _textService.Resolve(TextTokens.ReportsTableInspectionDateLabel);
        InspectionGroupLabel = _textService.Resolve(TextTokens.ReportsTableInspectionGroupLabel);
        InspectionTaskRunsLabel = _textService.Resolve(TextTokens.ReportsTableInspectionTaskRunsLabel);
        InspectionPointTotalLabel = _textService.Resolve(TextTokens.ReportsTableInspectionPointTotalLabel);
        InspectionNormalLabel = _textService.Resolve(TextTokens.ReportsTableInspectionNormalLabel);
        InspectionFaultLabel = _textService.Resolve(TextTokens.ReportsTableInspectionFaultLabel);
        InspectionCompletionRateLabel = _textService.Resolve(TextTokens.ReportsTableInspectionCompletionRateLabel);
        FaultDateLabel = _textService.Resolve(TextTokens.ReportsTableFaultDateLabel);
        FaultTotalLabel = _textService.Resolve(TextTokens.ReportsTableFaultTotalLabel);
        FaultOfflineLabel = _textService.Resolve(TextTokens.ReportsTableFaultOfflineLabel);
        FaultPlaybackLabel = _textService.Resolve(TextTokens.ReportsTableFaultPlaybackLabel);
        FaultImageLabel = _textService.Resolve(TextTokens.ReportsTableFaultImageLabel);
        FaultNewLabel = _textService.Resolve(TextTokens.ReportsTableFaultNewLabel);
        FaultRepeatedLabel = _textService.Resolve(TextTokens.ReportsTableFaultRepeatedLabel);
        DispatchDateLabel = _textService.Resolve(TextTokens.ReportsTableDispatchDateLabel);
        DispatchPendingLabel = _textService.Resolve(TextTokens.ReportsTableDispatchPendingLabel);
        DispatchDispatchedLabel = _textService.Resolve(TextTokens.ReportsTableDispatchDispatchedLabel);
        DispatchRecoveredLabel = _textService.Resolve(TextTokens.ReportsTableDispatchRecoveredLabel);
        DispatchUnrecoveredLabel = _textService.Resolve(TextTokens.ReportsTableDispatchUnrecoveredLabel);
        DispatchAutomaticLabel = _textService.Resolve(TextTokens.ReportsTableDispatchAutomaticLabel);
        DispatchManualLabel = _textService.Resolve(TextTokens.ReportsTableDispatchManualLabel);
        ResponsibilityUnitLabel = _textService.Resolve(TextTokens.ReportsTableResponsibilityUnitLabel);
        ResponsibilityMaintainerLabel = _textService.Resolve(TextTokens.ReportsTableResponsibilityMaintainerLabel);
        ResponsibilitySupervisorLabel = _textService.Resolve(TextTokens.ReportsTableResponsibilitySupervisorLabel);
        ResponsibilityFaultCountLabel = _textService.Resolve(TextTokens.ReportsTableResponsibilityFaultCountLabel);
        ResponsibilityRecoveredLabel = _textService.Resolve(TextTokens.ReportsTableResponsibilityRecoveredLabel);
        ResponsibilityUnrecoveredLabel = _textService.Resolve(TextTokens.ReportsTableResponsibilityUnrecoveredLabel);
        OutstandingPointLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingPointLabel);
        OutstandingUnitLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingUnitLabel);
        OutstandingMaintainerLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingMaintainerLabel);
        OutstandingSupervisorLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingSupervisorLabel);
        OutstandingFaultTypeLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingFaultTypeLabel);
        OutstandingFirstFaultLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingFirstFaultLabel);
        OutstandingLatestFaultLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingLatestFaultLabel);
        OutstandingStatusLabel = _textService.Resolve(TextTokens.ReportsTableOutstandingStatusLabel);
    }

    private ReportMetricSummaryState CreateMetricCard(string key, string title, string description, ReportViewKind relatedReportView)
    {
        return new ReportMetricSummaryState(key, title, relatedReportView)
        {
            Description = description
        };
    }

    private ReportFilterState CreateFilterState()
    {
        return new ReportFilterState(
            new ObservableCollection<ReportFilterOptionState>(
            [
                new ReportFilterOptionState(TimeTodayKey, _textService.Resolve(TextTokens.ReportsFilterToday)),
                new ReportFilterOptionState(TimeWeekKey, _textService.Resolve(TextTokens.ReportsFilterThisWeek)),
                new ReportFilterOptionState(TimeMonthKey, _textService.Resolve(TextTokens.ReportsFilterThisMonth)),
                new ReportFilterOptionState(TimeCustomKey, _textService.Resolve(TextTokens.ReportsFilterCustomRange))
            ]),
            BuildOptionCollection(
                _allInspectionRows.Select(item => item.InspectionGroupName)
                    .Concat(_allFaultRows.Select(item => item.InspectionGroupName))
                    .Concat(_allDispatchRows.Select(item => item.InspectionGroupName))
                    .Concat(_allResponsibilityRows.Select(item => item.InspectionGroupName))
                    .Concat(_allOutstandingRows.Select(item => item.InspectionGroupName)),
                _textService.Resolve(TextTokens.ReportsFilterAllGroups)),
            BuildOptionCollection(
                _allInspectionRows.Select(item => item.CurrentHandlingUnit)
                    .Concat(_allFaultRows.Select(item => item.CurrentHandlingUnit))
                    .Concat(_allDispatchRows.Select(item => item.CurrentHandlingUnit))
                    .Concat(_allResponsibilityRows.Select(item => item.CurrentHandlingUnit))
                    .Concat(_allOutstandingRows.Select(item => item.CurrentHandlingUnit)),
                _textService.Resolve(TextTokens.ReportsFilterAllUnits)),
            BuildOptionCollection(
                _allInspectionRows.Select(item => item.FaultType)
                    .Concat(_allFaultRows.Select(item => item.FaultType))
                    .Concat(_allDispatchRows.Select(item => item.FaultType))
                    .Concat(_allResponsibilityRows.Select(item => item.FaultType))
                    .Concat(_allOutstandingRows.Select(item => item.FaultType))
                    .Where(item => !string.IsNullOrWhiteSpace(item) && item != _textService.Resolve(TextTokens.InspectionFaultTypeNone)),
                _textService.Resolve(TextTokens.ReportsFilterAllFaultTypes)));
    }

    private ObservableCollection<ReportFilterOptionState> BuildOptionCollection(IEnumerable<string> values, string allLabel)
    {
        var options = new ObservableCollection<ReportFilterOptionState>
        {
            new(AllOptionKey, allLabel)
        };

        foreach (var value in values
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            options.Add(new ReportFilterOptionState(value, value));
        }

        return options;
    }

    private void HandleFilterStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendFilterRefresh)
        {
            return;
        }

        if (e.PropertyName is nameof(ReportFilterState.SelectedTimeRangeOption)
            or nameof(ReportFilterState.SelectedGroupOption)
            or nameof(ReportFilterState.SelectedUnitOption)
            or nameof(ReportFilterState.SelectedFaultTypeOption))
        {
            OnPropertyChanged(nameof(IsCustomRangeSelected));
            ApplyFilters();
        }
    }

    private void ApplyFilters()
    {
        FilteredInspectionRows = new ObservableCollection<InspectionExecutionReportRowState>(_allInspectionRows.Where(MatchesInspectionFilters));
        FilteredFaultRows = new ObservableCollection<FaultStatisticsReportRowState>(_allFaultRows.Where(MatchesFaultFilters));
        FilteredDispatchRows = new ObservableCollection<DispatchDisposalReportRowState>(_allDispatchRows.Where(MatchesDispatchFilters));
        FilteredResponsibilityRows = new ObservableCollection<ResponsibilityOwnershipReportRowState>(_allResponsibilityRows.Where(MatchesResponsibilityFilters));
        FilteredOutstandingRows = new ObservableCollection<OutstandingFaultReportRowState>(_allOutstandingRows.Where(MatchesOutstandingFilters));

        UpdateMetricCards();
        UpdateCharts();
        CurrentRowCountText = string.Format(_textService.Resolve(TextTokens.ReportsTableRowCountPattern), GetCurrentRowCount());
        WorkspaceFeedback = string.Format(
            _textService.Resolve(TextTokens.ReportsFilterFeedbackPattern),
            FilterState.SelectedTimeRangeOption?.Label ?? string.Empty,
            FilterState.SelectedGroupOption?.Label ?? string.Empty,
            FilterState.SelectedUnitOption?.Label ?? string.Empty,
            FilterState.SelectedFaultTypeOption?.Label ?? string.Empty);

        OnPropertyChanged(nameof(HasCurrentRows));
        OnPropertyChanged(nameof(HasNoCurrentRows));
    }

    private void ResetFilters()
    {
        _suspendFilterRefresh = true;

        try
        {
            FilterState.SelectedTimeRangeOption = FilterState.TimeRangeOptions.FirstOrDefault();
            FilterState.SelectedGroupOption = FilterState.GroupOptions.FirstOrDefault();
            FilterState.SelectedUnitOption = FilterState.UnitOptions.FirstOrDefault();
            FilterState.SelectedFaultTypeOption = FilterState.FaultTypeOptions.FirstOrDefault();
        }
        finally
        {
            _suspendFilterRefresh = false;
        }

        OnPropertyChanged(nameof(IsCustomRangeSelected));
        ApplyFilters();
        WorkspaceFeedback = _textService.Resolve(TextTokens.ReportsFilterResetFeedback);
    }

    private void SelectMetric(ReportMetricSummaryState metric)
    {
        SelectReportView(metric.RelatedReportView, metric.Key);
        WorkspaceFeedback = metric.Title;
    }

    private void SelectReportView(ReportViewKind viewKind, string? metricKey = null)
    {
        SelectedReportView = viewKind;

        foreach (var tab in ReportTabs)
        {
            tab.IsSelected = tab.ViewKind == viewKind;
        }

        var selectedMetricKey = metricKey ?? ResolveMetricKey(viewKind);
        foreach (var metric in MetricCards)
        {
            metric.IsSelected = metric.Key == selectedMetricKey;
        }

        CurrentRowCountText = string.Format(_textService.Resolve(TextTokens.ReportsTableRowCountPattern), GetCurrentRowCount());
        OnPropertyChanged(nameof(HasCurrentRows));
        OnPropertyChanged(nameof(HasNoCurrentRows));
    }

    private string ResolveMetricKey(ReportViewKind viewKind)
    {
        return viewKind switch
        {
            ReportViewKind.InspectionExecution => "tasks",
            ReportViewKind.FaultStatistics => "faults",
            ReportViewKind.DispatchDisposal => "dispatched",
            ReportViewKind.ResponsibilityOwnership => "faults",
            ReportViewKind.OutstandingFaults => "unrecovered",
            _ => "tasks"
        };
    }

    private void TriggerExport(ReportExportActionState action)
    {
        foreach (var exportAction in ExportActions)
        {
            exportAction.IsHighlighted = exportAction == action;
        }

        WorkspaceFeedback = action.Key == "excel"
            ? string.Format(_textService.Resolve(TextTokens.ReportsExportExcelFeedbackPattern), GetSelectedTabLabel())
            : string.Format(_textService.Resolve(TextTokens.ReportsExportWordFeedbackPattern), GetSelectedTabLabel());
    }

    private string GetSelectedTabLabel()
        => ReportTabs.FirstOrDefault(item => item.ViewKind == SelectedReportView)?.Label ?? string.Empty;

    private void UpdateMetricCards()
    {
        SetMetricValue("tasks", FilteredInspectionRows.Sum(item => item.DailyTaskRuns), ReportsMetricTaskCountDescription);
        SetMetricValue("points", FilteredInspectionRows.Sum(item => item.TotalPoints), ReportsMetricPointCountDescription);
        SetMetricValue("faults", FilteredFaultRows.Sum(item => item.FaultTotal), ReportsMetricFaultTotalDescription);
        SetMetricValue("dispatched", FilteredDispatchRows.Sum(item => item.DispatchedCount), ReportsMetricDispatchedCountDescription);
        SetMetricValue("unrecovered", FilteredOutstandingRows.Count, ReportsMetricUnrecoveredCountDescription);
        SetMetricValue("recovered", FilteredDispatchRows.Sum(item => item.RecoveredCount), ReportsMetricRecoveredCountDescription);
    }

    private void SetMetricValue(string key, int value, string description)
    {
        var metric = MetricCards.First(item => item.Key == key);
        metric.Value = value.ToString();
        metric.Description = description;
    }

    private void UpdateCharts()
    {
        UpdateFaultTrendChart();
        UpdateFaultTypeChart();
        UpdateDispatchRecoveryChart();
    }

    private void UpdateFaultTrendChart()
    {
        FaultTrendChart.TrendPoints.Clear();
        var grouped = FilteredFaultRows
            .GroupBy(item => item.ReportDate)
            .OrderBy(item => item.Key)
            .Select(group => new
            {
                Label = group.Key.ToString("MM-dd"),
                FaultTotal = group.Sum(item => item.FaultTotal)
            })
            .ToList();

        var maxValue = Math.Max(1, grouped.Select(item => item.FaultTotal).DefaultIfEmpty().Max());
        foreach (var item in grouped)
        {
            FaultTrendChart.TrendPoints.Add(
                new ReportTrendPointState(
                    item.Label,
                    item.FaultTotal.ToString(),
                    Math.Max(22, item.FaultTotal * 132d / maxValue)));
        }

        var average = grouped.Count == 0 ? "0" : Math.Round(grouped.Average(item => item.FaultTotal), 1).ToString("0.0");
        FaultTrendChart.FooterText = string.Format(_textService.Resolve(TextTokens.ReportsChartTrendFooterPattern), average);
    }

    private void UpdateFaultTypeChart()
    {
        FaultTypeChart.DistributionPoints.Clear();
        var grouped = FilteredFaultRows
            .GroupBy(item => item.FaultType)
            .Select(group => new
            {
                Label = group.Key,
                Value = group.Sum(item => item.FaultTotal)
            })
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ToList();

        var total = Math.Max(1, grouped.Sum(item => item.Value));
        var maxValue = Math.Max(1, grouped.Select(item => item.Value).DefaultIfEmpty().Max());

        foreach (var item in grouped)
        {
            FaultTypeChart.DistributionPoints.Add(
                new ReportDistributionPointState(
                    item.Label,
                    item.Value.ToString(),
                    $"{Math.Round(item.Value * 100d / total, 0)}%",
                    Math.Max(60, item.Value * 180d / maxValue)));
        }

        var topLabel = grouped.FirstOrDefault()?.Label ?? "--";
        FaultTypeChart.FooterText = string.Format(_textService.Resolve(TextTokens.ReportsChartTypeFooterPattern), topLabel);
    }

    private void UpdateDispatchRecoveryChart()
    {
        DispatchRecoveryChart.DualTrendPoints.Clear();
        var grouped = FilteredDispatchRows
            .GroupBy(item => item.ReportDate)
            .OrderBy(item => item.Key)
            .Select(group => new
            {
                Label = group.Key.ToString("MM-dd"),
                Dispatch = group.Sum(item => item.DispatchedCount),
                Recovery = group.Sum(item => item.RecoveredCount)
            })
            .ToList();

        var maxValue = Math.Max(1, grouped.Select(item => Math.Max(item.Dispatch, item.Recovery)).DefaultIfEmpty().Max());
        foreach (var item in grouped)
        {
            DispatchRecoveryChart.DualTrendPoints.Add(
                new ReportDualTrendPointState(
                    item.Label,
                    item.Dispatch.ToString(),
                    Math.Max(20, item.Dispatch * 120d / maxValue),
                    item.Recovery.ToString(),
                    Math.Max(20, item.Recovery * 120d / maxValue)));
        }

        DispatchRecoveryChart.FooterText = _textService.Resolve(TextTokens.ReportsChartRecoveryFooterPattern);
    }

    private int GetCurrentRowCount()
    {
        return SelectedReportView switch
        {
            ReportViewKind.InspectionExecution => FilteredInspectionRows.Count,
            ReportViewKind.FaultStatistics => FilteredFaultRows.Count,
            ReportViewKind.DispatchDisposal => FilteredDispatchRows.Count,
            ReportViewKind.ResponsibilityOwnership => FilteredResponsibilityRows.Count,
            ReportViewKind.OutstandingFaults => FilteredOutstandingRows.Count,
            _ => 0
        };
    }

    private bool MatchesInspectionFilters(InspectionExecutionReportRowState row)
    {
        return MatchesDate(row.ReportDate)
            && MatchesOption(FilterState.SelectedGroupOption, row.InspectionGroupName)
            && MatchesOption(FilterState.SelectedUnitOption, row.CurrentHandlingUnit)
            && MatchesFaultOption(row.FaultType);
    }

    private bool MatchesFaultFilters(FaultStatisticsReportRowState row)
    {
        return MatchesDate(row.ReportDate)
            && MatchesOption(FilterState.SelectedGroupOption, row.InspectionGroupName)
            && MatchesOption(FilterState.SelectedUnitOption, row.CurrentHandlingUnit)
            && MatchesFaultOption(row.FaultType);
    }

    private bool MatchesDispatchFilters(DispatchDisposalReportRowState row)
    {
        return MatchesDate(row.ReportDate)
            && MatchesOption(FilterState.SelectedGroupOption, row.InspectionGroupName)
            && MatchesOption(FilterState.SelectedUnitOption, row.CurrentHandlingUnit)
            && MatchesFaultOption(row.FaultType);
    }

    private bool MatchesResponsibilityFilters(ResponsibilityOwnershipReportRowState row)
    {
        return MatchesOption(FilterState.SelectedGroupOption, row.InspectionGroupName)
            && MatchesOption(FilterState.SelectedUnitOption, row.CurrentHandlingUnit)
            && MatchesFaultOption(row.FaultType);
    }

    private bool MatchesOutstandingFilters(OutstandingFaultReportRowState row)
    {
        return MatchesDate(row.ReportDate)
            && MatchesOption(FilterState.SelectedGroupOption, row.InspectionGroupName)
            && MatchesOption(FilterState.SelectedUnitOption, row.CurrentHandlingUnit)
            && MatchesFaultOption(row.FaultType);
    }

    private bool MatchesDate(DateOnly reportDate)
    {
        var timeRange = FilterState.SelectedTimeRangeOption?.Key ?? TimeTodayKey;
        var today = new DateOnly(2026, 3, 12);

        return timeRange switch
        {
            TimeTodayKey => reportDate == today,
            TimeWeekKey => reportDate >= new DateOnly(2026, 3, 9) && reportDate <= today,
            TimeMonthKey => reportDate >= new DateOnly(2026, 3, 1) && reportDate <= today,
            TimeCustomKey => reportDate >= new DateOnly(2026, 3, 5) && reportDate <= today,
            _ => true
        };
    }

    private bool MatchesOption(ReportFilterOptionState? option, string value)
        => option is null || option.Key == AllOptionKey || string.Equals(option.Key, value, StringComparison.Ordinal);

    private bool MatchesFaultOption(string value)
        => MatchesOption(FilterState.SelectedFaultTypeOption, value);

    private List<InspectionExecutionReportRowState> CreateInspectionExecutionRows()
    {
        return
        [
            new(new DateOnly(2026, 3, 12), "2026-03-12", "沿江慢直播保障一组", "沿江运维一中心", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 4, 12, 8, 4, "100%"),
            new(new DateOnly(2026, 3, 12), "2026-03-12", "城区夜景值守二组", "文旅联合中心", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 3, 10, 7, 3, "100%"),
            new(new DateOnly(2026, 3, 11), "2026-03-11", "桥梁联防巡检组", "桥梁联防中心", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), 3, 9, 7, 2, "100%"),
            new(new DateOnly(2026, 3, 10), "2026-03-10", "沿江慢直播保障一组", "沿江运维一中心", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 4, 12, 9, 3, "100%"),
            new(new DateOnly(2026, 3, 9), "2026-03-09", "城区夜景值守二组", "文旅联合中心", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 2, 10, 8, 2, "100%")
        ];
    }

    private List<FaultStatisticsReportRowState> CreateFaultStatisticsRows()
    {
        return
        [
            new(new DateOnly(2026, 3, 12), "2026-03-12", "沿江慢直播保障一组", "沿江运维一中心", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 5, 1, 3, 1, 3, 2),
            new(new DateOnly(2026, 3, 12), "2026-03-12", "城区夜景值守二组", "文旅联合中心", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 4, 0, 1, 3, 2, 2),
            new(new DateOnly(2026, 3, 11), "2026-03-11", "桥梁联防巡检组", "桥梁联防中心", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), 3, 3, 0, 0, 1, 2),
            new(new DateOnly(2026, 3, 10), "2026-03-10", "沿江慢直播保障一组", "沿江运维一中心", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 4, 1, 2, 1, 2, 2),
            new(new DateOnly(2026, 3, 9), "2026-03-09", "城区夜景值守二组", "文旅联合中心", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 2, 0, 0, 2, 1, 1)
        ];
    }

    private List<DispatchDisposalReportRowState> CreateDispatchDisposalRows()
    {
        return
        [
            new(new DateOnly(2026, 3, 12), "2026-03-12", "沿江慢直播保障一组", "沿江运维一中心", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 2, 3, 1, 2, 1, 2),
            new(new DateOnly(2026, 3, 12), "2026-03-12", "城区夜景值守二组", "文旅联合中心", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 1, 2, 2, 1, 0, 2),
            new(new DateOnly(2026, 3, 11), "2026-03-11", "桥梁联防巡检组", "桥梁联防中心", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), 1, 2, 1, 2, 2, 0),
            new(new DateOnly(2026, 3, 10), "2026-03-10", "沿江慢直播保障一组", "沿江运维一中心", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 2, 2, 1, 2, 1, 1),
            new(new DateOnly(2026, 3, 9), "2026-03-09", "城区夜景值守二组", "文旅联合中心", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 1, 1, 1, 1, 0, 1)
        ];
    }

    private List<ResponsibilityOwnershipReportRowState> CreateResponsibilityRows()
    {
        return
        [
            new("沿江慢直播保障一组", "沿江运维一中心", "张衡", "李强", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 5, 2, 3),
            new("城区夜景值守二组", "文旅联合中心", "林悦", "宋晨", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 4, 3, 1),
            new("桥梁联防巡检组", "桥梁联防中心", "周岑", "王征", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), 3, 1, 2),
            new("沿江慢直播保障一组", "沿江运维二中心", "赵宁", "韩征", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), 2, 1, 1),
            new("城区夜景值守二组", "文旅联合中心", "宋野", "高航", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), 2, 1, 1)
        ];
    }

    private List<OutstandingFaultReportRowState> CreateOutstandingRows()
    {
        return
        [
            new(new DateOnly(2026, 3, 12), "沿江慢直播保障一组", "轮渡码头北口", "沿江运维一中心", "张衡", "李强", _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed), "2026-03-12 07:08", "2026-03-12 08:42", _textService.Resolve(TextTokens.DispatchRecoveryUnrecovered)),
            new(new DateOnly(2026, 3, 12), "沿江慢直播保障一组", "防洪泵站外侧", "沿江运维一中心", "周岑", "王征", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), "2026-03-12 06:12", "2026-03-12 07:10", _textService.Resolve(TextTokens.DispatchRecoveryUnrecovered)),
            new(new DateOnly(2026, 3, 11), "桥梁联防巡检组", "江心灯塔监看点", "桥梁联防中心", "陈野", "高航", _textService.Resolve(TextTokens.InspectionFaultTypeOffline), "2026-03-11 22:40", "2026-03-12 06:51", _textService.Resolve(TextTokens.DispatchRecoveryUnrecovered)),
            new(new DateOnly(2026, 3, 9), "城区夜景值守二组", "会展中心北侧广角位", "文旅联合中心", "林悦", "宋晨", _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal), "2026-03-09 19:12", "2026-03-09 22:30", _textService.Resolve(TextTokens.DispatchRecoveryUnrecovered))
        ];
    }
}
