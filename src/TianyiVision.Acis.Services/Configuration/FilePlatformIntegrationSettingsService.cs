using System.Text.Json;
using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Configuration;

public sealed class FilePlatformIntegrationSettingsService : IPlatformIntegrationSettingsService
{
    private const double DefaultMapCenterLongitude = 103.761263d;
    private const double DefaultMapCenterLatitude = 29.552997d;
    private const int DefaultMapZoom = 11;

    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FilePlatformIntegrationSettingsService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public PlatformIntegrationSettings Load()
    {
        var settings = _documentStore.LoadOrCreate(_paths.CtyunIntegrationFile, CreateDefaultSettings);
        var bundledMapProvider = LoadBundledMapProviderDefaults();
        var normalized = settings with
        {
            OpenPlatform = Normalize(settings.OpenPlatform),
            MapProvider = Normalize(settings.MapProvider, bundledMapProvider)
        };

        _documentStore.Save(_paths.CtyunIntegrationFile, normalized);
        return normalized;
    }

    private PlatformIntegrationSettings CreateDefaultSettings()
    {
        var settings = CreateFallbackSettings();

        var templatePath = FindBundledTemplateFile("appsettings.json");
        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            try
            {
                using var stream = File.OpenRead(templatePath);
                var template = JsonSerializer.Deserialize<CtyunTemplateDocument>(stream);
                if (template?.OpenPlatform is not null && template.MapProvider is not null)
                {
                    settings = new PlatformIntegrationSettings(
                        new OpenPlatformSettings(
                            template.OpenPlatform.ServiceMode ?? "AutoFallback",
                            template.OpenPlatform.EnableDemoFallback ?? true,
                            template.OpenPlatform.BaseUrl ?? string.Empty,
                            template.OpenPlatform.AppId ?? string.Empty,
                            template.OpenPlatform.AppKey ?? string.Empty,
                            template.OpenPlatform.AppSecret ?? string.Empty,
                            template.OpenPlatform.RsaPrivateKey ?? string.Empty,
                            template.OpenPlatform.Version ?? "1.1",
                            template.OpenPlatform.ApiVersion ?? "2.0",
            template.OpenPlatform.ClientType ?? "3",
                            template.OpenPlatform.GrantType ?? "vcp_189",
                            template.OpenPlatform.EnterpriseUser ?? string.Empty,
                            template.OpenPlatform.ParentUser ?? string.Empty,
                            new OpenPlatformTokenSettings(
                                template.OpenPlatform.Token?.AccessTokenPath ?? "/open/oauth/getAccessToken",
                                template.OpenPlatform.Token?.RefreshGrantType ?? "refresh_token",
                                template.OpenPlatform.Token?.ReuseBeforeExpirySeconds ?? 300),
                            new OpenPlatformDeviceApiSettings(
                                template.OpenPlatform.DeviceApi?.DeviceListPath ?? "/open/token/device/getAllDeviceListNew",
                                template.OpenPlatform.DeviceApi?.DeviceDetailPath ?? "/open/token/device/getCusDeviceByDeviceCode",
                                template.OpenPlatform.DeviceApi?.PageSize ?? 12,
                                template.OpenPlatform.DeviceApi?.InitialLastId ?? 0,
                                template.OpenPlatform.DeviceApi?.HasChildDevices ?? 0,
                                template.OpenPlatform.DeviceApi?.DetailEnrichmentLimit ?? 12),
                            new OpenPlatformAlarmApiSettings(
                                template.OpenPlatform.AlarmApi?.AiAlertListPath ?? "/open/token/AIAlarm/getAlertInfoList",
                                template.OpenPlatform.AlarmApi?.DeviceAlertListPath ?? "/open/token/device/getDeviceAlarmMessage",
                                template.OpenPlatform.AlarmApi?.PageNo ?? 1,
                                template.OpenPlatform.AlarmApi?.PageSize ?? 20,
                                template.OpenPlatform.AlarmApi?.AiAlertSource ?? 1,
                                template.OpenPlatform.AlarmApi?.DeviceAlertSource ?? 1,
                                template.OpenPlatform.AlarmApi?.AiAlertTypeList ?? "3",
                                template.OpenPlatform.AlarmApi?.DeviceAlertTypeList ?? "1,2,10,11",
                                template.OpenPlatform.AlarmApi?.LookbackHours ?? 72)),
                        new MapProviderSettings(
                            template.MapProvider.AmapWebJsApiKey ?? string.Empty,
                            template.MapProvider.AmapSecurityJsCode ?? string.Empty,
                            template.MapProvider.AmapJsApiVersion ?? "2.0",
                            template.MapProvider.CoordinateSystem ?? "GCJ-02",
                            template.MapProvider.DefaultCenterLongitude ?? DefaultMapCenterLongitude,
                            template.MapProvider.DefaultCenterLatitude ?? DefaultMapCenterLatitude,
                            template.MapProvider.DefaultZoom ?? DefaultMapZoom,
                            template.MapProvider.AmapWebServiceApiKey ?? string.Empty,
                            template.MapProvider.EnableCoordinateConversion ?? true,
                            template.MapProvider.EnableJsCoordinateFallback ?? false));
                }
            }
            catch
            {
                // Fall back to placeholder settings when the bundled template is unreadable.
            }
        }

