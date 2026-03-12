using System.Collections.ObjectModel;
using System.ComponentModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.Services.Localization;
using TianyiVision.Acis.UI.Mvvm;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class InspectionPageViewModel
{
    private const string ReviewQuickFilterAll = "all";
    private const string ReviewQuickFilterNormal = "normal";
    private const string ReviewQuickFilterFault = "fault";
    private const string ReviewQuickFilterPending = "pending";
    private const string ReviewQuickFilterDispatchPool = "dispatch";

    private bool _isReviewWallVisible;
    private InspectionReviewTaskSummaryState? _reviewTaskSummary;
    private InspectionReviewFilterState? _reviewFilter;
    private ObservableCollection<InspectionReviewCardState> _filteredReviewCards = [];
    private InspectionReviewCardState? _selectedReviewCard;
    private InspectionReviewDetailState? _selectedReviewDetail;
    private InspectionReviewFilterState? _activeReviewFilter;

    public string OpenReviewWallText { get; private set; } = string.Empty;
    public string ReviewWallBadge { get; private set; } = string.Empty;
    public string ReviewWallTitle { get; private set; } = string.Empty;
    public string ReviewWallDescription { get; private set; } = string.Empty;
    public string ReviewSummaryDescription { get; private set; } = string.Empty;
    public string ReviewTaskFinishedAtLabel { get; private set; } = string.Empty;
    public string ReviewTaskReviewStatusLabel { get; private set; } = string.Empty;
    public string ReviewTaskReviewedCountLabel { get; private set; } = string.Empty;
    public string ReviewTaskTransitionHintLabel { get; private set; } = string.Empty;
    public string ReviewConfirmTaskText { get; private set; } = string.Empty;
    public string ReviewBackToInspectionText { get; private set; } = string.Empty;
    public string ReviewMarkReviewedText { get; private set; } = string.Empty;
    public string ReviewReviewedText { get; private set; } = string.Empty;
    public string ReviewFilterTitle { get; private set; } = string.Empty;
    public string ReviewFilterDescription { get; private set; } = string.Empty;
    public string ReviewFilterUnitLabel { get; private set; } = string.Empty;
    public string ReviewFilterFaultTypeLabel { get; private set; } = string.Empty;
    public string ReviewWallSectionDescription { get; private set; } = string.Empty;
    public string ReviewDetailDescription { get; private set; } = string.Empty;
    public string ReviewDetailResultLabel { get; private set; } = string.Empty;
    public string ReviewDetailReviewStatusLabel { get; private set; } = string.Empty;
    public string ReviewDetailScreenshotLabel { get; private set; } = string.Empty;
    public string ReviewDetailReviewNoteLabel { get; private set; } = string.Empty;

    public bool IsReviewWallVisible
    {
        get => _isReviewWallVisible;
        private set => SetProperty(ref _isReviewWallVisible, value);
    }

    public InspectionReviewTaskSummaryState? ReviewTaskSummary
    {
        get => _reviewTaskSummary;
        private set
        {
            if (SetProperty(ref _reviewTaskSummary, value))
            {
                _confirmReviewCompletedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public InspectionReviewFilterState? ReviewFilter
    {
        get => _reviewFilter;
        private set
        {
            AttachReviewFilter(value);
            SetProperty(ref _reviewFilter, value);
        }
    }

    public ObservableCollection<InspectionReviewCardState> FilteredReviewCards
    {
        get => _filteredReviewCards;
        private set => SetProperty(ref _filteredReviewCards, value);
    }

    public InspectionReviewCardState? SelectedReviewCard
    {
        get => _selectedReviewCard;
        private set
        {
            if (SetProperty(ref _selectedReviewCard, value))
            {
                _markSelectedReviewCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public InspectionReviewDetailState? SelectedReviewDetail
    {
        get => _selectedReviewDetail;
        private set => SetProperty(ref _selectedReviewDetail, value);
    }

    private void InitializeReviewText(ITextService textService)
    {
        OpenReviewWallText = textService.Resolve(TextTokens.InspectionActionOpenReviewWall);
        ReviewWallBadge = textService.Resolve(TextTokens.InspectionReviewWallBadge);
        ReviewWallTitle = textService.Resolve(TextTokens.InspectionReviewWallTitle);
        ReviewWallDescription = textService.Resolve(TextTokens.InspectionReviewWallDescription);
        ReviewSummaryDescription = textService.Resolve(TextTokens.InspectionReviewSummaryDescription);
        ReviewTaskFinishedAtLabel = textService.Resolve(TextTokens.InspectionReviewTaskFinishedAtLabel);
        ReviewTaskReviewStatusLabel = textService.Resolve(TextTokens.InspectionReviewTaskReviewStatusLabel);
        ReviewTaskReviewedCountLabel = textService.Resolve(TextTokens.InspectionReviewTaskReviewedCountLabel);
        ReviewTaskTransitionHintLabel = textService.Resolve(TextTokens.InspectionReviewTaskTransitionHint);
        ReviewConfirmTaskText = textService.Resolve(TextTokens.InspectionReviewActionConfirmTask);
        ReviewBackToInspectionText = textService.Resolve(TextTokens.InspectionReviewActionBackToInspection);
        ReviewMarkReviewedText = textService.Resolve(TextTokens.InspectionReviewActionMarkReviewed);
        ReviewReviewedText = textService.Resolve(TextTokens.InspectionReviewActionReviewed);
        ReviewFilterTitle = textService.Resolve(TextTokens.InspectionReviewFilterTitle);
        ReviewFilterDescription = textService.Resolve(TextTokens.InspectionReviewFilterDescription);
        ReviewFilterUnitLabel = textService.Resolve(TextTokens.InspectionReviewFilterUnitLabel);
        ReviewFilterFaultTypeLabel = textService.Resolve(TextTokens.InspectionReviewFilterFaultTypeLabel);
        ReviewWallSectionDescription = textService.Resolve(TextTokens.InspectionReviewWallSectionDescription);
        ReviewDetailDescription = textService.Resolve(TextTokens.InspectionReviewDetailDescription);
        ReviewDetailResultLabel = textService.Resolve(TextTokens.InspectionReviewDetailResultLabel);
        ReviewDetailReviewStatusLabel = textService.Resolve(TextTokens.InspectionReviewDetailReviewStatusLabel);
        ReviewDetailScreenshotLabel = textService.Resolve(TextTokens.InspectionReviewDetailScreenshotLabel);
        ReviewDetailReviewNoteLabel = textService.Resolve(TextTokens.InspectionReviewDetailReviewNoteLabel);
    }

    private void InitializeReviewCommands()
    {
        _openReviewWallCommand = new RelayCommand(_ => OpenReviewWall());
        _confirmReviewCompletedCommand = new RelayCommand(_ => ConfirmReviewCompleted(), _ => ReviewTaskSummary is not null);
        _markSelectedReviewCommand = new RelayCommand(_ => MarkSelectedReviewCard(), _ => SelectedReviewCard is not null && !SelectedReviewCard.IsReviewed);

        OpenReviewWallCommand = _openReviewWallCommand;
        ReturnToInspectionWorkspaceCommand = new RelayCommand(_ => IsReviewWallVisible = false);
        ConfirmReviewCompletedCommand = _confirmReviewCompletedCommand;
        SelectReviewCardCommand = new RelayCommand(parameter =>
        {
            if (parameter is InspectionReviewCardState card)
            {
                SelectReviewCard(card);
            }
        });
        MarkSelectedReviewCommand = _markSelectedReviewCommand;
        SelectReviewQuickFilterCommand = new RelayCommand(parameter =>
        {
            if (parameter is InspectionReviewFilterOptionState option)
            {
                SetReviewQuickFilter(option.Key);
            }
        });
    }

    private void OpenReviewWall()
    {
        if (GetCurrentWorkspace() is not { } workspace)
        {
            return;
        }

        LoadReviewState(workspace, refreshFromPoints: true);
        IsReviewWallVisible = true;
    }

    private void ConfirmReviewCompleted()
    {
        if (GetCurrentWorkspace() is not { } workspace || workspace.ReviewCards is null)
        {
            return;
        }

        foreach (var card in workspace.ReviewCards)
        {
            SetCardReviewState(card, InspectionReviewStatus.Reviewed);
        }

        UpdateReviewSummary(workspace);
        ApplyReviewFilter(workspace);
        if (SelectedReviewCard is not null)
        {
            SelectedReviewDetail = CreateReviewDetail(SelectedReviewCard);
        }
    }

    private void MarkSelectedReviewCard()
    {
        if (GetCurrentWorkspace() is not { } workspace || SelectedReviewCard is null)
        {
            return;
        }

        SetCardReviewState(SelectedReviewCard, InspectionReviewStatus.Reviewed);
        UpdateReviewSummary(workspace);
        ApplyReviewFilter(workspace, SelectedReviewCard.PointId);
    }

    private void SelectReviewCard(InspectionReviewCardState card)
    {
        if (GetCurrentWorkspace() is not { } workspace || workspace.ReviewCards is null)
        {
            return;
        }

        foreach (var candidate in workspace.ReviewCards)
        {
            candidate.IsSelected = candidate.PointId == card.PointId;
        }

        SelectedReviewCard = workspace.ReviewCards.First(item => item.PointId == card.PointId);
        SelectedReviewDetail = CreateReviewDetail(SelectedReviewCard);
    }

    private void SetReviewQuickFilter(string filterKey)
    {
        if (ReviewFilter is null)
        {
            return;
        }

        foreach (var option in ReviewFilter.QuickFilters)
        {
            option.IsSelected = option.Key == filterKey;
        }

        ReviewFilter.SelectedQuickFilterKey = filterKey;
    }

    private void LoadReviewState(GroupWorkspaceState workspace, bool refreshFromPoints)
    {
        if (refreshFromPoints || workspace.ReviewCards is null)
        {
            var existingStatuses = workspace.ReviewCards?.ToDictionary(card => card.PointId, card => card.ReviewStatus)
                ?? new Dictionary<string, InspectionReviewStatus>(StringComparer.Ordinal);
            workspace.ReviewCards = BuildReviewCards(workspace, existingStatuses);
        }

        workspace.ReviewFilter ??= CreateReviewFilterState();
        UpdateReviewFilterOptions(workspace.ReviewFilter, workspace.ReviewCards);

        workspace.ReviewSummary ??= new InspectionReviewTaskSummaryState();
        UpdateReviewSummary(workspace);

        ReviewTaskSummary = workspace.ReviewSummary;
        ReviewFilter = workspace.ReviewFilter;
        ApplyReviewFilter(workspace);
    }

    private void RefreshReviewStateAfterSimulation(GroupWorkspaceState workspace)
    {
        if (workspace.ReviewCards is not null)
        {
            LoadReviewState(workspace, refreshFromPoints: true);
        }
    }

    private ObservableCollection<InspectionReviewCardState> BuildReviewCards(
        GroupWorkspaceState workspace,
        IReadOnlyDictionary<string, InspectionReviewStatus> existingStatuses)
    {
        var startedAt = ParseDateTimeOrDefault(workspace.RunSummary.StartedAt, new DateTime(2026, 3, 12, 9, 0, 0));
        var cards = workspace.Points
            .Where(ShouldAppearOnReviewWall)
            .Select((point, index) =>
            {
                var reviewStatus = existingStatuses.TryGetValue(point.Id, out var persistedStatus)
                    ? persistedStatus
                    : InspectionReviewStatus.Pending;
                return CreateReviewCard(point, startedAt.AddMinutes(index * 3 + 2), reviewStatus);
            });

        return new ObservableCollection<InspectionReviewCardState>(cards);
    }

    private InspectionReviewCardState CreateReviewCard(
        InspectionPointState point,
        DateTime inspectedAt,
        InspectionReviewStatus reviewStatus)
    {
        var effectiveStatus = ResolveReviewEffectiveStatus(point);
        var isFault = effectiveStatus is InspectionPointStatus.Fault or InspectionPointStatus.PausedUntilRecovery;
        var screenshotTitle = effectiveStatus == InspectionPointStatus.PausedUntilRecovery
            ? _textService.Resolve(TextTokens.InspectionReviewCardPreviewPaused)
            : isFault
                ? _textService.Resolve(TextTokens.InspectionReviewCardPreviewFault)
                : _textService.Resolve(TextTokens.InspectionReviewCardPreviewNormal);

        return new InspectionReviewCardState(
            point.Id,
            point.Name,
            point.UnitName,
            screenshotTitle,
            ResolvePointStatus(effectiveStatus),
            point.FaultDescription,
            point.DispatchPoolEntry,
            point.FaultType,
            point.OnlineStatus,
            point.PlaybackStatus,
            point.ImageStatus,
            point.LastFaultTime,
            point.LastInspectionConclusion,
            _textService.Resolve(TextTokens.InspectionReviewNotePlaceholder),
            isFault,
            string.Equals(point.DispatchPoolEntry, _textService.Resolve(TextTokens.InspectionDispatchPoolYes), StringComparison.Ordinal),
            inspectedAt,
            reviewStatus,
            ResolveReviewStatusText(reviewStatus));
    }

    private InspectionReviewFilterState CreateReviewFilterState()
    {
        return new InspectionReviewFilterState(
            new ObservableCollection<InspectionReviewFilterOptionState>
            {
                new(ReviewQuickFilterAll, _textService.Resolve(TextTokens.InspectionReviewFilterAll), true),
                new(ReviewQuickFilterNormal, _textService.Resolve(TextTokens.InspectionReviewFilterNormal)),
                new(ReviewQuickFilterFault, _textService.Resolve(TextTokens.InspectionReviewFilterFault)),
                new(ReviewQuickFilterPending, _textService.Resolve(TextTokens.InspectionReviewFilterPendingOnly)),
                new(ReviewQuickFilterDispatchPool, _textService.Resolve(TextTokens.InspectionReviewFilterDispatchPool))
            },
            new ObservableCollection<string>(),
            new ObservableCollection<string>(),
            _textService.Resolve(TextTokens.InspectionReviewFilterAllUnits),
            _textService.Resolve(TextTokens.InspectionReviewFilterAllFaultTypes),
            ReviewQuickFilterAll);
    }

    private void UpdateReviewFilterOptions(
        InspectionReviewFilterState filter,
        IEnumerable<InspectionReviewCardState> cards)
    {
        var allUnitsText = _textService.Resolve(TextTokens.InspectionReviewFilterAllUnits);
        var allFaultTypesText = _textService.Resolve(TextTokens.InspectionReviewFilterAllFaultTypes);
        var unitOptions = cards
            .Select(card => card.UnitName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Prepend(allUnitsText);
        var faultTypeOptions = cards
            .Where(card => !string.Equals(card.FaultType, _textService.Resolve(TextTokens.InspectionFaultTypeNone), StringComparison.Ordinal))
            .Select(card => card.FaultType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Prepend(allFaultTypesText);

        ReplaceCollection(filter.UnitOptions, unitOptions);
        ReplaceCollection(filter.FaultTypeOptions, faultTypeOptions);

        if (!filter.UnitOptions.Contains(filter.SelectedUnit))
        {
            filter.SelectedUnit = allUnitsText;
        }

        if (!filter.FaultTypeOptions.Contains(filter.SelectedFaultType))
        {
            filter.SelectedFaultType = allFaultTypesText;
        }
    }

    private void ApplyReviewFilter(GroupWorkspaceState? workspace = null, string? preferredPointId = null)
    {
        workspace ??= GetCurrentWorkspace();
        if (workspace?.ReviewCards is null || ReviewFilter is null)
        {
            FilteredReviewCards = [];
            SelectedReviewCard = null;
            SelectedReviewDetail = null;
            return;
        }

        IEnumerable<InspectionReviewCardState> filteredCards = workspace.ReviewCards;
        filteredCards = ReviewFilter.SelectedQuickFilterKey switch
        {
            ReviewQuickFilterNormal => filteredCards.Where(card => !card.IsFault),
            ReviewQuickFilterFault => filteredCards.Where(card => card.IsFault),
            ReviewQuickFilterPending => filteredCards.Where(card => !card.IsReviewed),
            ReviewQuickFilterDispatchPool => filteredCards.Where(card => card.EntersDispatchPool),
            _ => filteredCards
        };

        var allUnitsText = _textService.Resolve(TextTokens.InspectionReviewFilterAllUnits);
        if (!string.Equals(ReviewFilter.SelectedUnit, allUnitsText, StringComparison.Ordinal))
        {
            filteredCards = filteredCards.Where(card => string.Equals(card.UnitName, ReviewFilter.SelectedUnit, StringComparison.Ordinal));
        }

        var allFaultTypesText = _textService.Resolve(TextTokens.InspectionReviewFilterAllFaultTypes);
        if (!string.Equals(ReviewFilter.SelectedFaultType, allFaultTypesText, StringComparison.Ordinal))
        {
            filteredCards = filteredCards.Where(card => string.Equals(card.FaultType, ReviewFilter.SelectedFaultType, StringComparison.Ordinal));
        }

        var orderedCards = filteredCards
            .OrderBy(card => card.IsFault ? 0 : 1)
            .ThenBy(card => card.InspectedAt)
            .ThenBy(card => card.PointName, StringComparer.Ordinal)
            .ToList();

        FilteredReviewCards = new ObservableCollection<InspectionReviewCardState>(orderedCards);

        var nextSelected = orderedCards.FirstOrDefault(card => card.PointId == preferredPointId)
            ?? orderedCards.FirstOrDefault(card => card.PointId == SelectedReviewCard?.PointId)
            ?? orderedCards.FirstOrDefault(card => card.IsFault)
            ?? orderedCards.FirstOrDefault();

        if (nextSelected is not null)
        {
            SelectReviewCard(nextSelected);
            return;
        }

        foreach (var card in workspace.ReviewCards)
        {
            card.IsSelected = false;
        }

        SelectedReviewCard = null;
        SelectedReviewDetail = null;
    }

    private void UpdateReviewSummary(GroupWorkspaceState workspace)
    {
        if (workspace.ReviewCards is null)
        {
            return;
        }

        var totalCount = workspace.ReviewCards.Count;
        var faultCount = workspace.ReviewCards.Count(card => card.IsFault);
        var reviewedCount = workspace.ReviewCards.Count(card => card.IsReviewed);
        var reviewStatus = reviewedCount == totalCount && totalCount > 0
            ? InspectionReviewStatus.Reviewed
            : InspectionReviewStatus.Pending;

        workspace.ReviewSummary ??= new InspectionReviewTaskSummaryState();
        workspace.ReviewSummary.GroupName = workspace.Group.Name;
        workspace.ReviewSummary.StartedAt = workspace.RunSummary.StartedAt;
        workspace.ReviewSummary.FinishedAt = workspace.TaskFinishedAt;
        workspace.ReviewSummary.TotalPoints = totalCount.ToString();
        workspace.ReviewSummary.NormalCount = (totalCount - faultCount).ToString();
        workspace.ReviewSummary.FaultCount = faultCount.ToString();
        workspace.ReviewSummary.ReviewedCount = $"{reviewedCount} / {totalCount}";
        workspace.ReviewSummary.ReviewStatus = reviewStatus;
        workspace.ReviewSummary.ReviewStatusText = ResolveReviewStatusText(reviewStatus);
        workspace.ReviewSummary.TransitionHint = reviewStatus == InspectionReviewStatus.Reviewed
            ? _textService.Resolve(TextTokens.InspectionReviewTaskCompletedFeedback)
            : _textService.Resolve(TextTokens.InspectionReviewTaskPendingFeedback);
    }

    private void SetCardReviewState(InspectionReviewCardState card, InspectionReviewStatus reviewStatus)
    {
        card.ReviewStatus = reviewStatus;
        card.ReviewStatusText = ResolveReviewStatusText(reviewStatus);
    }

    private InspectionReviewDetailState CreateReviewDetail(InspectionReviewCardState card)
    {
        return new InspectionReviewDetailState(
            card.PointName,
            card.UnitName,
            card.InspectionResult,
            card.OnlineStatus,
            card.PlaybackStatus,
            card.ImageStatus,
            card.FaultType,
            card.FaultDescription,
            card.ScreenshotTitle,
            card.DispatchPoolEntry,
            card.LastFaultTime,
            card.LastInspectionConclusion,
            card.ReviewStatusText,
            card.IsReviewed
                ? _textService.Resolve(TextTokens.InspectionReviewItemReviewedFeedback)
                : card.ReviewNotePlaceholder,
            card.IsReviewed,
            card.IsFault);
    }

    private void AttachReviewFilter(InspectionReviewFilterState? filter)
    {
        if (ReferenceEquals(_activeReviewFilter, filter))
        {
            return;
        }

        if (_activeReviewFilter is not null)
        {
            _activeReviewFilter.PropertyChanged -= HandleReviewFilterChanged;
        }

        _activeReviewFilter = filter;

        if (_activeReviewFilter is not null)
        {
            _activeReviewFilter.PropertyChanged += HandleReviewFilterChanged;
        }
    }

    private void HandleReviewFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InspectionReviewFilterState.SelectedUnit)
            or nameof(InspectionReviewFilterState.SelectedFaultType)
            or nameof(InspectionReviewFilterState.SelectedQuickFilterKey))
        {
            ApplyReviewFilter();
        }
    }

    private string ResolveReviewStatusText(InspectionReviewStatus reviewStatus)
    {
        return reviewStatus == InspectionReviewStatus.Reviewed
            ? _textService.Resolve(TextTokens.InspectionReviewStatusReviewed)
            : _textService.Resolve(TextTokens.InspectionReviewStatusPending);
    }

    private InspectionPointStatus ResolveReviewEffectiveStatus(InspectionPointState point)
    {
        return point.Status is InspectionPointStatus.Pending or InspectionPointStatus.Inspecting
            ? point.CompletionStatus
            : point.Status;
    }

    private bool ShouldAppearOnReviewWall(InspectionPointState point)
        => point.Status != InspectionPointStatus.Silent;

    private GroupWorkspaceState? GetCurrentWorkspace()
    {
        if (SelectedGroup is null || !_workspaceByGroupId.TryGetValue(SelectedGroup.Id, out var workspace))
        {
            return null;
        }

        return workspace;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static DateTime ParseDateTimeOrDefault(string value, DateTime fallback)
        => DateTime.TryParse(value, out var parsed) ? parsed : fallback;
}
