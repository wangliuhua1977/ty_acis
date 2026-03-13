using System.Text.Json;
using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Configuration;

public sealed class FilePlatformIntegrationSettingsService : IPlatformIntegrationSettingsService
{
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
        var normalized = settings with
        {
            OpenPlatform = Normalize(settings.OpenPlatform)
        };

        _documentStore.Save(_paths.CtyunIntegrationFile, normalized);
        return normalized;
    }

    private PlatformIntegrationSettings CreateDefaultSettings()
    {
        var templatePath = FindBundledTemplateFile();
        if (!string.IsNullOrWhiteSpace(templatePath))
        {
            try
            {
                using var stream = File.OpenRead(templatePath);
                var template = JsonSerializer.Deserialize<CtyunTemplateDocument>(stream);
                if (template?.OpenPlatform is not null && template.MapProvider is not null)
                {
                    return new PlatformIntegrationSettings(
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
                            template.OpenPlatform.ClientType ?? "4",
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
                            template.MapProvider.CoordinateSystem ?? "GCJ-02"));
                }
            }
            catch
            {
                // Fall back to placeholder settings when the bundled template is unreadable.
            }
        }

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
                "GCJ-02"));
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
            ClientType = string.IsNullOrWhiteSpace(settings.ClientType) ? "4" : settings.ClientType.Trim(),
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

    private static string? FindBundledTemplateFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "ctyun-api", "appsettings.json");
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
        string? CoordinateSystem);
}
