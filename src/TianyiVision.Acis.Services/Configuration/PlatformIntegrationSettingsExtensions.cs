namespace TianyiVision.Acis.Services.Configuration;

public static class PlatformIntegrationSettingsExtensions
{
    public const string DemoMode = "Demo";
    public const string CtyunMode = "CTYun";
    public const string AutoFallbackMode = "AutoFallback";

    public static bool IsCtyunPreferred(this PlatformIntegrationSettings settings)
    {
        return NormalizeMode(settings.OpenPlatform.ServiceMode) is CtyunMode or AutoFallbackMode;
    }

    public static bool IsAutoFallback(this PlatformIntegrationSettings settings)
    {
        return NormalizeMode(settings.OpenPlatform.ServiceMode) == AutoFallbackMode;
    }

    public static IReadOnlyList<string> GetCtyunConfigurationIssues(this PlatformIntegrationSettings settings)
    {
        var issues = new List<string>();
        var openPlatform = settings.OpenPlatform;

        if (string.IsNullOrWhiteSpace(openPlatform.BaseUrl))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.BaseUrl。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.AppId))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.AppId。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.AppSecret))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.AppSecret。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.EnterpriseUser))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.EnterpriseUser。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.Token.AccessTokenPath))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.Token.AccessTokenPath。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.DeviceApi.DeviceListPath))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.DeviceApi.DeviceListPath。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.DeviceApi.DeviceDetailPath))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.DeviceApi.DeviceDetailPath。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.AlarmApi.AiAlertListPath))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.AlarmApi.AiAlertListPath。");
        }

        if (string.IsNullOrWhiteSpace(openPlatform.AlarmApi.DeviceAlertListPath))
        {
            issues.Add("CTYun 配置缺少 OpenPlatform.AlarmApi.DeviceAlertListPath。");
        }

        return issues;
    }

    public static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, CtyunMode, StringComparison.OrdinalIgnoreCase))
        {
            return CtyunMode;
        }

        if (string.Equals(mode, DemoMode, StringComparison.OrdinalIgnoreCase))
        {
            return DemoMode;
        }

        return AutoFallbackMode;
    }
}
