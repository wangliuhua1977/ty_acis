using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using TianyiVision.Acis.Services.Devices;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.UI.States;
using DrawingColor = System.Drawing.Color;

namespace TianyiVision.Acis.UI.Views.Controls;

public partial class RealMapHost : UserControl
{
    private static readonly DrawingColor StageBackgroundColor = DrawingColor.FromArgb(0x07, 0x11, 0x1f);
    private static readonly object ColorRuleSyncRoot = new();
    private static readonly Dictionary<string, string> ColorRuleSignatures = new(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<MapPointColorCategory, MapVisualColorRule> VisualColorRules =
        new Dictionary<MapPointColorCategory, MapVisualColorRule>
        {
            [MapPointColorCategory.Online] = new("Theme.SuccessBrush", "map.point.online", "#34D6A2"),
            [MapPointColorCategory.Fault] = new("Theme.DangerBrush", "map.point.fault", "#FF4D4F"),
            [MapPointColorCategory.Warning] = new("Theme.WarningBrush", "map.point.warning", "#C99026"),
            [MapPointColorCategory.Neutral] = new("Theme.TextSecondaryBrush", "map.point.neutral", "#7F8EA3")
        };
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<MapPointState>),
            typeof(RealMapHost),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedPointIdProperty =
        DependencyProperty.Register(
            nameof(SelectedPointId),
            typeof(string),
            typeof(RealMapHost),
            new PropertyMetadata(string.Empty, OnHostStateChanged));

