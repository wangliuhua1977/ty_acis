namespace TianyiVision.Acis.Services.Storage;

public sealed class AcisLocalDataPaths
{
    public AcisLocalDataPaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TianyiVision.Acis");
    }

    public string RootDirectory { get; }

    public string ConfigDirectory => Path.Combine(RootDirectory, "config");

    public string AppearanceFile => Path.Combine(ConfigDirectory, "appearance.json");

    public string ThemeCatalogFile => Path.Combine(ConfigDirectory, "themes.catalog.json");

    public string TerminologyCatalogFile => Path.Combine(ConfigDirectory, "terminologies.catalog.json");

    public string HomeOverlayLayoutFile => Path.Combine(ConfigDirectory, "home-overlay-layout.json");

    public string IntegrationDirectory => Path.Combine(ConfigDirectory, "integrations");

    public string CtyunIntegrationFile => Path.Combine(IntegrationDirectory, "ctyun-open-platform.json");

    public string NotificationDirectory => Path.Combine(ConfigDirectory, "notifications");

    public string DispatchNotificationFile => Path.Combine(NotificationDirectory, "dispatch-notification.json");
}
