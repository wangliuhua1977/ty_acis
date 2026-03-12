using System.Collections.ObjectModel;
using System.ComponentModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.ViewModels;

public sealed partial class SettingsPageViewModel
{
    private ObservableCollection<TerminologySchemeSummaryState> CreateTerminologySchemes()
    {
        var items = new ObservableCollection<TerminologySchemeSummaryState>();

        foreach (var profile in _textService.GetAvailableProfiles())
        {
            items.Add(new TerminologySchemeSummaryState(
                profile.Id,
                profile.DisplayName,
                DescribeTerminologyProfile(profile.Id),
                _textService.Resolve(TextTokens.SettingsTerminologyPresetTag),
                true,
                CloneProfile(profile, profile.Id, profile.DisplayName),
                CloneProfile(profile, profile.Id, profile.DisplayName)));
        }

        _customTerminologyCounter = items.Count + 1;
        return items;
    }

    private ObservableCollection<TerminologyGroupState> CreateTerminologyGroups()
    {
        return
        [
            new TerminologyGroupState(
                "navigation",
                _textService.Resolve(TextTokens.SettingsTerminologyGroupNavigation),
                new ObservableCollection<TerminologyFieldState>
                {
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.NavigationHome, _textService.Resolve(TextTokens.SettingsTerminologyFieldNavigationHome), _textService.Resolve(TextTokens.NavigationHome))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.NavigationInspection, _textService.Resolve(TextTokens.SettingsTerminologyFieldNavigationInspection), _textService.Resolve(TextTokens.NavigationInspection))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.NavigationDispatch, _textService.Resolve(TextTokens.SettingsTerminologyFieldNavigationDispatch), _textService.Resolve(TextTokens.NavigationDispatch))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.NavigationReports, _textService.Resolve(TextTokens.SettingsTerminologyFieldNavigationReports), _textService.Resolve(TextTokens.NavigationReports))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.NavigationSettings, _textService.Resolve(TextTokens.SettingsTerminologyFieldNavigationSettings), _textService.Resolve(TextTokens.NavigationSettings)))
                }),
            new TerminologyGroupState(
                "titles",
                _textService.Resolve(TextTokens.SettingsTerminologyGroupPageTitles),
                new ObservableCollection<TerminologyFieldState>
                {
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.HomeTitle, _textService.Resolve(TextTokens.SettingsTerminologyFieldHomeTitle), _textService.Resolve(TextTokens.HomeTitle))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.InspectionTitle, _textService.Resolve(TextTokens.SettingsTerminologyFieldInspectionTitle), _textService.Resolve(TextTokens.InspectionTitle))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchTitle, _textService.Resolve(TextTokens.SettingsTerminologyFieldDispatchTitle), _textService.Resolve(TextTokens.DispatchTitle))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.ReportsTitle, _textService.Resolve(TextTokens.SettingsTerminologyFieldReportsTitle), _textService.Resolve(TextTokens.ReportsTitle))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.SettingsTitle, _textService.Resolve(TextTokens.SettingsTerminologyFieldSettingsTitle), _textService.Resolve(TextTokens.SettingsTitle)))
                }),
            new TerminologyGroupState(
                "status",
                _textService.Resolve(TextTokens.SettingsTerminologyGroupStatus),
                new ObservableCollection<TerminologyFieldState>
                {
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.InspectionStatusNormal, _textService.Resolve(TextTokens.SettingsTerminologyFieldInspectionNormal), _textService.Resolve(TextTokens.InspectionStatusNormal))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.InspectionStatusFault, _textService.Resolve(TextTokens.SettingsTerminologyFieldInspectionFault), _textService.Resolve(TextTokens.InspectionStatusFault))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchWorkOrderPending, _textService.Resolve(TextTokens.SettingsTerminologyFieldDispatchPending), _textService.Resolve(TextTokens.DispatchWorkOrderPending))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchWorkOrderDispatched, _textService.Resolve(TextTokens.SettingsTerminologyFieldDispatchDispatched), _textService.Resolve(TextTokens.DispatchWorkOrderDispatched))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchRecoveryUnrecovered, _textService.Resolve(TextTokens.SettingsTerminologyFieldRecoveryPending), _textService.Resolve(TextTokens.DispatchRecoveryUnrecovered))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchRecoveryRecovered, _textService.Resolve(TextTokens.SettingsTerminologyFieldRecoveryDone), _textService.Resolve(TextTokens.DispatchRecoveryRecovered)))
                }),
            new TerminologyGroupState(
                "hints",
                _textService.Resolve(TextTokens.SettingsTerminologyGroupHints),
                new ObservableCollection<TerminologyFieldState>
                {
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.HomeMapSelectionHint, _textService.Resolve(TextTokens.SettingsTerminologyFieldHomeHint), _textService.Resolve(TextTokens.HomeMapSelectionHint))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.InspectionWorkbenchHint, _textService.Resolve(TextTokens.SettingsTerminologyFieldInspectionHint), _textService.Resolve(TextTokens.InspectionWorkbenchHint))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchMergeDescription, _textService.Resolve(TextTokens.SettingsTerminologyFieldDispatchHint), _textService.Resolve(TextTokens.DispatchMergeDescription))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.ReportsDescription, _textService.Resolve(TextTokens.SettingsTerminologyFieldReportsHint), _textService.Resolve(TextTokens.ReportsDescription)))
                }),
            new TerminologyGroupState(
                "notifications",
                _textService.Resolve(TextTokens.SettingsTerminologyGroupNotifications),
                new ObservableCollection<TerminologyFieldState>
                {
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchNotificationFaultTitle, _textService.Resolve(TextTokens.SettingsTerminologyFieldFaultNotification), _textService.Resolve(TextTokens.DispatchNotificationFaultTitle))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.DispatchNotificationRecoveryTitle, _textService.Resolve(TextTokens.SettingsTerminologyFieldRecoveryNotification), _textService.Resolve(TextTokens.DispatchNotificationRecoveryTitle)))
                }),
            new TerminologyGroupState(
                "reports",
                _textService.Resolve(TextTokens.SettingsTerminologyGroupReports),
                new ObservableCollection<TerminologyFieldState>
                {
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.ReportsTabInspectionExecution, _textService.Resolve(TextTokens.SettingsTerminologyFieldReportInspection), _textService.Resolve(TextTokens.ReportsTabInspectionExecution))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.ReportsTabFaultStatistics, _textService.Resolve(TextTokens.SettingsTerminologyFieldReportFault), _textService.Resolve(TextTokens.ReportsTabFaultStatistics))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.ReportsTabDispatchDisposal, _textService.Resolve(TextTokens.SettingsTerminologyFieldReportDispatch), _textService.Resolve(TextTokens.ReportsTabDispatchDisposal))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.ReportsTabResponsibility, _textService.Resolve(TextTokens.SettingsTerminologyFieldReportResponsibility), _textService.Resolve(TextTokens.ReportsTabResponsibility))),
                    RegisterTerminologyField(new TerminologyFieldState(TextTokens.ReportsTabOutstanding, _textService.Resolve(TextTokens.SettingsTerminologyFieldReportOutstanding), _textService.Resolve(TextTokens.ReportsTabOutstanding)))
                })
        ];
    }

    private TerminologyFieldState RegisterTerminologyField(TerminologyFieldState field)
    {
        _terminologyFields[field.TokenKey] = field;
        return field;
    }

    private void HookTerminologyEditor()
    {
        foreach (var group in TerminologyGroups)
        {
            foreach (var field in group.Fields)
            {
                field.PropertyChanged += HandleTerminologyFieldChanged;
            }
        }
    }

    private void HandleTerminologyFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminologyFieldState.Value))
        {
            UpdateTerminologyPreview();
        }
    }

    private void SelectTerminologyScheme(string profileId)
    {
        SelectedTerminologyScheme = TerminologyItems.FirstOrDefault(item => item.Id == profileId) ?? TerminologyItems.First();

        foreach (var item in TerminologyItems)
        {
            item.IsSelected = item == SelectedTerminologyScheme;
        }

        LoadTerminologyEditor(SelectedTerminologyScheme);
        UpdateTerminologyPreview();
    }

    private void LoadTerminologyEditor(TerminologySchemeSummaryState scheme)
    {
        foreach (var field in _terminologyFields.Values)
        {
            field.Value = scheme.SavedProfile.Resolve(field.TokenKey);
        }
    }

    private void UpdateTerminologyPreview()
    {
        if (SelectedTerminologyScheme is null)
        {
            return;
        }

        TerminologyPreview.SchemeName = SelectedTerminologyScheme.DisplayName;
        TerminologyPreview.Groups.Clear();

        foreach (var group in TerminologyGroups)
        {
            var previewLines = new ObservableCollection<TerminologyPreviewLineState>(
                group.Fields.Select(field => new TerminologyPreviewLineState(field.Label, field.Value)));
            TerminologyPreview.Groups.Add(new TerminologyPreviewGroupState(group.Title, previewLines));
        }
    }

    private void ApplySelectedTerminology()
    {
        if (SelectedTerminologyScheme is null)
        {
            return;
        }

        var profile = BuildTerminologyProfile(SelectedTerminologyScheme);
        SelectedTerminologyScheme.SavedProfile = profile;
        _textService.SetProfile(profile);

        foreach (var item in TerminologyItems)
        {
            item.IsApplied = item == SelectedTerminologyScheme;
        }

        AppliedState.ActiveTerminologyName = SelectedTerminologyScheme.DisplayName;
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsTerminologyAppliedFeedbackPattern), SelectedTerminologyScheme.DisplayName);
    }

    private void CopySelectedTerminology()
    {
        if (SelectedTerminologyScheme is null)
        {
            return;
        }

        var profile = BuildTerminologyProfile(SelectedTerminologyScheme);
        var schemeName = string.Format(_textService.Resolve(TextTokens.SettingsTerminologyCopyNamePattern), _customTerminologyCounter);
        var copied = new TerminologySchemeSummaryState(
            $"terminology-custom-{_customTerminologyCounter}",
            schemeName,
            _textService.Resolve(TextTokens.SettingsTerminologyCopyDescription),
            _textService.Resolve(TextTokens.SettingsTerminologyCustomTag),
            false,
            CloneProfile(profile, $"terminology-custom-{_customTerminologyCounter}", schemeName),
            null);

        TerminologyItems.Add(copied);
        _customTerminologyCounter++;
        SelectTerminologyScheme(copied.Id);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsTerminologyCopyFeedbackPattern), copied.DisplayName);
    }

    private void CreateCustomTerminologyFromCurrent()
    {
        var schemeName = string.Format(_textService.Resolve(TextTokens.SettingsTerminologyNewNamePattern), _customTerminologyCounter);
        var custom = new TerminologySchemeSummaryState(
            $"terminology-custom-{_customTerminologyCounter}",
            schemeName,
            _textService.Resolve(TextTokens.SettingsTerminologyNewDescription),
            _textService.Resolve(TextTokens.SettingsTerminologyCustomTag),
            false,
            CloneProfile(SelectedTerminologyScheme?.SavedProfile ?? _textService.ActiveProfile, $"terminology-custom-{_customTerminologyCounter}", schemeName),
            null);

        TerminologyItems.Add(custom);
        _customTerminologyCounter++;
        SelectTerminologyScheme(custom.Id);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsTerminologyNewFeedbackPattern), custom.DisplayName);
    }

    private void SaveTerminologyChanges()
    {
        if (SelectedTerminologyScheme is null)
        {
            return;
        }

        SelectedTerminologyScheme.SavedProfile = BuildTerminologyProfile(SelectedTerminologyScheme);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsTerminologySaveFeedbackPattern), SelectedTerminologyScheme.DisplayName);
    }

    private void SaveTerminologyAsNew()
    {
        if (SelectedTerminologyScheme is null)
        {
            return;
        }

        var profile = BuildTerminologyProfile(SelectedTerminologyScheme);
        var schemeName = string.Format(_textService.Resolve(TextTokens.SettingsTerminologySaveAsNamePattern), _customTerminologyCounter);
        var custom = new TerminologySchemeSummaryState(
            $"terminology-custom-{_customTerminologyCounter}",
            schemeName,
            _textService.Resolve(TextTokens.SettingsTerminologySaveAsDescription),
            _textService.Resolve(TextTokens.SettingsTerminologyCustomTag),
            false,
            CloneProfile(profile, $"terminology-custom-{_customTerminologyCounter}", schemeName),
            null);

        TerminologyItems.Add(custom);
        _customTerminologyCounter++;
        SelectTerminologyScheme(custom.Id);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsTerminologySaveAsFeedbackPattern), custom.DisplayName);
    }

    private void RestoreDefaultTerminology()
    {
        var defaultScheme = TerminologyItems.First(item => item.Id == DefaultTerminologyProfileId);
        if (defaultScheme.PresetProfile is not null)
        {
            defaultScheme.SavedProfile = CloneProfile(defaultScheme.PresetProfile, defaultScheme.Id, defaultScheme.DisplayName);
        }

        SelectTerminologyScheme(defaultScheme.Id);
        AppliedState.StatusText = string.Format(_textService.Resolve(TextTokens.SettingsTerminologyRestoreFeedbackPattern), defaultScheme.DisplayName);
    }

    private TerminologyProfile BuildTerminologyProfile(TerminologySchemeSummaryState scheme)
    {
        var textEntries = new Dictionary<string, string>(scheme.SavedProfile.TextEntries, StringComparer.Ordinal);
        foreach (var field in _terminologyFields.Values)
        {
            if (!string.IsNullOrWhiteSpace(field.Value))
            {
                textEntries[field.TokenKey] = field.Value;
            }
        }

        return new TerminologyProfile(
            scheme.Id,
            scheme.DisplayName,
            textEntries,
            new Dictionary<string, string>(scheme.SavedProfile.Variables, StringComparer.Ordinal));
    }

    private string DescribeTerminologyProfile(string profileId)
    {
        return profileId switch
        {
            "security" => _textService.Resolve(TextTokens.SettingsTerminologySecurityDescription),
            "tourism" => _textService.Resolve(TextTokens.SettingsTerminologyTourismDescription),
            _ => _textService.Resolve(TextTokens.SettingsTerminologyTelecomDescription)
        };
    }

    private static TerminologyProfile CloneProfile(TerminologyProfile source, string id, string name)
    {
        return new TerminologyProfile(
            id,
            name,
            new Dictionary<string, string>(source.TextEntries, StringComparer.Ordinal),
            new Dictionary<string, string>(source.Variables, StringComparer.Ordinal));
    }
}
