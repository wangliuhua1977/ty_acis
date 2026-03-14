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
using TianyiVision.Acis.UI.States;

namespace TianyiVision.Acis.UI.Views.Controls;

public partial class RealMapHost : UserControl
{
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

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private INotifyCollectionChanged? _collectionChangedSource;
    private readonly Dictionary<MapPointState, PropertyChangedEventHandler> _itemHandlers = [];
    private bool _isInitialized;
    private bool _isMapReady;
    private bool _isReloading;

    public RealMapHost()
    {
        InitializeComponent();
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
                DefaultZoom <= 0 ? 11 : DefaultZoom)));
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
                NotifyAvailability(false, message.RootElement.TryGetProperty("payload", out var errorElement)
                    ? errorElement.GetString()
                    : "map_error");
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

        var payload = new
        {
            type = "state",
            selectedPointId = SelectedPointId,
            points = (ItemsSource ?? [])
                .Where(point => point.CanRenderOnMap)
                .Select(point => new
                {
                    pointId = point.PointId,
                    pointName = point.PointName,
                    longitude = point.Longitude,
                    latitude = point.Latitude,
                    visualKind = point.VisualKind.ToString(),
                    isSelected = point.IsSelected || string.Equals(point.PointId, SelectedPointId, StringComparison.Ordinal),
                    isCurrent = point.IsCurrent
                })
                .ToList(),
            theme = new
            {
                stageBackground = ResolveColor("Theme.WorkbenchBackgroundBrush", "#07111f"),
                textPrimary = ResolveColor("Theme.TextPrimaryBrush", "#e8eef9"),
                panelBackground = ResolveColor("Theme.PanelBackgroundBrush", "#162235"),
                normal = ResolveColor("Theme.SuccessBrush", "#5fbf87"),
                fault = ResolveColor("Theme.DangerBrush", "#ff6b6b"),
                key = ResolveColor("Theme.WarningBrush", "#f7b955"),
                inspecting = ResolveColor("Theme.InspectionActiveBrush", "#4db2ff"),
                silent = ResolveColor("Theme.TextSecondaryBrush", "#94a7bd"),
                paused = ResolveColor("Theme.WarningBrush", "#f08b62"),
                selected = ResolveColor("Theme.TextPrimaryBrush", "#ffffff")
            }
        };

        MapWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, _jsonOptions));
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

    private sealed record MapHostBootSettings(
        string ApiKey,
        string SecurityJsCode,
        string ApiVersion,
        double DefaultCenterLongitude,
        double DefaultCenterLatitude,
        int DefaultZoom);

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
