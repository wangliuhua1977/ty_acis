using System.Collections.ObjectModel;
using TianyiVision.Acis.Core.Localization;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class TerminologySchemeSummaryState : ViewModelBase
{
    private bool _isSelected;
    private bool _isApplied;

    public TerminologySchemeSummaryState(
        string id,
        string displayName,
        string description,
        string kindLabel,
        bool isPreset,
        TerminologyProfile savedProfile,
        TerminologyProfile? presetProfile)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        KindLabel = kindLabel;
        IsPreset = isPreset;
        SavedProfile = savedProfile;
        PresetProfile = presetProfile;
    }

    public string Id { get; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public string KindLabel { get; set; }

    public bool IsPreset { get; }

    public TerminologyProfile SavedProfile { get; set; }

    public TerminologyProfile? PresetProfile { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsApplied
    {
        get => _isApplied;
        set => SetProperty(ref _isApplied, value);
    }
}

public sealed class TerminologyFieldState : ViewModelBase
{
    private string _value;

    public TerminologyFieldState(string tokenKey, string label, string value)
    {
        TokenKey = tokenKey;
        Label = label;
        _value = value;
    }

    public string TokenKey { get; }

    public string Label { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

public sealed class TerminologyGroupState
{
    public TerminologyGroupState(string key, string title, ObservableCollection<TerminologyFieldState> fields)
    {
        Key = key;
        Title = title;
        Fields = fields;
    }

    public string Key { get; }

    public string Title { get; }

    public ObservableCollection<TerminologyFieldState> Fields { get; }
}

public sealed class TerminologyPreviewLineState
{
    public TerminologyPreviewLineState(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }
}

public sealed class TerminologyPreviewGroupState
{
    public TerminologyPreviewGroupState(string title, ObservableCollection<TerminologyPreviewLineState> lines)
    {
        Title = title;
        Lines = lines;
    }

    public string Title { get; }

    public ObservableCollection<TerminologyPreviewLineState> Lines { get; }
}

public sealed class TerminologyPreviewState : ViewModelBase
{
    private string _schemeName = string.Empty;

    public ObservableCollection<TerminologyPreviewGroupState> Groups { get; } = [];

    public string SchemeName
    {
        get => _schemeName;
        set => SetProperty(ref _schemeName, value);
    }
}
