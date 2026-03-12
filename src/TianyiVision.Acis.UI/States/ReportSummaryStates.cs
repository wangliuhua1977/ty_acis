using System.Collections.ObjectModel;
using TianyiVision.Acis.UI.Mvvm;

namespace TianyiVision.Acis.UI.States;

public sealed class ReportMetricSummaryState : ViewModelBase
{
    private string _value = string.Empty;
    private string _description = string.Empty;
    private bool _isSelected;

    public ReportMetricSummaryState(string key, string title, ReportViewKind relatedReportView)
    {
        Key = key;
        Title = title;
        RelatedReportView = relatedReportView;
    }

    public string Key { get; }

    public string Title { get; }

    public ReportViewKind RelatedReportView { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ReportChartSummaryState : ViewModelBase
{
    private string _footerText = string.Empty;

    public ReportChartSummaryState(string title, string description, string primaryLegend, string secondaryLegend = "")
    {
        Title = title;
        Description = description;
        PrimaryLegend = primaryLegend;
        SecondaryLegend = secondaryLegend;
    }

    public string Title { get; }

    public string Description { get; }

    public string PrimaryLegend { get; }

    public string SecondaryLegend { get; }

    public ObservableCollection<ReportTrendPointState> TrendPoints { get; } = [];

    public ObservableCollection<ReportDistributionPointState> DistributionPoints { get; } = [];

    public ObservableCollection<ReportDualTrendPointState> DualTrendPoints { get; } = [];

    public string FooterText
    {
        get => _footerText;
        set => SetProperty(ref _footerText, value);
    }
}

public sealed record ReportTrendPointState(string Label, string ValueText, double VisualHeight);

public sealed record ReportDistributionPointState(string Label, string ValueText, string ShareText, double VisualWidth);

public sealed record ReportDualTrendPointState(
    string Label,
    string PrimaryValueText,
    double PrimaryVisualHeight,
    string SecondaryValueText,
    double SecondaryVisualHeight);

public sealed class ReportExportActionState : ViewModelBase
{
    private bool _isHighlighted;

    public ReportExportActionState(string key, string title, string description)
    {
        Key = key;
        Title = title;
        Description = description;
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }
}
