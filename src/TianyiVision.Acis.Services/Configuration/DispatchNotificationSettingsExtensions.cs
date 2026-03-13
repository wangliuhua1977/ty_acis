namespace TianyiVision.Acis.Services.Configuration;

public static class DispatchNotificationSettingsExtensions
{
    public const string DemoMode = "Demo";
    public const string EnterpriseWeChatMode = "EnterpriseWeChat";
    public const string AutoFallbackMode = "AutoFallback";

    public static bool IsEnterpriseWeChatPreferred(this DispatchNotificationSettings settings)
    {
        return NormalizeMode(settings.ServiceMode) is EnterpriseWeChatMode or AutoFallbackMode;
    }

    public static bool IsAutoFallback(this DispatchNotificationSettings settings)
    {
        return NormalizeMode(settings.ServiceMode) == AutoFallbackMode;
    }

    public static bool HasEnabledChannels(this DispatchNotificationSettings settings)
    {
        return settings.Channels.Any(channel =>
            channel.IsEnabled &&
            !string.IsNullOrWhiteSpace(channel.WebhookUrl));
    }

    public static IReadOnlyList<string> GetConfigurationIssues(this DispatchNotificationSettings settings)
    {
        var issues = new List<string>();
        if (settings.Channels.Count == 0)
        {
            issues.Add("DispatchNotification.Channels is empty.");
            return issues;
        }

        if (!settings.Channels.Any(channel => channel.IsEnabled))
        {
            issues.Add("DispatchNotification has no enabled channel.");
        }

        foreach (var channel in settings.Channels.Where(channel => channel.IsEnabled))
        {
            if (string.IsNullOrWhiteSpace(channel.ChannelId))
            {
                issues.Add("DispatchNotification has an enabled channel without ChannelId.");
            }

            if (string.IsNullOrWhiteSpace(channel.WebhookUrl))
            {
                issues.Add($"DispatchNotification channel '{channel.DisplayName}' is missing WebhookUrl.");
            }
        }

        return issues;
    }

    public static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, EnterpriseWeChatMode, StringComparison.OrdinalIgnoreCase))
        {
            return EnterpriseWeChatMode;
        }

        if (string.Equals(mode, DemoMode, StringComparison.OrdinalIgnoreCase))
        {
            return DemoMode;
        }

        return AutoFallbackMode;
    }
}