    public static readonly DependencyProperty PointSelectedCommandProperty =
        DependencyProperty.Register(
            nameof(PointSelectedCommand),
            typeof(ICommand),
            typeof(RealMapHost),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ApiKeyProperty =
        DependencyProperty.Register(
            nameof(ApiKey),
            typeof(string),
            typeof(RealMapHost),
            new PropertyMetadata(string.Empty, OnConfigurationChanged));

    public static readonly DependencyProperty SecurityJsCodeProperty =
        DependencyProperty.Register(
            nameof(SecurityJsCode),
            typeof(string),
            typeof(RealMapHost),
            new PropertyMetadata(string.Empty, OnConfigurationChanged));

    public static readonly DependencyProperty ApiVersionProperty =
        DependencyProperty.Register(
            nameof(ApiVersion),
            typeof(string),
            typeof(RealMapHost),
            new PropertyMetadata("2.0", OnConfigurationChanged));

    public static readonly DependencyProperty DefaultCenterLongitudeProperty =
        DependencyProperty.Register(
            nameof(DefaultCenterLongitude),
            typeof(double),
            typeof(RealMapHost),
            new PropertyMetadata(103.761263d, OnConfigurationChanged));

    public static readonly DependencyProperty DefaultCenterLatitudeProperty =
        DependencyProperty.Register(
            nameof(DefaultCenterLatitude),
            typeof(double),
            typeof(RealMapHost),
            new PropertyMetadata(29.552997d, OnConfigurationChanged));

    public static readonly DependencyProperty DefaultZoomProperty =
        DependencyProperty.Register(
            nameof(DefaultZoom),
            typeof(int),
            typeof(RealMapHost),
            new PropertyMetadata(11, OnConfigurationChanged));

    public static readonly DependencyProperty HostContextProperty =
        DependencyProperty.Register(
            nameof(HostContext),
            typeof(string),
            typeof(RealMapHost),
            new PropertyMetadata("Unknown"));

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private INotifyCollectionChanged? _collectionChangedSource;
    private readonly Dictionary<MapPointState, PropertyChangedEventHandler> _itemHandlers = [];
    private readonly Dictionary<string, string> _pointAuditSignatures = new(StringComparer.Ordinal);
    private bool _isInitialized;
    private bool _isMapReady;
    private bool _isReloading;

    public RealMapHost()
    {
        InitializeComponent();
        MapWebView.DefaultBackgroundColor = StageBackgroundColor;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<MapAvailabilityChangedEventArgs>? AvailabilityChanged;

    public IEnumerable<MapPointState>? ItemsSource
    {
        get => (IEnumerable<MapPointState>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string SelectedPointId
    {
        get => (string)GetValue(SelectedPointIdProperty);
        set => SetValue(SelectedPointIdProperty, value);
    }

    public ICommand? PointSelectedCommand
    {
        get => (ICommand?)GetValue(PointSelectedCommandProperty);
        set => SetValue(PointSelectedCommandProperty, value);
    }

    public string ApiKey
    {
        get => (string)GetValue(ApiKeyProperty);
        set => SetValue(ApiKeyProperty, value);
    }

    public string SecurityJsCode
    {
        get => (string)GetValue(SecurityJsCodeProperty);
        set => SetValue(SecurityJsCodeProperty, value);
    }

    public string ApiVersion
    {
        get => (string)GetValue(ApiVersionProperty);
        set => SetValue(ApiVersionProperty, value);
    }

    public double DefaultCenterLongitude
    {
        get => (double)GetValue(DefaultCenterLongitudeProperty);
        set => SetValue(DefaultCenterLongitudeProperty, value);
    }

    public double DefaultCenterLatitude
    {
        get => (double)GetValue(DefaultCenterLatitudeProperty);
        set => SetValue(DefaultCenterLatitudeProperty, value);
    }

    public int DefaultZoom
    {
        get => (int)GetValue(DefaultZoomProperty);
        set => SetValue(DefaultZoomProperty, value);
    }

    public string HostContext
    {
        get => (string)GetValue(HostContextProperty);
        set => SetValue(HostContextProperty, value);
    }

    public async Task<bool> CapturePreviewAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        await EnsureInitializedAsync();
        if (!_isMapReady || MapWebView.CoreWebView2 is null)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await MapWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        await stream.FlushAsync();
        return true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachItemsSource(ItemsSource);
        await EnsureInitializedAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachItemsSource();
        if (MapWebView.CoreWebView2 is not null)
        {
            MapWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RealMapHost host)
        {
            return;
        }

        host.DetachItemsSource();
        host.AttachItemsSource(e.NewValue as IEnumerable<MapPointState>);
        host.QueueStateSync();
    }

    private static void OnHostStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RealMapHost host)
        {
            host.QueueStateSync();
        }
    }

    private static async void OnConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RealMapHost host && host.IsLoaded)
        {
            await host.ReloadMapAsync();
        }
    }

    private async Task ReloadMapAsync()
    {
        if (_isReloading)
        {
            return;
        }

        _isReloading = true;
        _isInitialized = false;
        _isMapReady = false;

        try
        {
            MapWebView.Visibility = Visibility.Collapsed;
            await EnsureInitializedAsync();
        }
        finally
        {
            _isReloading = false;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            QueueStateSync();
            return;
        }

        if (!HasUsableConfiguration())
        {
            NotifyAvailability(false, "map_config_missing");
            return;
        }

        try
        {
            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.DefaultBackgroundColor = StageBackgroundColor;
            MapWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            MapWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            MapWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            MapWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            MapWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            MapWebView.NavigateToString(MapHostHtmlBuilder.Build(new MapHostBootSettings(
                ApiKey,
                SecurityJsCode,
                string.IsNullOrWhiteSpace(ApiVersion) ? "2.0" : ApiVersion,
                DefaultCenterLongitude,
                DefaultCenterLatitude,
                DefaultZoom <= 0 ? 11 : DefaultZoom,
                NormalizeHostContext())));
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            NotifyAvailability(false, ex.Message);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var message = JsonDocument.Parse(e.WebMessageAsJson);
        if (!message.RootElement.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        var type = typeElement.GetString() ?? string.Empty;
        switch (type)
        {
            case "ready":
                _isMapReady = true;
                MapWebView.Visibility = Visibility.Visible;
                MapPointSourceDiagnostics.Write("MapRender", $"{NormalizeHostContext()} map_ready");
                NotifyAvailability(true, "map_ready");
                QueueStateSync();
                break;
            case "pointSelected":
                var pointId = message.RootElement.TryGetProperty("payload", out var payloadElement)
                    ? payloadElement.GetString()
                    : null;
                if (!string.IsNullOrWhiteSpace(pointId)
                    && PointSelectedCommand?.CanExecute(pointId) == true)
                {
                    PointSelectedCommand.Execute(pointId);
                }
                break;
            case "error":
                _isMapReady = false;
                var mapError = message.RootElement.TryGetProperty("payload", out var errorElement)
                    ? errorElement.GetString()
                    : "map_error";
                MapPointSourceDiagnostics.Write("MapRender", $"{NormalizeHostContext()} error = {mapError}");
                NotifyAvailability(false, mapError);
                break;
            case "diagnostic":
                HandleDiagnosticMessage(message.RootElement);
                break;
        }
    }

    private void QueueStateSync()
    {
        if (!_isMapReady || MapWebView.CoreWebView2 is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(SendStateToMap));
    }

    private void SendStateToMap()
    {
        if (!_isMapReady || MapWebView.CoreWebView2 is null)
        {
            return;
        }

        var points = (ItemsSource ?? []).ToList();
        var stageBackground = ResolveColor("Theme.WorkbenchBackgroundBrush", "#07111f");
        var textPrimary = ResolveColor("Theme.TextPrimaryBrush", "#e8eef9");
        var labelBackground = ResolveColor("Theme.PanelBackgroundBrush", "#162235");
        var online = ResolveColorRule(MapPointColorCategory.Online);
        var fault = ResolveColorRule(MapPointColorCategory.Fault);
        var pending = ResolveColorRule(MapPointColorCategory.Warning);
        var neutral = ResolveColorRule(MapPointColorCategory.Neutral);
        var selected = ResolveColor("Theme.TextPrimaryBrush", "#ffffff");

        MapPointSourceDiagnostics.Write("MapRender", $"{NormalizeHostContext()} incomingPointCount = {points.Count}");
        LogColorRuleUsage(stageBackground, textPrimary, labelBackground, online.ColorValue, fault.ColorValue, pending.ColorValue, neutral.ColorValue, selected);
        LogPointRenderAudit(points);

        var payload = new
        {
            type = "state",
            hostContext = NormalizeHostContext(),
            selectedPointId = SelectedPointId,
            points = points
                .Select(point => new
                {
                    pointId = point.PointId,
                    pointName = point.PointName,
                    mapLongitude = point.MapLongitude,
                    mapLatitude = point.MapLatitude,
                    rawLongitude = point.RawLongitude,
                    rawLatitude = point.RawLatitude,
                    registeredLongitude = point.RegisteredLongitude,
                    registeredLatitude = point.RegisteredLatitude,
                    coordinateSystem = point.RegisteredCoordinateSystem.ToString(),
                    mapCoordinateSystem = point.MapCoordinateSystem.ToString(),
                    coordinateStatusEnum = point.CoordinateStatus.ToString(),
                    canRenderOnMap = point.CanRenderOnMap,
                    coordinateStatusText = point.CoordinateStatusText,
                    mapSource = point.MapSource,
                    businessSummaryCoordinateStatus = point.BusinessSummaryCoordinateStatus,
                    finalRenderable = ResolveFinalRenderable(point),
                    visualKind = point.VisualKind.ToString(),
                    colorCategory = point.ColorCategory.ToString(),
                    colorRuleKey = ResolveColorRule(point.ColorCategory).ColorRuleKey,
                    isSelected = point.IsSelected || string.Equals(point.PointId, SelectedPointId, StringComparison.Ordinal),
                    isCurrent = point.IsCurrent
                })
                .ToList(),
            theme = new
            {
                stageBackground,
                textPrimary,
                labelBackground,
                online = online.ColorValue,
                fault = fault.ColorValue,
                pending = pending.ColorValue,
                neutral = neutral.ColorValue,
                selected
            }
        };

        MapWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, _jsonOptions));
    }

    private void HandleDiagnosticMessage(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("payload", out var payloadElement)
            || payloadElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var stage = payloadElement.TryGetProperty("stage", out var stageElement)
            ? stageElement.GetString()
            : "MapRender";
        var message = payloadElement.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        MapPointSourceDiagnostics.Write(stage ?? "MapRender", $"{NormalizeHostContext()} {message}");
    }

    private void AttachItemsSource(IEnumerable<MapPointState>? items)
    {
        if (items is null)
        {
            return;
        }

        if (items is INotifyCollectionChanged collectionChanged)
        {
            _collectionChangedSource = collectionChanged;
            _collectionChangedSource.CollectionChanged += OnItemsCollectionChanged;
        }

        foreach (var item in items)
        {
            AttachItem(item);
        }
    }

    private void DetachItemsSource()
    {
        if (_collectionChangedSource is not null)
        {
            _collectionChangedSource.CollectionChanged -= OnItemsCollectionChanged;
            _collectionChangedSource = null;
        }

        foreach (var pair in _itemHandlers.ToList())
        {
            pair.Key.PropertyChanged -= pair.Value;
        }

        _itemHandlers.Clear();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<MapPointState>().ToList())
            {
                DetachItem(item);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<MapPointState>())
            {
                AttachItem(item);
            }
        }

        QueueStateSync();
    }

    private void AttachItem(MapPointState item)
    {
        if (_itemHandlers.ContainsKey(item))
        {
            return;
        }

        PropertyChangedEventHandler handler = (_, _) => QueueStateSync();
        item.PropertyChanged += handler;
        _itemHandlers[item] = handler;
    }

    private void DetachItem(MapPointState item)
    {
        if (!_itemHandlers.TryGetValue(item, out var handler))
        {
            return;
        }

        item.PropertyChanged -= handler;
        _itemHandlers.Remove(item);
    }

    private bool HasUsableConfiguration()
    {
        return !string.IsNullOrWhiteSpace(ApiKey)
            && !ApiKey.Contains("your-", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(SecurityJsCode)
            && !SecurityJsCode.Contains("your-", StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyAvailability(bool isAvailable, string? reason)
    {
        AvailabilityChanged?.Invoke(this, new MapAvailabilityChangedEventArgs(isAvailable, reason ?? string.Empty));
    }

    private static string ResolveColor(string resourceKey, string fallback)
    {
        if (Application.Current.TryFindResource(resourceKey) is SolidColorBrush brush)
        {
            return brush.Color.ToString();
        }

        return fallback;
    }

    private void LogColorRuleUsage(
        string stageBackground,
        string textPrimary,
        string labelBackground,
        string online,
        string fault,
        string pending,
        string neutral,
        string selected)
    {
        var signature = string.Join(
            "|",
            [
                stageBackground,
                textPrimary,
                labelBackground,
                online,
                fault,
                pending,
                neutral,
                selected
            ]);

        lock (ColorRuleSyncRoot)
        {
            ColorRuleSignatures[NormalizeHostContext()] = signature;
            var sharedAcrossContexts = ColorRuleSignatures.Count < 2
                || ColorRuleSignatures.Values.Distinct(StringComparer.Ordinal).Count() == 1;
            MapPointSourceDiagnostics.Write(
                "MapRender",
                $"{NormalizeHostContext()} sharedColorRule = {sharedAcrossContexts}, contexts = {string.Join(", ", ColorRuleSignatures.Keys.OrderBy(keyName => keyName, StringComparer.Ordinal))}");
        }
    }

    private string NormalizeHostContext()
    {
        return string.IsNullOrWhiteSpace(HostContext) ? "Unknown" : HostContext.Trim();
    }

    private static MapVisualColorRule ResolveColorRule(MapPointColorCategory colorCategory)
    {
        return VisualColorRules.TryGetValue(colorCategory, out var rule)
            ? rule with { ColorValue = rule.FallbackColor }
            : new MapVisualColorRule("Theme.SuccessBrush", "map.point.online", "#34D6A2", "#34D6A2");
    }

    private void LogPointRenderAudit(IEnumerable<MapPointState> points)
    {
        foreach (var point in points)
        {
            var colorRule = ResolveColorRule(point.ColorCategory);
            var signature = string.Join(
                "|",
                [
                    NormalizeLogValue(point.RawLongitude),
                    NormalizeLogValue(point.RawLatitude),
                    point.CoordinateStatus.ToString(),
                    NormalizeLogValue(point.CoordinateStatusText),
                    point.CanRenderOnMap.ToString(),
                    FormatNullableDouble(point.MapLongitude),
                    FormatNullableDouble(point.MapLatitude),
                    NormalizeLogValue(point.MapSource),
                    NormalizeLogValue(point.BusinessSummaryCoordinateStatus),
                    ResolveFinalRenderable(point).ToString(),
                    point.VisualKind.ToString(),
                    point.ColorCategory.ToString(),
                    colorRule.ColorRuleKey
                ]);

            var auditKey = $"{NormalizeHostContext()}::{point.PointId}";
            if (_pointAuditSignatures.TryGetValue(auditKey, out var previousSignature)
                && string.Equals(previousSignature, signature, StringComparison.Ordinal))
            {
                continue;
            }

            _pointAuditSignatures[auditKey] = signature;
            MapPointSourceDiagnostics.Write(
                "MapRenderPoint",
                $"{NormalizeHostContext()} pointId = {NormalizeLogValue(point.PointId)}, deviceCode = {NormalizeLogValue(point.DeviceCode)}, rawLongitude = {NormalizeLogValue(point.RawLongitude)}, rawLatitude = {NormalizeLogValue(point.RawLatitude)}, coordinateSystem = {point.RegisteredCoordinateSystem}, parsedLongitude = {FormatNullableDouble(point.RegisteredLongitude)}, parsedLatitude = {FormatNullableDouble(point.RegisteredLatitude)}, coordinateStatusEnum = {point.CoordinateStatus}, coordinateStatusText = {NormalizeLogValue(point.CoordinateStatusText)}, canRenderOnMap = {point.CanRenderOnMap}, mapLongitude = {FormatNullableDouble(point.MapLongitude)}, mapLatitude = {FormatNullableDouble(point.MapLatitude)}, mapSource = {NormalizeLogValue(point.MapSource)}, businessSummaryCoordinateStatus = {NormalizeLogValue(point.BusinessSummaryCoordinateStatus)}, finalRenderable = {ResolveFinalRenderable(point)}, visualState = {point.VisualKind}, colorCategory = {point.ColorCategory}, colorRuleKey = {colorRule.ColorRuleKey}");
        }
    }

    private static bool ResolveFinalRenderable(MapPointState point)
    {
        return point.CanRenderOnMap
            && ((point.MapLongitude.HasValue && point.MapLatitude.HasValue)
                || (point.RegisteredLongitude.HasValue && point.RegisteredLatitude.HasValue));
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
            : "null";
    }

    private static string NormalizeLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : value.Trim();
    }

    private sealed record MapHostBootSettings(
        string ApiKey,
        string SecurityJsCode,
        string ApiVersion,
        double DefaultCenterLongitude,
        double DefaultCenterLatitude,
        int DefaultZoom,
        string HostContext);

    private sealed record MapVisualColorRule(
        string ResourceKey,
        string ColorRuleKey,
        string FallbackColor,
        string ColorValue = "");

    private static class MapHostHtmlBuilder
    {
        private const string Placeholder = "__BOOT_CONFIG__";

        public static string Build(MapHostBootSettings settings)
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "RealMapHostTemplate.html");
            if (!File.Exists(templatePath))
            {
                return "<html><body><script>window.chrome.webview.postMessage({type:'error',payload:'map_template_missing'});</script></body></html>";
            }

            var template = File.ReadAllText(templatePath);
            return template.Replace(
                Placeholder,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                StringComparison.Ordinal);
        }
    }
}

public sealed class MapAvailabilityChangedEventArgs : EventArgs
{
    public MapAvailabilityChangedEventArgs(bool isAvailable, string reason)
    {
        IsAvailable = isAvailable;
        Reason = reason;
    }

    public bool IsAvailable { get; }

    public string Reason { get; }
}
