using System.Collections.ObjectModel;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public enum ReportTimeRange
{
    Today,
    ThisWeek,
    ThisMonth,
    Custom
}

public sealed class ReportFilterOptionState : ViewModelBase
{
    public ReportFilterOptionState(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }
}

public sealed class ReportFilterState : ViewModelBase
{
    private ReportFilterOptionState? _selectedTimeRangeOption;
    private ReportFilterOptionState? _selectedGroupOption;
    private ReportFilterOptionState? _selectedUnitOption;
    private ReportFilterOptionState? _selectedFaultTypeOption;

    public ReportFilterState(
        ObservableCollection<ReportFilterOptionState> timeRangeOptions,
        ObservableCollection<ReportFilterOptionState> groupOptions,
        ObservableCollection<ReportFilterOptionState> unitOptions,
        ObservableCollection<ReportFilterOptionState> faultTypeOptions)
    {
        TimeRangeOptions = timeRangeOptions;
        GroupOptions = groupOptions;
        UnitOptions = unitOptions;
        FaultTypeOptions = faultTypeOptions;
        _selectedTimeRangeOption = timeRangeOptions.FirstOrDefault();
        _selectedGroupOption = groupOptions.FirstOrDefault();
        _selectedUnitOption = unitOptions.FirstOrDefault();
        _selectedFaultTypeOption = faultTypeOptions.FirstOrDefault();
    }

    public ObservableCollection<ReportFilterOptionState> TimeRangeOptions { get; }

    public ObservableCollection<ReportFilterOptionState> GroupOptions { get; }

    public ObservableCollection<ReportFilterOptionState> UnitOptions { get; }

    public ObservableCollection<ReportFilterOptionState> FaultTypeOptions { get; }

    public ReportFilterOptionState? SelectedTimeRangeOption
    {
        get => _selectedTimeRangeOption;
        set => SetProperty(ref _selectedTimeRangeOption, value);
    }

    public ReportFilterOptionState? SelectedGroupOption
    {
        get => _selectedGroupOption;
        set => SetProperty(ref _selectedGroupOption, value);
    }

    public ReportFilterOptionState? SelectedUnitOption
    {
        get => _selectedUnitOption;
        set => SetProperty(ref _selectedUnitOption, value);
    }

    public ReportFilterOptionState? SelectedFaultTypeOption
    {
        get => _selectedFaultTypeOption;
        set => SetProperty(ref _selectedFaultTypeOption, value);
    }
}
