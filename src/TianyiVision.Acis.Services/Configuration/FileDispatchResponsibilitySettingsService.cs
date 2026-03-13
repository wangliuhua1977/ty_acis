using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Configuration;

public sealed class FileDispatchResponsibilitySettingsService : IDispatchResponsibilitySettingsService
{
    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FileDispatchResponsibilitySettingsService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public DispatchResponsibilitySettings Load()
    {
        var settings = _documentStore.LoadOrCreate(
            _paths.DispatchResponsibilityFile,
            CreateDefaultSettings);
        var normalized = Normalize(settings);
        _documentStore.Save(_paths.DispatchResponsibilityFile, normalized);
        return normalized;
    }

    public void Save(DispatchResponsibilitySettings settings)
    {
        _documentStore.Save(_paths.DispatchResponsibilityFile, Normalize(settings));
    }

    private static DispatchResponsibilitySettings CreateDefaultSettings()
    {
        return new DispatchResponsibilitySettings(
            "AutoFallback",
            true,
            new DispatchResponsibilityDefaultSettings(
                "Pending Assignment",
                "Unassigned Maintainer",
                "--",
                "Unassigned Supervisor",
                "--",
                "default"),
            [],
            []);
    }

    private static DispatchResponsibilitySettings Normalize(DispatchResponsibilitySettings settings)
    {
        var defaultAssignment = settings.DefaultAssignment ?? new DispatchResponsibilityDefaultSettings(
            "Pending Assignment",
            "Unassigned Maintainer",
            "--",
            "Unassigned Supervisor",
            "--",
            "default");
        var deviceAssignments = settings.DeviceAssignments ?? Array.Empty<DispatchResponsibilityAssignmentSettings>();
        var unitAssignments = settings.UnitAssignments ?? Array.Empty<DispatchResponsibilityUnitAssignmentSettings>();
        var mode = DispatchResponsibilitySettingsExtensions.NormalizeMode(settings.ServiceMode);
        return new DispatchResponsibilitySettings(
            mode,
            settings.EnableDemoFallback || mode == DispatchResponsibilitySettingsExtensions.AutoFallbackMode,
            NormalizeDefault(defaultAssignment),
            deviceAssignments
                .Select(item => new DispatchResponsibilityAssignmentSettings(
                    item.DeviceCode?.Trim() ?? string.Empty,
                    item.PointName?.Trim() ?? string.Empty,
                    item.CurrentHandlingUnit?.Trim() ?? string.Empty,
                    item.MaintainerName?.Trim() ?? string.Empty,
                    item.MaintainerPhone?.Trim() ?? string.Empty,
                    item.SupervisorName?.Trim() ?? string.Empty,
                    item.SupervisorPhone?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(item.NotificationChannelId) ? "default" : item.NotificationChannelId.Trim()))
                .Where(item => !string.IsNullOrWhiteSpace(item.DeviceCode))
                .ToList(),
            unitAssignments
                .Select(item => new DispatchResponsibilityUnitAssignmentSettings(
                    item.UnitName?.Trim() ?? string.Empty,
                    item.CurrentHandlingUnit?.Trim() ?? string.Empty,
                    item.MaintainerName?.Trim() ?? string.Empty,
                    item.MaintainerPhone?.Trim() ?? string.Empty,
                    item.SupervisorName?.Trim() ?? string.Empty,
                    item.SupervisorPhone?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(item.NotificationChannelId) ? "default" : item.NotificationChannelId.Trim()))
                .Where(item => !string.IsNullOrWhiteSpace(item.UnitName))
                .ToList());
    }

    private static DispatchResponsibilityDefaultSettings NormalizeDefault(DispatchResponsibilityDefaultSettings settings)
    {
        return new DispatchResponsibilityDefaultSettings(
            string.IsNullOrWhiteSpace(settings.CurrentHandlingUnit) ? "Pending Assignment" : settings.CurrentHandlingUnit.Trim(),
            string.IsNullOrWhiteSpace(settings.MaintainerName) ? "Unassigned Maintainer" : settings.MaintainerName.Trim(),
            string.IsNullOrWhiteSpace(settings.MaintainerPhone) ? "--" : settings.MaintainerPhone.Trim(),
            string.IsNullOrWhiteSpace(settings.SupervisorName) ? "Unassigned Supervisor" : settings.SupervisorName.Trim(),
            string.IsNullOrWhiteSpace(settings.SupervisorPhone) ? "--" : settings.SupervisorPhone.Trim(),
            string.IsNullOrWhiteSpace(settings.NotificationChannelId) ? "default" : settings.NotificationChannelId.Trim());
    }
}
