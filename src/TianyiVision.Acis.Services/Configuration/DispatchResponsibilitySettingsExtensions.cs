namespace TianyiVision.Acis.Services.Configuration;

public static class DispatchResponsibilitySettingsExtensions
{
    public const string DemoMode = "Demo";
    public const string LocalFileMode = "LocalFile";
    public const string AutoFallbackMode = "AutoFallback";

    public static bool IsLocalFilePreferred(this DispatchResponsibilitySettings settings)
    {
        return NormalizeMode(settings.ServiceMode) is LocalFileMode or AutoFallbackMode;
    }

    public static bool IsAutoFallback(this DispatchResponsibilitySettings settings)
    {
        return NormalizeMode(settings.ServiceMode) == AutoFallbackMode;
    }

    public static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, LocalFileMode, StringComparison.OrdinalIgnoreCase))
        {
            return LocalFileMode;
        }

        if (string.Equals(mode, DemoMode, StringComparison.OrdinalIgnoreCase))
        {
            return DemoMode;
        }

        return AutoFallbackMode;
    }
}