        var localOverridePath = FindBundledTemplateFile("appsettings.local.json");
        if (!string.IsNullOrWhiteSpace(localOverridePath))
        {
            try
            {
                using var stream = File.OpenRead(localOverridePath);
                var localOverride = JsonSerializer.Deserialize<LocalSettingsOverrideDocument>(stream);
                settings = ApplyLocalOverride(settings, localOverride);
            }
            catch
            {
                // Ignore invalid local overrides so startup still falls back cleanly.
            }
        }

        return settings;
    }

    private static PlatformIntegrationSettings CreateFallbackSettings()
    {
        return new PlatformIntegrationSettings(
            new OpenPlatformSettings(
                "AutoFallback",
                true,
                "https://vcp.21cn.com",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "1.1",
                "2.0",
                "4",
                "vcp_189",
                string.Empty,
                string.Empty,
                new OpenPlatformTokenSettings(
                    "/open/oauth/getAccessToken",
                    "refresh_token",
                    300),
                new OpenPlatformDeviceApiSettings(
                    "/open/token/device/getAllDeviceListNew",
                    "/open/token/device/getCusDeviceByDeviceCode",
                    12,
                    0,
                    0,
                    12),
                new OpenPlatformAlarmApiSettings(
                    "/open/token/AIAlarm/getAlertInfoList",
                    "/open/token/device/getDeviceAlarmMessage",
                    1,
                    20,
                    1,
                    1,
                    "3",
                    "1,2,10,11",
                    72)),
            new MapProviderSettings(
                string.Empty,
                string.Empty,
                "2.0",
                "GCJ-02",
                DefaultMapCenterLongitude,
                DefaultMapCenterLatitude,
                DefaultMapZoom,
                string.Empty,
                true,
                false));
    }

    private static OpenPlatformSettings Normalize(OpenPlatformSettings settings)
    {
        return settings with
        {
            ServiceMode = string.IsNullOrWhiteSpace(settings.ServiceMode) ? "AutoFallback" : settings.ServiceMode,
            BaseUrl = settings.BaseUrl?.Trim() ?? string.Empty,
            AppId = settings.AppId?.Trim() ?? string.Empty,
            AppKey = settings.AppKey?.Trim() ?? string.Empty,
            AppSecret = settings.AppSecret?.Trim() ?? string.Empty,
            RsaPrivateKey = settings.RsaPrivateKey?.Trim() ?? string.Empty,
            Version = string.IsNullOrWhiteSpace(settings.Version) ? "1.1" : settings.Version.Trim(),
            ApiVersion = string.IsNullOrWhiteSpace(settings.ApiVersion) ? "2.0" : settings.ApiVersion.Trim(),
            ClientType = string.IsNullOrWhiteSpace(settings.ClientType) ? "3" : settings.ClientType.Trim(),
            GrantType = string.IsNullOrWhiteSpace(settings.GrantType) ? "vcp_189" : settings.GrantType.Trim(),
            EnterpriseUser = settings.EnterpriseUser?.Trim() ?? string.Empty,
            ParentUser = settings.ParentUser?.Trim() ?? string.Empty,
            Token = settings.Token with
            {
                AccessTokenPath = string.IsNullOrWhiteSpace(settings.Token.AccessTokenPath) ? "/open/oauth/getAccessToken" : settings.Token.AccessTokenPath.Trim(),
                RefreshGrantType = string.IsNullOrWhiteSpace(settings.Token.RefreshGrantType) ? "refresh_token" : settings.Token.RefreshGrantType.Trim(),
                ReuseBeforeExpirySeconds = settings.Token.ReuseBeforeExpirySeconds <= 0 ? 300 : settings.Token.ReuseBeforeExpirySeconds
            },
            DeviceApi = settings.DeviceApi with
            {
                DeviceListPath = string.IsNullOrWhiteSpace(settings.DeviceApi.DeviceListPath) ? "/open/token/device/getAllDeviceListNew" : settings.DeviceApi.DeviceListPath.Trim(),
                DeviceDetailPath = string.IsNullOrWhiteSpace(settings.DeviceApi.DeviceDetailPath) ? "/open/token/device/getCusDeviceByDeviceCode" : settings.DeviceApi.DeviceDetailPath.Trim(),
                PageSize = settings.DeviceApi.PageSize <= 0 ? 12 : settings.DeviceApi.PageSize,
                DetailEnrichmentLimit = settings.DeviceApi.DetailEnrichmentLimit <= 0 ? 12 : settings.DeviceApi.DetailEnrichmentLimit
            },
            AlarmApi = settings.AlarmApi with
            {
                AiAlertListPath = string.IsNullOrWhiteSpace(settings.AlarmApi.AiAlertListPath) ? "/open/token/AIAlarm/getAlertInfoList" : settings.AlarmApi.AiAlertListPath.Trim(),
                DeviceAlertListPath = string.IsNullOrWhiteSpace(settings.AlarmApi.DeviceAlertListPath) ? "/open/token/device/getDeviceAlarmMessage" : settings.AlarmApi.DeviceAlertListPath.Trim(),
                PageNo = settings.AlarmApi.PageNo <= 0 ? 1 : settings.AlarmApi.PageNo,
                PageSize = settings.AlarmApi.PageSize <= 0 ? 20 : settings.AlarmApi.PageSize,
                AiAlertSource = settings.AlarmApi.AiAlertSource <= 0 ? 1 : settings.AlarmApi.AiAlertSource,
                DeviceAlertSource = settings.AlarmApi.DeviceAlertSource <= 0 ? 1 : settings.AlarmApi.DeviceAlertSource,
                AiAlertTypeList = string.IsNullOrWhiteSpace(settings.AlarmApi.AiAlertTypeList) ? "3" : settings.AlarmApi.AiAlertTypeList.Trim(),
                DeviceAlertTypeList = string.IsNullOrWhiteSpace(settings.AlarmApi.DeviceAlertTypeList) ? "1,2,10,11" : settings.AlarmApi.DeviceAlertTypeList.Trim(),
                LookbackHours = settings.AlarmApi.LookbackHours <= 0 ? 72 : settings.AlarmApi.LookbackHours
            }
        };
    }

    private MapProviderSettings LoadBundledMapProviderDefaults()
    {
        var settings = CreateFallbackSettings().MapProvider;

        var templatePath = FindBundledTemplateFile("appsettings.json");
        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            try
            {
                using var stream = File.OpenRead(templatePath);
                var template = JsonSerializer.Deserialize<CtyunTemplateDocument>(stream);
                if (template?.MapProvider is not null)
                {
                    settings = settings with
                    {
                        AmapWebJsApiKey = template.MapProvider.AmapWebJsApiKey ?? settings.AmapWebJsApiKey,
                        AmapSecurityJsCode = template.MapProvider.AmapSecurityJsCode ?? settings.AmapSecurityJsCode,
                        AmapJsApiVersion = template.MapProvider.AmapJsApiVersion ?? settings.AmapJsApiVersion,
                        CoordinateSystem = template.MapProvider.CoordinateSystem ?? settings.CoordinateSystem,
                        DefaultCenterLongitude = template.MapProvider.DefaultCenterLongitude ?? settings.DefaultCenterLongitude,
                        DefaultCenterLatitude = template.MapProvider.DefaultCenterLatitude ?? settings.DefaultCenterLatitude,
                        DefaultZoom = template.MapProvider.DefaultZoom ?? settings.DefaultZoom,
                        AmapWebServiceApiKey = template.MapProvider.AmapWebServiceApiKey ?? settings.AmapWebServiceApiKey,
                        EnableCoordinateConversion = template.MapProvider.EnableCoordinateConversion ?? settings.EnableCoordinateConversion,
                        EnableJsCoordinateFallback = template.MapProvider.EnableJsCoordinateFallback ?? settings.EnableJsCoordinateFallback
                    };
                }
            }
            catch
            {
                // Keep fallback settings when the bundled template cannot be read.
            }
        }

        var localOverridePath = FindBundledTemplateFile("appsettings.local.json");
        if (!string.IsNullOrWhiteSpace(localOverridePath))
        {
            try
            {
                using var stream = File.OpenRead(localOverridePath);
                var localOverride = JsonSerializer.Deserialize<LocalSettingsOverrideDocument>(stream);
                if (localOverride?.Amap is not null)
                {
                    settings = settings with
                    {
                        AmapWebJsApiKey = localOverride.Amap.ApiKey ?? settings.AmapWebJsApiKey,
                        AmapSecurityJsCode = localOverride.Amap.SecurityJsCode ?? settings.AmapSecurityJsCode,
                        DefaultCenterLongitude = localOverride.Amap.DefaultCenterLongitude ?? settings.DefaultCenterLongitude,
                        DefaultCenterLatitude = localOverride.Amap.DefaultCenterLatitude ?? settings.DefaultCenterLatitude,
                        DefaultZoom = localOverride.Amap.DefaultZoom ?? settings.DefaultZoom,
                        AmapWebServiceApiKey = localOverride.Amap.WebServiceApiKey ?? settings.AmapWebServiceApiKey,
                        EnableCoordinateConversion = localOverride.Amap.EnableCoordinateConversion ?? settings.EnableCoordinateConversion,
                        EnableJsCoordinateFallback = localOverride.Amap.EnableJsCoordinateFallback ?? settings.EnableJsCoordinateFallback
                    };
                }
            }
            catch
            {
                // Ignore invalid local overrides so persisted settings still load.
            }
        }

        return settings;
    }

    private static MapProviderSettings Normalize(MapProviderSettings settings, MapProviderSettings bundledDefaults)
    {
        var defaultLongitude = ResolveDefaultLongitude(settings.DefaultCenterLongitude, settings.DefaultCenterLatitude, bundledDefaults.DefaultCenterLongitude);
        var defaultLatitude = ResolveDefaultLatitude(settings.DefaultCenterLongitude, settings.DefaultCenterLatitude, bundledDefaults.DefaultCenterLatitude);

        return settings with
        {
            AmapWebJsApiKey = ResolveConfiguredValue(settings.AmapWebJsApiKey, bundledDefaults.AmapWebJsApiKey),
            AmapSecurityJsCode = ResolveConfiguredValue(settings.AmapSecurityJsCode, bundledDefaults.AmapSecurityJsCode),
            AmapJsApiVersion = ResolveOptionalValue(settings.AmapJsApiVersion, bundledDefaults.AmapJsApiVersion, "2.0"),
            CoordinateSystem = ResolveOptionalValue(settings.CoordinateSystem, bundledDefaults.CoordinateSystem, "GCJ-02"),
            DefaultCenterLongitude = defaultLongitude,
            DefaultCenterLatitude = defaultLatitude,
            DefaultZoom = settings.DefaultZoom <= 0
                ? (bundledDefaults.DefaultZoom <= 0 ? DefaultMapZoom : bundledDefaults.DefaultZoom)
                : settings.DefaultZoom,
            AmapWebServiceApiKey = ResolveConfiguredValue(settings.AmapWebServiceApiKey, bundledDefaults.AmapWebServiceApiKey),
            EnableCoordinateConversion = settings.EnableCoordinateConversion,
            EnableJsCoordinateFallback = settings.EnableJsCoordinateFallback
        };
    }

    private static string ResolveConfiguredValue(string? currentValue, string? fallbackValue)
    {
        var current = currentValue?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(current) && !LooksLikePlaceholder(current))
        {
            return current;
        }

        var fallback = fallbackValue?.Trim() ?? string.Empty;
        return LooksLikePlaceholder(fallback) ? string.Empty : fallback;
    }

    private static string ResolveOptionalValue(string? currentValue, string? fallbackValue, string finalFallback)
    {
        var current = currentValue?.Trim();
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        var fallback = fallbackValue?.Trim();
        return string.IsNullOrWhiteSpace(fallback) ? finalFallback : fallback;
    }

    private static double ResolveDefaultLongitude(double currentLongitude, double currentLatitude, double fallbackLongitude)
    {
        if (IsZeroOrigin(currentLongitude, currentLatitude))
        {
            return fallbackLongitude is >= -180d and <= 180d ? fallbackLongitude : DefaultMapCenterLongitude;
        }

        return currentLongitude is >= -180d and <= 180d
            ? currentLongitude
            : fallbackLongitude is >= -180d and <= 180d
                ? fallbackLongitude
                : DefaultMapCenterLongitude;
    }

    private static double ResolveDefaultLatitude(double currentLongitude, double currentLatitude, double fallbackLatitude)
    {
        if (IsZeroOrigin(currentLongitude, currentLatitude))
        {
            return fallbackLatitude is >= -90d and <= 90d ? fallbackLatitude : DefaultMapCenterLatitude;
        }

        return currentLatitude is >= -90d and <= 90d
            ? currentLatitude
            : fallbackLatitude is >= -90d and <= 90d
                ? fallbackLatitude
                : DefaultMapCenterLatitude;
    }

    private static bool LooksLikePlaceholder(string value)
    {
        return value.Contains("your-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsZeroOrigin(double longitude, double latitude)
    {
        return longitude == 0d && latitude == 0d;
    }

    private static PlatformIntegrationSettings ApplyLocalOverride(
        PlatformIntegrationSettings settings,
        LocalSettingsOverrideDocument? localOverride)
    {
        if (localOverride is null)
        {
            return settings;
        }

        var openPlatform = settings.OpenPlatform;
        var mapProvider = settings.MapProvider;

        if (localOverride.TylinkApi is not null)
        {
            openPlatform = openPlatform with
            {
                BaseUrl = localOverride.TylinkApi.BaseUrl ?? openPlatform.BaseUrl,
                AppId = localOverride.TylinkApi.AppId ?? openPlatform.AppId,
                AppSecret = localOverride.TylinkApi.AppSecret ?? openPlatform.AppSecret,
                RsaPrivateKey = localOverride.TylinkApi.RsaPrivateKey ?? openPlatform.RsaPrivateKey,
                Version = localOverride.TylinkApi.Version ?? openPlatform.Version,
                ApiVersion = localOverride.TylinkApi.ApiVersion ?? openPlatform.ApiVersion,
                ClientType = localOverride.TylinkApi.ClientType?.ToString() ?? openPlatform.ClientType,
                EnterpriseUser = localOverride.TylinkApi.EnterpriseUser ?? openPlatform.EnterpriseUser,
                ParentUser = localOverride.TylinkApi.ParentUser ?? openPlatform.ParentUser,
                Token = openPlatform.Token with
                {
                    ReuseBeforeExpirySeconds = localOverride.TylinkApi.TokenRefreshAheadSeconds ?? openPlatform.Token.ReuseBeforeExpirySeconds
                },
                AlarmApi = openPlatform.AlarmApi with
                {
                    PageSize = localOverride.TylinkApi.DefaultPageSize ?? openPlatform.AlarmApi.PageSize
                }
            };
        }

        if (localOverride.Amap is not null)
        {
            mapProvider = mapProvider with
            {
                AmapWebJsApiKey = localOverride.Amap.ApiKey ?? mapProvider.AmapWebJsApiKey,
                AmapSecurityJsCode = localOverride.Amap.SecurityJsCode ?? mapProvider.AmapSecurityJsCode,
                DefaultCenterLongitude = localOverride.Amap.DefaultCenterLongitude ?? mapProvider.DefaultCenterLongitude,
                DefaultCenterLatitude = localOverride.Amap.DefaultCenterLatitude ?? mapProvider.DefaultCenterLatitude,
                DefaultZoom = localOverride.Amap.DefaultZoom ?? mapProvider.DefaultZoom,
                AmapWebServiceApiKey = localOverride.Amap.WebServiceApiKey ?? mapProvider.AmapWebServiceApiKey,
                EnableCoordinateConversion = localOverride.Amap.EnableCoordinateConversion ?? mapProvider.EnableCoordinateConversion,
                EnableJsCoordinateFallback = localOverride.Amap.EnableJsCoordinateFallback ?? mapProvider.EnableJsCoordinateFallback
            };
        }

        return new PlatformIntegrationSettings(openPlatform, mapProvider);
    }

    private static string? FindBundledTemplateFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "ctyun-api", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record CtyunTemplateDocument(
        CtyunOpenPlatformTemplate? OpenPlatform,
        CtyunMapProviderTemplate? MapProvider);

    private sealed record CtyunOpenPlatformTemplate(
        string? ServiceMode,
        bool? EnableDemoFallback,
        string? BaseUrl,
        string? AppId,
        string? AppKey,
        string? AppSecret,
        string? RsaPrivateKey,
        string? Version,
        string? ApiVersion,
        string? ClientType,
        string? GrantType,
        string? EnterpriseUser,
        string? ParentUser,
        CtyunOpenPlatformTokenTemplate? Token,
        CtyunOpenPlatformDeviceApiTemplate? DeviceApi,
        CtyunOpenPlatformAlarmApiTemplate? AlarmApi);

    private sealed record CtyunOpenPlatformTokenTemplate(
        string? AccessTokenPath,
        string? RefreshGrantType,
        int? ReuseBeforeExpirySeconds);

    private sealed record CtyunOpenPlatformDeviceApiTemplate(
        string? DeviceListPath,
        string? DeviceDetailPath,
        int? PageSize,
        long? InitialLastId,
        int? HasChildDevices,
        int? DetailEnrichmentLimit);

    private sealed record CtyunOpenPlatformAlarmApiTemplate(
        string? AiAlertListPath,
        string? DeviceAlertListPath,
        int? PageNo,
        int? PageSize,
        int? AiAlertSource,
        int? DeviceAlertSource,
        string? AiAlertTypeList,
        string? DeviceAlertTypeList,
        int? LookbackHours);

    private sealed record CtyunMapProviderTemplate(
        string? AmapWebJsApiKey,
        string? AmapSecurityJsCode,
        string? AmapJsApiVersion,
        string? CoordinateSystem,
        double? DefaultCenterLongitude,
        double? DefaultCenterLatitude,
        int? DefaultZoom,
        string? AmapWebServiceApiKey,
        bool? EnableCoordinateConversion,
        bool? EnableJsCoordinateFallback);

    private sealed record LocalSettingsOverrideDocument(
        LocalTylinkApiTemplate? TylinkApi,
        LocalAmapTemplate? Amap);

    private sealed record LocalTylinkApiTemplate(
        string? BaseUrl,
        string? AppId,
        string? AppSecret,
        string? RsaPrivateKey,
        string? Version,
        string? ApiVersion,
        int? ClientType,
        string? EnterpriseUser,
        string? ParentUser,
        int? TokenRefreshAheadSeconds,
        int? DefaultPageSize);

    private sealed record LocalAmapTemplate(
        string? ApiKey,
        string? SecurityJsCode,
        double? DefaultCenterLongitude,
        double? DefaultCenterLatitude,
        int? DefaultZoom,
        string? WebServiceApiKey,
        bool? EnableCoordinateConversion,
        bool? EnableJsCoordinateFallback);
}
