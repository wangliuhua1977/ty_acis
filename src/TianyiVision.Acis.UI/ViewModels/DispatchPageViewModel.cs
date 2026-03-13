using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TianyiVision.Acis.Core.Application;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Dispatch;
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

    private readonly IDispatchNotificationService _dispatchNotificationService;
    private readonly IDispatchResponsibilityService _dispatchResponsibilityService;
    private readonly ITextService _textService;
    private readonly List<DispatchWorkOrderDetailState> _allWorkOrders;
    private readonly RelayCommand _dispatchNowCommand;
    private readonly RelayCommand _markRecoveredCommand;
    private readonly RelayCommand _openResponsibilityEditorCommand;
    private readonly RelayCommand _openReportsCenterCommand;
    private readonly RelayCommand _saveResponsibilityCommand;

    private DispatchFilterState _filterState = null!;
    private ObservableCollection<DispatchWorkOrderSummaryState> _filteredWorkOrders = [];
    private DispatchWorkOrderSummaryState? _selectedWorkOrderSummary;
    private DispatchWorkOrderDetailState? _selectedWorkOrderDetail;
    private bool _isResponsibilityEditorOpen;
    private DispatchResponsibilityState? _responsibilityEditor;
    private string _workspaceFeedback = string.Empty;

    public DispatchPageViewModel(
        ITextService textService,
        IDispatchNotificationService dispatchNotificationService,
        IDispatchResponsibilityService dispatchResponsibilityService)
        : base(
            textService.Resolve(TextTokens.DispatchTitle),
            textService.Resolve(TextTokens.DispatchDescription))
    {
        _dispatchNotificationService = dispatchNotificationService;
        _dispatchResponsibilityService = dispatchResponsibilityService;
        _textService = textService;
        InitializeText();

        _dispatchNowCommand = new RelayCommand(_ => SimulateDispatchSelected(), _ => SelectedWorkOrderDetail?.WorkOrderStatus == DispatchWorkOrderStatus.PendingDispatch);
        _markRecoveredCommand = new RelayCommand(_ => SimulateRecoverySelected(), _ => SelectedWorkOrderDetail?.RecoveryStatus == DispatchRecoveryStatus.Unrecovered);
        _openResponsibilityEditorCommand = new RelayCommand(_ => OpenResponsibilityEditor(), _ => SelectedWorkOrderDetail is not null);
        _openReportsCenterCommand = new RelayCommand(_ => RequestNavigate(AppSectionId.Reports));
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
        OpenReportsCenterCommand = _openReportsCenterCommand;
        SaveResponsibilityCommand = _saveResponsibilityCommand;
        CancelResponsibilityCommand = new RelayCommand(_ => CloseResponsibilityEditor());
        NavigateToDispatchCommand = new RelayCommand(_ => RequestNavigate(AppSectionId.Dispatch));

        _allWorkOrders = CreateWorkOrders(dispatchNotificationService.GetWorkOrders().Data);
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
    public string OpenReportsCenterText { get; private set; } = string.Empty;
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
    public ICommand OpenReportsCenterCommand { get; }
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
        OpenReportsCenterText = _textService.Resolve(TextTokens.ReportsActionOpenCenter);
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

        var notificationResponse = _dispatchNotificationService.SendFaultNotification(CreateNotificationRequest(SelectedWorkOrderDetail));
        var sentAt = notificationResponse.Data.SentAt;
        var statusText = notificationResponse.Data.StatusText;
        SelectedWorkOrderDetail.NotificationRecord.FaultNotificationSentAt = sentAt;
        SelectedWorkOrderDetail.NotificationRecord.FaultNotificationStatus = statusText;
        SelectedWorkOrderDetail.NotificationRecord.TimelineEntries.Insert(
            0,
            new DispatchNotificationEntryState(
                _textService.Resolve(TextTokens.DispatchNotificationFaultTitle),
                sentAt,
                statusText,
                notificationResponse.Data.TimelineActor));

        if (notificationResponse.IsSuccess)
        {
            SelectedWorkOrderDetail.WorkOrderStatus = DispatchWorkOrderStatus.Dispatched;
            SelectedWorkOrderDetail.WorkOrderStatusText = _textService.Resolve(TextTokens.DispatchWorkOrderDispatched);
            WorkspaceFeedback = string.Format(
                _textService.Resolve(TextTokens.DispatchDispatchFeedbackPattern),
                SelectedWorkOrderDetail.PointName);
        }
        else
        {
            WorkspaceFeedback = notificationResponse.Message;
        }

        ApplyFilters(SelectedWorkOrderDetail.WorkOrderId);
    }

    private void SimulateRecoverySelected()
    {
        if (SelectedWorkOrderDetail is null || SelectedWorkOrderDetail.RecoveryStatus != DispatchRecoveryStatus.Unrecovered)
        {
            return;
        }

        var notificationResponse = _dispatchNotificationService.SendRecoveryNotification(CreateNotificationRequest(SelectedWorkOrderDetail));
        var sentAt = notificationResponse.Data.SentAt;
        var statusText = notificationResponse.Data.StatusText;
        SelectedWorkOrderDetail.NotificationRecord.RecoveryNotificationSentAt = sentAt;
        SelectedWorkOrderDetail.NotificationRecord.RecoveryNotificationStatus = statusText;
        SelectedWorkOrderDetail.NotificationRecord.TimelineEntries.Insert(
            0,
            new DispatchNotificationEntryState(
                _textService.Resolve(TextTokens.DispatchNotificationRecoveryTitle),
                sentAt,
                statusText,
                notificationResponse.Data.TimelineActor));

        if (notificationResponse.IsSuccess)
        {
            SelectedWorkOrderDetail.RecoveryStatus = DispatchRecoveryStatus.Recovered;
            SelectedWorkOrderDetail.RecoveryStatusText = _textService.Resolve(TextTokens.DispatchRecoveryRecovered);
            WorkspaceFeedback = string.Format(
                _textService.Resolve(TextTokens.DispatchRecoveryFeedbackPattern),
                SelectedWorkOrderDetail.PointName);
        }
        else
        {
            WorkspaceFeedback = notificationResponse.Message;
        }

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

        var saveResponse = _dispatchResponsibilityService.Save(new DispatchResponsibilityUpdateDto(
            SelectedWorkOrderDetail.PointId,
            SelectedWorkOrderDetail.PointName,
            ResponsibilityEditor.CurrentHandlingUnit,
            ResponsibilityEditor.MaintainerName,
            ResponsibilityEditor.MaintainerPhone,
            ResponsibilityEditor.SupervisorName,
            ResponsibilityEditor.SupervisorPhone,
            ResponsibilityEditor.NotificationChannelId));
        if (!saveResponse.IsSuccess)
        {
            WorkspaceFeedback = saveResponse.Message;
            return;
        }

        SelectedWorkOrderDetail.Responsibility.CurrentHandlingUnit = saveResponse.Data.CurrentHandlingUnit;
        SelectedWorkOrderDetail.Responsibility.MaintainerName = saveResponse.Data.MaintainerName;
        SelectedWorkOrderDetail.Responsibility.MaintainerPhone = saveResponse.Data.MaintainerPhone;
        SelectedWorkOrderDetail.Responsibility.SupervisorName = saveResponse.Data.SupervisorName;
        SelectedWorkOrderDetail.Responsibility.SupervisorPhone = saveResponse.Data.SupervisorPhone;
        SelectedWorkOrderDetail.Responsibility.NotificationChannelId = saveResponse.Data.NotificationChannelId;

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

    private DispatchNotificationRequestDto CreateNotificationRequest(DispatchWorkOrderDetailState workOrder)
    {
        return new DispatchNotificationRequestDto(
            workOrder.PointId,
            workOrder.Responsibility.CurrentHandlingUnit,
            workOrder.Responsibility.MaintainerName,
            workOrder.Responsibility.MaintainerPhone,
            workOrder.Responsibility.SupervisorName,
            workOrder.Responsibility.SupervisorPhone,
            workOrder.PointName,
            workOrder.FaultType,
            ParseFaultTime(workOrder.RepeatFault.LatestFaultTime),
            workOrder.ScreenshotTitle,
            workOrder.Responsibility.NotificationChannelId);
    }

    private List<DispatchWorkOrderDetailState> CreateWorkOrders(IReadOnlyList<DispatchWorkOrderModel> workOrders)
        => workOrders.Select(CreateWorkOrder).ToList();

    private DispatchWorkOrderDetailState CreateWorkOrder(DispatchWorkOrderModel workOrder)
    {
        return CreateWorkOrder(
            workOrder.WorkOrderId,
            workOrder.PointId,
            workOrder.PointName,
            workOrder.FaultType,
            workOrder.InspectionGroupName,
            workOrder.MapLocationPlaceholder,
            workOrder.ScreenshotTitle,
            workOrder.ScreenshotSubtitle,
            workOrder.FaultSummary,
            workOrder.LatestInspectionConclusion,
            workOrder.EntersDispatchPool,
            workOrder.IsTodayNew,
            MapDispatchMethod(workOrder.DispatchMethod),
            MapWorkOrderStatus(workOrder.WorkOrderStatus),
            MapRecoveryStatus(workOrder.RecoveryStatus),
            new DispatchResponsibilityState(
                workOrder.Responsibility.CurrentHandlingUnit,
                workOrder.Responsibility.MaintainerName,
                workOrder.Responsibility.MaintainerPhone,
                workOrder.Responsibility.SupervisorName,
                workOrder.Responsibility.SupervisorPhone,
                workOrder.Responsibility.NotificationChannelId),
            workOrder.RepeatFault.FirstFaultTime,
            workOrder.RepeatFault.LatestFaultTime,
            workOrder.RepeatFault.RepeatCount,
            workOrder.NotificationRecord.FaultNotificationSentAt,
            workOrder.NotificationRecord.FaultNotificationStatus,
            workOrder.NotificationRecord.RecoveryNotificationSentAt,
            workOrder.NotificationRecord.RecoveryNotificationStatus);
    }

    private static DateTime ParseFaultTime(string value)
        => DateTime.TryParse(value, out var parsed) ? parsed : DateTime.Now;

    private static DispatchMethod MapDispatchMethod(DispatchMethodModel method)
        => method == DispatchMethodModel.Automatic ? DispatchMethod.Automatic : DispatchMethod.Manual;

    private static DispatchWorkOrderStatus MapWorkOrderStatus(DispatchWorkOrderStatusModel status)
        => status == DispatchWorkOrderStatusModel.PendingDispatch
            ? DispatchWorkOrderStatus.PendingDispatch
            : DispatchWorkOrderStatus.Dispatched;

    private static DispatchRecoveryStatus MapRecoveryStatus(DispatchRecoveryStatusModel status)
        => status == DispatchRecoveryStatusModel.Unrecovered
            ? DispatchRecoveryStatus.Unrecovered
            : DispatchRecoveryStatus.Recovered;

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
