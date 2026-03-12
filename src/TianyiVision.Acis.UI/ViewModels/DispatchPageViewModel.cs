using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed class DispatchPageViewModel : PageViewModelBase
{
    private const string QuickFilterAll = "all";
    private const string QuickFilterPending = "pending";
    private const string QuickFilterDispatched = "dispatched";
    private const string QuickFilterUnrecovered = "unrecovered";
    private const string QuickFilterRecovered = "recovered";
    private const string QuickFilterAutomatic = "automatic";
    private const string QuickFilterManual = "manual";
    private const string QuickFilterTodayNew = "today";
    private const string QuickFilterRepeated = "repeated";
    private const string AllOptionKey = "all";

    private readonly ITextService _textService;
    private readonly List<DispatchWorkOrderDetailState> _allWorkOrders;
    private readonly RelayCommand _dispatchNowCommand;
    private readonly RelayCommand _markRecoveredCommand;
    private readonly RelayCommand _openResponsibilityEditorCommand;
    private readonly RelayCommand _saveResponsibilityCommand;

    private DispatchFilterState _filterState = null!;
    private ObservableCollection<DispatchWorkOrderSummaryState> _filteredWorkOrders = [];
    private DispatchWorkOrderSummaryState? _selectedWorkOrderSummary;
    private DispatchWorkOrderDetailState? _selectedWorkOrderDetail;
    private bool _isResponsibilityEditorOpen;
    private DispatchResponsibilityState? _responsibilityEditor;
    private string _workspaceFeedback = string.Empty;

    public DispatchPageViewModel(ITextService textService)
        : base(
            textService.Resolve(TextTokens.DispatchTitle),
            textService.Resolve(TextTokens.DispatchDescription))
    {
        _textService = textService;
        InitializeText();

        _dispatchNowCommand = new RelayCommand(_ => SimulateDispatchSelected(), _ => SelectedWorkOrderDetail?.WorkOrderStatus == DispatchWorkOrderStatus.PendingDispatch);
        _markRecoveredCommand = new RelayCommand(_ => SimulateRecoverySelected(), _ => SelectedWorkOrderDetail?.RecoveryStatus == DispatchRecoveryStatus.Unrecovered);
        _openResponsibilityEditorCommand = new RelayCommand(_ => OpenResponsibilityEditor(), _ => SelectedWorkOrderDetail is not null);
        _saveResponsibilityCommand = new RelayCommand(_ => SaveResponsibilityEditor(), _ => IsResponsibilityEditorOpen && ResponsibilityEditor is not null && SelectedWorkOrderDetail is not null);

        SelectQuickFilterCommand = new RelayCommand(parameter =>
        {
            if (parameter is DispatchFilterOptionState option)
            {
                SelectQuickFilter(option.Key);
            }
        });
        SelectWorkOrderCommand = new RelayCommand(parameter =>
        {
            if (parameter is DispatchWorkOrderSummaryState summary)
            {
                SelectWorkOrder(summary.WorkOrderId);
            }
        });
        DispatchNowCommand = _dispatchNowCommand;
        MarkRecoveredCommand = _markRecoveredCommand;
        OpenResponsibilityEditorCommand = _openResponsibilityEditorCommand;
        SaveResponsibilityCommand = _saveResponsibilityCommand;
        CancelResponsibilityCommand = new RelayCommand(_ => CloseResponsibilityEditor());
        NavigateToDispatchCommand = new RelayCommand(_ => RequestNavigate(AppSectionId.Dispatch));

        _allWorkOrders = CreateFakeWorkOrders();
        FilterState = CreateFilterState();
        WorkspaceFeedback = MergeDescription;
        ApplyFilters();
    }

    public string FilterTitle { get; private set; } = string.Empty;
    public string FilterDescription { get; private set; } = string.Empty;
    public string ListTitle { get; private set; } = string.Empty;
    public string ListDescription { get; private set; } = string.Empty;
    public string MergeTitle { get; private set; } = string.Empty;
    public string MergeDescription { get; private set; } = string.Empty;
    public string DetailTitle { get; private set; } = string.Empty;
    public string DetailDescription { get; private set; } = string.Empty;
    public string EvidenceTitle { get; private set; } = string.Empty;
    public string EvidenceDescription { get; private set; } = string.Empty;
    public string FilterGroupLabel { get; private set; } = string.Empty;
    public string FilterUnitLabel { get; private set; } = string.Empty;
    public string FilterMaintainerLabel { get; private set; } = string.Empty;
    public string FilterSupervisorLabel { get; private set; } = string.Empty;
    public string FilterFaultTypeLabel { get; private set; } = string.Empty;
    public string PointNameColumnLabel { get; private set; } = string.Empty;
    public string FaultTypeColumnLabel { get; private set; } = string.Empty;
    public string HandlingUnitColumnLabel { get; private set; } = string.Empty;
    public string MaintainerColumnLabel { get; private set; } = string.Empty;
    public string SupervisorColumnLabel { get; private set; } = string.Empty;
    public string LatestFaultTimeColumnLabel { get; private set; } = string.Empty;
    public string WorkOrderStatusColumnLabel { get; private set; } = string.Empty;
    public string RecoveryStatusColumnLabel { get; private set; } = string.Empty;
    public string DispatchMethodColumnLabel { get; private set; } = string.Empty;
    public string RepeatCountColumnLabel { get; private set; } = string.Empty;
    public string DetailBasicTitle { get; private set; } = string.Empty;
    public string DetailResponsibilityTitle { get; private set; } = string.Empty;
    public string DetailNotificationTitle { get; private set; } = string.Empty;
    public string DetailEvidenceTitle { get; private set; } = string.Empty;
    public string DetailPointNameLabel { get; private set; } = string.Empty;
    public string DetailFaultTypeLabel { get; private set; } = string.Empty;
    public string DetailInspectionGroupLabel { get; private set; } = string.Empty;
    public string DetailFirstFaultTimeLabel { get; private set; } = string.Empty;
    public string DetailLatestFaultTimeLabel { get; private set; } = string.Empty;
    public string DetailRepeatCountLabel { get; private set; } = string.Empty;
    public string DetailMapLocationLabel { get; private set; } = string.Empty;
    public string DetailCurrentHandlingUnitLabel { get; private set; } = string.Empty;
    public string DetailMaintainerLabel { get; private set; } = string.Empty;
    public string DetailMaintainerPhoneLabel { get; private set; } = string.Empty;
    public string DetailSupervisorLabel { get; private set; } = string.Empty;
    public string DetailSupervisorPhoneLabel { get; private set; } = string.Empty;
    public string DetailFaultNotificationTimeLabel { get; private set; } = string.Empty;
    public string DetailFaultNotificationStatusLabel { get; private set; } = string.Empty;
    public string DetailRecoveryNotificationTimeLabel { get; private set; } = string.Empty;
    public string DetailRecoveryNotificationStatusLabel { get; private set; } = string.Empty;
    public string DetailScreenshotLabel { get; private set; } = string.Empty;
    public string DetailFaultSummaryLabel { get; private set; } = string.Empty;
    public string DetailLatestInspectionConclusionLabel { get; private set; } = string.Empty;
    public string DetailDispatchPoolEntryLabel { get; private set; } = string.Empty;
    public string DispatchNowText { get; private set; } = string.Empty;
    public string MarkRecoveredText { get; private set; } = string.Empty;
    public string EditResponsibilityText { get; private set; } = string.Empty;
    public string SaveResponsibilityText { get; private set; } = string.Empty;
    public string CancelResponsibilityText { get; private set; } = string.Empty;
    public string TimelineTitle { get; private set; } = string.Empty;
    public string ResponsibilityEditorTitle { get; private set; } = string.Empty;
    public string ResponsibilityEditorDescription { get; private set; } = string.Empty;
    public string EmptyStateTitle { get; private set; } = string.Empty;
    public string EmptyStateDescription { get; private set; } = string.Empty;
    public string DispatchQuickFilterTodayNewText { get; private set; } = string.Empty;

    public DispatchFilterState FilterState
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

    public ObservableCollection<DispatchWorkOrderSummaryState> FilteredWorkOrders
    {
        get => _filteredWorkOrders;
        private set
        {
            if (SetProperty(ref _filteredWorkOrders, value))
            {
                OnPropertyChanged(nameof(HasWorkOrders));
                OnPropertyChanged(nameof(HasNoWorkOrders));
            }
        }
    }

    public DispatchWorkOrderSummaryState? SelectedWorkOrderSummary
    {
        get => _selectedWorkOrderSummary;
        private set => SetProperty(ref _selectedWorkOrderSummary, value);
    }

    public DispatchWorkOrderDetailState? SelectedWorkOrderDetail
    {
        get => _selectedWorkOrderDetail;
        private set
        {
            if (SetProperty(ref _selectedWorkOrderDetail, value))
            {
                OnPropertyChanged(nameof(HasSelectedWorkOrder));
                OnPropertyChanged(nameof(HasNoSelectedWorkOrder));
                _dispatchNowCommand.RaiseCanExecuteChanged();
                _markRecoveredCommand.RaiseCanExecuteChanged();
                _openResponsibilityEditorCommand.RaiseCanExecuteChanged();
                _saveResponsibilityCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsResponsibilityEditorOpen
    {
        get => _isResponsibilityEditorOpen;
        private set
        {
            if (SetProperty(ref _isResponsibilityEditorOpen, value))
            {
                _saveResponsibilityCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DispatchResponsibilityState? ResponsibilityEditor
    {
        get => _responsibilityEditor;
        private set
        {
            if (SetProperty(ref _responsibilityEditor, value))
            {
                _saveResponsibilityCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string WorkspaceFeedback
    {
        get => _workspaceFeedback;
        private set => SetProperty(ref _workspaceFeedback, value);
    }

    public bool HasSelectedWorkOrder => SelectedWorkOrderDetail is not null;
    public bool HasNoSelectedWorkOrder => !HasSelectedWorkOrder;
    public bool HasWorkOrders => FilteredWorkOrders.Count > 0;
    public bool HasNoWorkOrders => !HasWorkOrders;

    public ICommand SelectQuickFilterCommand { get; }
    public ICommand SelectWorkOrderCommand { get; }
    public ICommand DispatchNowCommand { get; }
    public ICommand MarkRecoveredCommand { get; }
    public ICommand OpenResponsibilityEditorCommand { get; }
    public ICommand SaveResponsibilityCommand { get; }
    public ICommand CancelResponsibilityCommand { get; }
    public ICommand NavigateToDispatchCommand { get; }

    private void InitializeText()
    {
        FilterTitle = _textService.Resolve(TextTokens.DispatchFilterTitle);
        FilterDescription = _textService.Resolve(TextTokens.DispatchFilterDescription);
        ListTitle = _textService.Resolve(TextTokens.DispatchListTitle);
        ListDescription = _textService.Resolve(TextTokens.DispatchListDescription);
        MergeTitle = _textService.Resolve(TextTokens.DispatchMergeTitle);
        MergeDescription = _textService.Resolve(TextTokens.DispatchMergeDescription);
        DetailTitle = _textService.Resolve(TextTokens.DispatchDetailTitle);
        DetailDescription = _textService.Resolve(TextTokens.DispatchDetailDescription);
        EvidenceTitle = _textService.Resolve(TextTokens.DispatchEvidenceTitle);
        EvidenceDescription = _textService.Resolve(TextTokens.DispatchEvidenceDescription);
        FilterGroupLabel = _textService.Resolve(TextTokens.DispatchFilterGroupLabel);
        FilterUnitLabel = _textService.Resolve(TextTokens.DispatchFilterUnitLabel);
        FilterMaintainerLabel = _textService.Resolve(TextTokens.DispatchFilterMaintainerLabel);
        FilterSupervisorLabel = _textService.Resolve(TextTokens.DispatchFilterSupervisorLabel);
        FilterFaultTypeLabel = _textService.Resolve(TextTokens.DispatchFilterFaultTypeLabel);
        PointNameColumnLabel = _textService.Resolve(TextTokens.DispatchListPointNameLabel);
        FaultTypeColumnLabel = _textService.Resolve(TextTokens.DispatchListFaultTypeLabel);
        HandlingUnitColumnLabel = _textService.Resolve(TextTokens.DispatchListHandlingUnitLabel);
        MaintainerColumnLabel = _textService.Resolve(TextTokens.DispatchListMaintainerLabel);
        SupervisorColumnLabel = _textService.Resolve(TextTokens.DispatchListSupervisorLabel);
        LatestFaultTimeColumnLabel = _textService.Resolve(TextTokens.DispatchListLatestFaultTimeLabel);
        WorkOrderStatusColumnLabel = _textService.Resolve(TextTokens.DispatchListWorkOrderStatusLabel);
        RecoveryStatusColumnLabel = _textService.Resolve(TextTokens.DispatchListRecoveryStatusLabel);
        DispatchMethodColumnLabel = _textService.Resolve(TextTokens.DispatchListDispatchMethodLabel);
        RepeatCountColumnLabel = _textService.Resolve(TextTokens.DispatchListRepeatCountLabel);
        DetailBasicTitle = _textService.Resolve(TextTokens.DispatchDetailBasicTitle);
        DetailResponsibilityTitle = _textService.Resolve(TextTokens.DispatchDetailResponsibilityTitle);
        DetailNotificationTitle = _textService.Resolve(TextTokens.DispatchDetailNotificationTitle);
        DetailEvidenceTitle = _textService.Resolve(TextTokens.DispatchDetailEvidenceTitle);
        DetailPointNameLabel = _textService.Resolve(TextTokens.DispatchDetailPointNameLabel);
        DetailFaultTypeLabel = _textService.Resolve(TextTokens.DispatchDetailFaultTypeLabel);
        DetailInspectionGroupLabel = _textService.Resolve(TextTokens.DispatchDetailInspectionGroupLabel);
        DetailFirstFaultTimeLabel = _textService.Resolve(TextTokens.DispatchDetailFirstFaultTimeLabel);
        DetailLatestFaultTimeLabel = _textService.Resolve(TextTokens.DispatchDetailLatestFaultTimeLabel);
        DetailRepeatCountLabel = _textService.Resolve(TextTokens.DispatchDetailRepeatCountLabel);
        DetailMapLocationLabel = _textService.Resolve(TextTokens.DispatchDetailMapLocationLabel);
        DetailCurrentHandlingUnitLabel = _textService.Resolve(TextTokens.DispatchDetailCurrentHandlingUnitLabel);
        DetailMaintainerLabel = _textService.Resolve(TextTokens.DispatchDetailMaintainerLabel);
        DetailMaintainerPhoneLabel = _textService.Resolve(TextTokens.DispatchDetailMaintainerPhoneLabel);
        DetailSupervisorLabel = _textService.Resolve(TextTokens.DispatchDetailSupervisorLabel);
        DetailSupervisorPhoneLabel = _textService.Resolve(TextTokens.DispatchDetailSupervisorPhoneLabel);
        DetailFaultNotificationTimeLabel = _textService.Resolve(TextTokens.DispatchDetailFaultNotificationTimeLabel);
        DetailFaultNotificationStatusLabel = _textService.Resolve(TextTokens.DispatchDetailFaultNotificationStatusLabel);
        DetailRecoveryNotificationTimeLabel = _textService.Resolve(TextTokens.DispatchDetailRecoveryNotificationTimeLabel);
        DetailRecoveryNotificationStatusLabel = _textService.Resolve(TextTokens.DispatchDetailRecoveryNotificationStatusLabel);
        DetailScreenshotLabel = _textService.Resolve(TextTokens.DispatchDetailScreenshotLabel);
        DetailFaultSummaryLabel = _textService.Resolve(TextTokens.DispatchDetailFaultSummaryLabel);
        DetailLatestInspectionConclusionLabel = _textService.Resolve(TextTokens.DispatchDetailLatestInspectionConclusionLabel);
        DetailDispatchPoolEntryLabel = _textService.Resolve(TextTokens.DispatchDetailDispatchPoolEntryLabel);
        DispatchNowText = _textService.Resolve(TextTokens.DispatchActionDispatchNow);
        MarkRecoveredText = _textService.Resolve(TextTokens.DispatchActionMarkRecovered);
        EditResponsibilityText = _textService.Resolve(TextTokens.DispatchActionEditResponsibility);
        SaveResponsibilityText = _textService.Resolve(TextTokens.DispatchActionSaveResponsibility);
        CancelResponsibilityText = _textService.Resolve(TextTokens.DispatchActionCancelResponsibility);
        TimelineTitle = _textService.Resolve(TextTokens.DispatchTimelineTitle);
        ResponsibilityEditorTitle = _textService.Resolve(TextTokens.DispatchEditorTitle);
        ResponsibilityEditorDescription = _textService.Resolve(TextTokens.DispatchEditorDescription);
        EmptyStateTitle = _textService.Resolve(TextTokens.DispatchEmptyStateTitle);
        EmptyStateDescription = _textService.Resolve(TextTokens.DispatchEmptyStateDescription);
        DispatchQuickFilterTodayNewText = _textService.Resolve(TextTokens.DispatchQuickFilterTodayNew);
    }

    private void HandleFilterStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DispatchFilterState.SelectedQuickFilterKey)
            or nameof(DispatchFilterState.SelectedGroupOption)
            or nameof(DispatchFilterState.SelectedUnitOption)
            or nameof(DispatchFilterState.SelectedMaintainerOption)
            or nameof(DispatchFilterState.SelectedSupervisorOption)
            or nameof(DispatchFilterState.SelectedFaultTypeOption))
        {
            ApplyFilters(SelectedWorkOrderDetail?.WorkOrderId);
        }
    }

    private DispatchFilterState CreateFilterState()
    {
        var quickFilters = new ObservableCollection<DispatchFilterOptionState>(
        [
            CreateQuickFilterOption(QuickFilterAll, _textService.Resolve(TextTokens.DispatchQuickFilterAll), true),
            CreateQuickFilterOption(QuickFilterPending, _textService.Resolve(TextTokens.DispatchQuickFilterPending)),
            CreateQuickFilterOption(QuickFilterDispatched, _textService.Resolve(TextTokens.DispatchQuickFilterDispatched)),
            CreateQuickFilterOption(QuickFilterUnrecovered, _textService.Resolve(TextTokens.DispatchQuickFilterUnrecovered)),
            CreateQuickFilterOption(QuickFilterRecovered, _textService.Resolve(TextTokens.DispatchQuickFilterRecovered)),
            CreateQuickFilterOption(QuickFilterAutomatic, _textService.Resolve(TextTokens.DispatchQuickFilterAutomatic)),
            CreateQuickFilterOption(QuickFilterManual, _textService.Resolve(TextTokens.DispatchQuickFilterManual)),
            CreateQuickFilterOption(QuickFilterTodayNew, _textService.Resolve(TextTokens.DispatchQuickFilterTodayNew)),
            CreateQuickFilterOption(QuickFilterRepeated, _textService.Resolve(TextTokens.DispatchQuickFilterRepeated))
        ]);

        return new DispatchFilterState(
            quickFilters,
            BuildOptionCollection(_allWorkOrders.Select(item => item.InspectionGroupName), _textService.Resolve(TextTokens.DispatchFilterAllGroups)),
            BuildOptionCollection(_allWorkOrders.Select(item => item.Responsibility.CurrentHandlingUnit), _textService.Resolve(TextTokens.DispatchFilterAllUnits)),
            BuildOptionCollection(_allWorkOrders.Select(item => item.Responsibility.MaintainerName), _textService.Resolve(TextTokens.DispatchFilterAllMaintainers)),
            BuildOptionCollection(_allWorkOrders.Select(item => item.Responsibility.SupervisorName), _textService.Resolve(TextTokens.DispatchFilterAllSupervisors)),
            BuildOptionCollection(_allWorkOrders.Select(item => item.FaultType), _textService.Resolve(TextTokens.DispatchFilterAllFaultTypes)),
            QuickFilterAll);
    }

    private static DispatchFilterOptionState CreateQuickFilterOption(string key, string label, bool isSelected = false)
        => new(key, label) { IsSelected = isSelected };

    private static ObservableCollection<DispatchFilterOptionState> BuildOptionCollection(IEnumerable<string> values, string allLabel)
    {
        var options = new ObservableCollection<DispatchFilterOptionState>
        {
            new(AllOptionKey, allLabel)
        };

        foreach (var value in values.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
        {
            options.Add(new DispatchFilterOptionState(value, value));
        }

        return options;
    }

    private void SelectQuickFilter(string filterKey)
    {
        foreach (var option in FilterState.QuickFilters)
        {
            option.IsSelected = option.Key == filterKey;
        }

        FilterState.SelectedQuickFilterKey = filterKey;
    }

    private void ApplyFilters(string? preferredWorkOrderId = null)
    {
        var filtered = _allWorkOrders
            .Where(MatchesFilter)
            .OrderBy(item => item.WorkOrderStatus == DispatchWorkOrderStatus.PendingDispatch ? 0 : 1)
            .ThenBy(item => item.RecoveryStatus == DispatchRecoveryStatus.Unrecovered ? 0 : 1)
            .ThenByDescending(item => item.RepeatFault.RepeatCount)
            .ThenByDescending(item => item.RepeatFault.LatestFaultTime, StringComparer.Ordinal)
            .ToList();

        FilteredWorkOrders = new ObservableCollection<DispatchWorkOrderSummaryState>(filtered.Select(CreateSummary));

        var targetWorkOrderId = preferredWorkOrderId;
        if (targetWorkOrderId is null || FilteredWorkOrders.All(item => item.WorkOrderId != targetWorkOrderId))
        {
            targetWorkOrderId = FilteredWorkOrders.FirstOrDefault()?.WorkOrderId;
        }

        SelectWorkOrder(targetWorkOrderId, fromFilterRefresh: true);
        WorkspaceFeedback = FilteredWorkOrders.Count == 0 ? EmptyStateDescription : MergeDescription;
    }

    private bool MatchesFilter(DispatchWorkOrderDetailState workOrder)
    {
        return MatchesQuickFilter(workOrder)
            && MatchesOption(FilterState.SelectedGroupOption, workOrder.InspectionGroupName)
            && MatchesOption(FilterState.SelectedUnitOption, workOrder.Responsibility.CurrentHandlingUnit)
            && MatchesOption(FilterState.SelectedMaintainerOption, workOrder.Responsibility.MaintainerName)
            && MatchesOption(FilterState.SelectedSupervisorOption, workOrder.Responsibility.SupervisorName)
            && MatchesOption(FilterState.SelectedFaultTypeOption, workOrder.FaultType);
    }

    private bool MatchesQuickFilter(DispatchWorkOrderDetailState workOrder)
    {
        return FilterState.SelectedQuickFilterKey switch
        {
            QuickFilterPending => workOrder.WorkOrderStatus == DispatchWorkOrderStatus.PendingDispatch,
            QuickFilterDispatched => workOrder.WorkOrderStatus == DispatchWorkOrderStatus.Dispatched,
            QuickFilterUnrecovered => workOrder.RecoveryStatus == DispatchRecoveryStatus.Unrecovered,
            QuickFilterRecovered => workOrder.RecoveryStatus == DispatchRecoveryStatus.Recovered,
            QuickFilterAutomatic => workOrder.DispatchMethod == DispatchMethod.Automatic,
            QuickFilterManual => workOrder.DispatchMethod == DispatchMethod.Manual,
            QuickFilterTodayNew => workOrder.IsTodayNew,
            QuickFilterRepeated => workOrder.IsRepeated,
            _ => true
        };
    }

    private static bool MatchesOption(DispatchFilterOptionState? option, string value)
        => option is null || option.Key == AllOptionKey || string.Equals(option.Key, value, StringComparison.Ordinal);

    private void SelectWorkOrder(string? workOrderId, bool fromFilterRefresh = false)
    {
        foreach (var summary in FilteredWorkOrders)
        {
            summary.IsSelected = summary.WorkOrderId == workOrderId;
        }

        foreach (var detail in _allWorkOrders)
        {
            detail.IsSelected = detail.WorkOrderId == workOrderId;
        }

        SelectedWorkOrderSummary = FilteredWorkOrders.FirstOrDefault(item => item.WorkOrderId == workOrderId);
        SelectedWorkOrderDetail = _allWorkOrders.FirstOrDefault(item => item.WorkOrderId == workOrderId);

        if (!fromFilterRefresh)
        {
            CloseResponsibilityEditor();
        }
    }

    private static DispatchWorkOrderSummaryState CreateSummary(DispatchWorkOrderDetailState detail)
        => new(
            detail.WorkOrderId,
            detail.PointName,
            detail.FaultType,
            detail.Responsibility.CurrentHandlingUnit,
            detail.Responsibility.MaintainerName,
            detail.Responsibility.SupervisorName,
            detail.RepeatFault.LatestFaultTime,
            detail.WorkOrderStatus,
            detail.WorkOrderStatusText,
            detail.RecoveryStatus,
            detail.RecoveryStatusText,
            detail.DispatchMethod,
            detail.DispatchMethodText,
            detail.RepeatFault.RepeatCount,
            detail.RepeatFault.RepeatSummary,
            detail.IsTodayNew);

    private void SimulateDispatchSelected()
    {
        if (SelectedWorkOrderDetail is null || SelectedWorkOrderDetail.WorkOrderStatus != DispatchWorkOrderStatus.PendingDispatch)
        {
            return;
        }

        var sentAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        SelectedWorkOrderDetail.WorkOrderStatus = DispatchWorkOrderStatus.Dispatched;
        SelectedWorkOrderDetail.WorkOrderStatusText = _textService.Resolve(TextTokens.DispatchWorkOrderDispatched);
        SelectedWorkOrderDetail.NotificationRecord.FaultNotificationSentAt = sentAt;
        SelectedWorkOrderDetail.NotificationRecord.FaultNotificationStatus = _textService.Resolve(TextTokens.DispatchNotificationSimulated);
        SelectedWorkOrderDetail.NotificationRecord.TimelineEntries.Insert(
            0,
            new DispatchNotificationEntryState(
                _textService.Resolve(TextTokens.DispatchNotificationFaultTitle),
                sentAt,
                _textService.Resolve(TextTokens.DispatchNotificationSimulated),
                $"{SelectedWorkOrderDetail.Responsibility.CurrentHandlingUnit} / {SelectedWorkOrderDetail.Responsibility.MaintainerName}"));

        WorkspaceFeedback = string.Format(
            _textService.Resolve(TextTokens.DispatchDispatchFeedbackPattern),
            SelectedWorkOrderDetail.PointName);

        ApplyFilters(SelectedWorkOrderDetail.WorkOrderId);
    }

    private void SimulateRecoverySelected()
    {
        if (SelectedWorkOrderDetail is null || SelectedWorkOrderDetail.RecoveryStatus != DispatchRecoveryStatus.Unrecovered)
        {
            return;
        }

        var sentAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        SelectedWorkOrderDetail.RecoveryStatus = DispatchRecoveryStatus.Recovered;
        SelectedWorkOrderDetail.RecoveryStatusText = _textService.Resolve(TextTokens.DispatchRecoveryRecovered);
        SelectedWorkOrderDetail.NotificationRecord.RecoveryNotificationSentAt = sentAt;
        SelectedWorkOrderDetail.NotificationRecord.RecoveryNotificationStatus = _textService.Resolve(TextTokens.DispatchNotificationSimulated);
        SelectedWorkOrderDetail.NotificationRecord.TimelineEntries.Insert(
            0,
            new DispatchNotificationEntryState(
                _textService.Resolve(TextTokens.DispatchNotificationRecoveryTitle),
                sentAt,
                _textService.Resolve(TextTokens.DispatchNotificationSimulated),
                $"{SelectedWorkOrderDetail.Responsibility.CurrentHandlingUnit} / {SelectedWorkOrderDetail.Responsibility.MaintainerName}"));

        WorkspaceFeedback = string.Format(
            _textService.Resolve(TextTokens.DispatchRecoveryFeedbackPattern),
            SelectedWorkOrderDetail.PointName);

        ApplyFilters(SelectedWorkOrderDetail.WorkOrderId);
    }

    private void OpenResponsibilityEditor()
    {
        if (SelectedWorkOrderDetail is null)
        {
            return;
        }

        ResponsibilityEditor = SelectedWorkOrderDetail.Responsibility.Clone();
        IsResponsibilityEditorOpen = true;
    }

    private void SaveResponsibilityEditor()
    {
        if (SelectedWorkOrderDetail is null || ResponsibilityEditor is null)
        {
            return;
        }

        SelectedWorkOrderDetail.Responsibility.CurrentHandlingUnit = ResponsibilityEditor.CurrentHandlingUnit;
        SelectedWorkOrderDetail.Responsibility.MaintainerName = ResponsibilityEditor.MaintainerName;
        SelectedWorkOrderDetail.Responsibility.MaintainerPhone = ResponsibilityEditor.MaintainerPhone;
        SelectedWorkOrderDetail.Responsibility.SupervisorName = ResponsibilityEditor.SupervisorName;
        SelectedWorkOrderDetail.Responsibility.SupervisorPhone = ResponsibilityEditor.SupervisorPhone;

        WorkspaceFeedback = string.Format(
            _textService.Resolve(TextTokens.DispatchResponsibilityFeedbackPattern),
            SelectedWorkOrderDetail.PointName);

        CloseResponsibilityEditor();
        ApplyFilters(SelectedWorkOrderDetail.WorkOrderId);
    }

    private void CloseResponsibilityEditor()
    {
        IsResponsibilityEditorOpen = false;
        ResponsibilityEditor = null;
    }

    private List<DispatchWorkOrderDetailState> CreateFakeWorkOrders()
    {
        return
        [
            CreateWorkOrder(
                "dispatch-001",
                "point-102",
                "轮渡码头北口",
                _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed),
                "沿江慢直播保障一组",
                "沿江片区 / 轮渡码头北口",
                "故障截图占位 A",
                "播放失败告警快照",
                "设备在线但视频播放失败，本轮已按重复故障合并处理。",
                _textService.Resolve(TextTokens.InspectionConclusionFault),
                entersDispatchPool: true,
                isTodayNew: true,
                DispatchMethod.Manual,
                DispatchWorkOrderStatus.PendingDispatch,
                DispatchRecoveryStatus.Unrecovered,
                new DispatchResponsibilityState("沿江运维一中心", "张磊", "13800000001", "李强", "13900000001"),
                firstFaultTime: "2026-03-12 07:08",
                latestFaultTime: "2026-03-12 08:42",
                repeatCount: 3,
                faultNotificationSentAt: "--",
                faultNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationPending),
                recoveryNotificationSentAt: "--",
                recoveryNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationPending)),
            CreateWorkOrder(
                "dispatch-002",
                "point-105",
                "防洪泵站外侧",
                _textService.Resolve(TextTokens.InspectionFaultTypeOffline),
                "沿江慢直播保障一组",
                "泵站外圈 / 西北角",
                "故障截图占位 B",
                "离线告警快照",
                "点位离线且恢复前暂停巡检，已模拟自动派单。",
                _textService.Resolve(TextTokens.InspectionConclusionFault),
                entersDispatchPool: true,
                isTodayNew: true,
                DispatchMethod.Automatic,
                DispatchWorkOrderStatus.Dispatched,
                DispatchRecoveryStatus.Unrecovered,
                new DispatchResponsibilityState("防汛保障中心", "周岚", "13800000012", "王征", "13900000012"),
                firstFaultTime: "2026-03-12 06:12",
                latestFaultTime: "2026-03-12 07:10",
                repeatCount: 2,
                faultNotificationSentAt: "2026-03-12 07:12",
                faultNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationSimulated),
                recoveryNotificationSentAt: "--",
                recoveryNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationPending)),
            CreateWorkOrder(
                "dispatch-003",
                "point-106",
                "江心灯塔监看点",
                _textService.Resolve(TextTokens.InspectionFaultTypeOffline),
                "夜景值守组",
                "江心灯塔 / 东南水域",
                "故障截图占位 C",
                "夜景离线快照",
                "夜间值守点位离线，当前仍在未恢复阶段。",
                _textService.Resolve(TextTokens.InspectionConclusionFault),
                entersDispatchPool: true,
                isTodayNew: false,
                DispatchMethod.Automatic,
                DispatchWorkOrderStatus.Dispatched,
                DispatchRecoveryStatus.Unrecovered,
                new DispatchResponsibilityState("航道监护中心", "陈野", "13800000023", "高航", "13900000023"),
                firstFaultTime: "2026-03-11 22:40",
                latestFaultTime: "2026-03-12 06:51",
                repeatCount: 4,
                faultNotificationSentAt: "2026-03-11 22:42",
                faultNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationSimulated),
                recoveryNotificationSentAt: "--",
                recoveryNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationPending)),
            CreateWorkOrder(
                "dispatch-004",
                "point-104",
                "城市阳台主广场",
                _textService.Resolve(TextTokens.InspectionFaultTypeImageAbnormal),
                "文旅值守组",
                "城市阳台 / 主广场上空",
                "故障截图占位 D",
                "画面异常快照",
                "重点点位画面异常，已进入人工派单跟踪。",
                _textService.Resolve(TextTokens.InspectionConclusionFault),
                entersDispatchPool: true,
                isTodayNew: true,
                DispatchMethod.Manual,
                DispatchWorkOrderStatus.Dispatched,
                DispatchRecoveryStatus.Recovered,
                new DispatchResponsibilityState("文旅联合中心", "林悦", "13800000034", "宋晨", "13900000034"),
                firstFaultTime: "2026-03-12 07:28",
                latestFaultTime: "2026-03-12 07:54",
                repeatCount: 1,
                faultNotificationSentAt: "2026-03-12 07:56",
                faultNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationSimulated),
                recoveryNotificationSentAt: "2026-03-12 09:10",
                recoveryNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationSimulated)),
            CreateWorkOrder(
                "dispatch-005",
                "point-109",
                "滨江步道南入口",
                _textService.Resolve(TextTokens.InspectionFaultTypePlaybackFailed),
                "沿江慢直播保障二组",
                "滨江步道 / 南入口立杆",
                "故障截图占位 E",
                "南入口播放失败快照",
                "新进入派单池的人工派单工单，等待管理员确认发送。",
                _textService.Resolve(TextTokens.InspectionConclusionFault),
                entersDispatchPool: true,
                isTodayNew: true,
                DispatchMethod.Manual,
                DispatchWorkOrderStatus.PendingDispatch,
                DispatchRecoveryStatus.Unrecovered,
                new DispatchResponsibilityState("沿江运维二中心", "赵宁", "13800000045", "韩征", "13900000045"),
                firstFaultTime: "2026-03-12 10:16",
                latestFaultTime: "2026-03-12 10:16",
                repeatCount: 1,
                faultNotificationSentAt: "--",
                faultNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationPending),
                recoveryNotificationSentAt: "--",
                recoveryNotificationStatus: _textService.Resolve(TextTokens.DispatchNotificationPending))
        ];
    }

    private DispatchWorkOrderDetailState CreateWorkOrder(
        string workOrderId,
        string pointId,
        string pointName,
        string faultType,
        string inspectionGroupName,
        string mapLocationPlaceholder,
        string screenshotTitle,
        string screenshotSubtitle,
        string faultSummary,
        string latestInspectionConclusion,
        bool entersDispatchPool,
        bool isTodayNew,
        DispatchMethod dispatchMethod,
        DispatchWorkOrderStatus workOrderStatus,
        DispatchRecoveryStatus recoveryStatus,
        DispatchResponsibilityState responsibility,
        string firstFaultTime,
        string latestFaultTime,
        int repeatCount,
        string faultNotificationSentAt,
        string faultNotificationStatus,
        string recoveryNotificationSentAt,
        string recoveryNotificationStatus)
    {
        var repeatSummary = string.Format(_textService.Resolve(TextTokens.DispatchRepeatSummaryPattern), repeatCount);
        var entries = new ObservableCollection<DispatchNotificationEntryState>();

        if (faultNotificationSentAt != "--")
        {
            entries.Add(new DispatchNotificationEntryState(
                _textService.Resolve(TextTokens.DispatchNotificationFaultTitle),
                faultNotificationSentAt,
                faultNotificationStatus,
                $"{responsibility.CurrentHandlingUnit} / {responsibility.MaintainerName}"));
        }

        if (recoveryNotificationSentAt != "--")
        {
            entries.Insert(
                0,
                new DispatchNotificationEntryState(
                    _textService.Resolve(TextTokens.DispatchNotificationRecoveryTitle),
                    recoveryNotificationSentAt,
                    recoveryNotificationStatus,
                    $"{responsibility.CurrentHandlingUnit} / {responsibility.MaintainerName}"));
        }

        return new DispatchWorkOrderDetailState(
            workOrderId,
            pointId,
            pointName,
            faultType,
            inspectionGroupName,
            mapLocationPlaceholder,
            screenshotTitle,
            screenshotSubtitle,
            faultSummary,
            latestInspectionConclusion,
            entersDispatchPool,
            entersDispatchPool ? _textService.Resolve(TextTokens.InspectionDispatchPoolYes) : _textService.Resolve(TextTokens.InspectionDispatchPoolNo),
            isTodayNew,
            dispatchMethod,
            dispatchMethod == DispatchMethod.Automatic
                ? _textService.Resolve(TextTokens.InspectionDispatchModeAuto)
                : _textService.Resolve(TextTokens.InspectionDispatchModeManual),
            workOrderStatus,
            workOrderStatus == DispatchWorkOrderStatus.PendingDispatch
                ? _textService.Resolve(TextTokens.DispatchWorkOrderPending)
                : _textService.Resolve(TextTokens.DispatchWorkOrderDispatched),
            recoveryStatus,
            recoveryStatus == DispatchRecoveryStatus.Unrecovered
                ? _textService.Resolve(TextTokens.DispatchRecoveryUnrecovered)
                : _textService.Resolve(TextTokens.DispatchRecoveryRecovered),
            responsibility,
            new DispatchNotificationRecordState(
                faultNotificationSentAt,
                faultNotificationStatus,
                recoveryNotificationSentAt,
                recoveryNotificationStatus,
                entries),
            new DispatchRepeatFaultState(firstFaultTime, latestFaultTime, repeatCount, repeatSummary));
    }
}
