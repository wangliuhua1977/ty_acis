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
        return _documentStore.LoadOrCreate(_paths.CtyunIntegrationFile, CreateDefaultSettings);
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
                            template.OpenPlatform.BaseUrl ?? string.Empty,
                            template.OpenPlatform.AppId ?? string.Empty,
                            template.OpenPlatform.AppSecret ?? string.Empty,
                            template.OpenPlatform.RsaPrivateKey ?? string.Empty,
                            template.OpenPlatform.Version ?? "1.1",
                            template.OpenPlatform.ApiVersion ?? "2.0",
                            template.OpenPlatform.ClientType ?? "4",
                            template.OpenPlatform.GrantType ?? "vcp_189",
                            template.OpenPlatform.EnterpriseUser ?? string.Empty,
                            template.OpenPlatform.ParentUser ?? string.Empty),
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
                "https://vcp.21cn.com",
                string.Empty,
                string.Empty,
                string.Empty,
                "1.1",
                "2.0",
                "4",
                "vcp_189",
                string.Empty,
                string.Empty),
            new MapProviderSettings(
                string.Empty,
                string.Empty,
                "2.0",
                "GCJ-02"));
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
        string? BaseUrl,
        string? AppId,
        string? AppSecret,
        string? RsaPrivateKey,
        string? Version,
        string? ApiVersion,
        string? ClientType,
        string? GrantType,
        string? EnterpriseUser,
        string? ParentUser);

    private sealed record CtyunMapProviderTemplate(
        string? AmapWebJsApiKey,
        string? AmapSecurityJsCode,
        string? AmapJsApiVersion,
        string? CoordinateSystem);
}
