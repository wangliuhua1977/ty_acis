using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Dispatch;

public sealed class FileDispatchResponsibilityService : IDispatchResponsibilityService
{
    private readonly IDispatchResponsibilitySettingsService _settingsService;

    public FileDispatchResponsibilityService(IDispatchResponsibilitySettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public ServiceResponse<DispatchResponsibilityModel> Resolve(DispatchResponsibilityQueryDto request)
    {
        var settings = _settingsService.Load();
        var assignment = settings.DeviceAssignments.FirstOrDefault(item =>
            string.Equals(item.DeviceCode, request.PointId, StringComparison.OrdinalIgnoreCase));

        if (assignment is not null)
        {
            return ServiceResponse<DispatchResponsibilityModel>.Success(MapAssignment(assignment, request.CurrentHandlingUnit, "LocalFile.Device"));
        }

        var unitAssignment = settings.UnitAssignments.FirstOrDefault(item =>
            string.Equals(item.UnitName, request.CurrentHandlingUnit, StringComparison.OrdinalIgnoreCase));

        if (unitAssignment is not null)
        {
            return ServiceResponse<DispatchResponsibilityModel>.Success(MapUnitAssignment(unitAssignment, request.CurrentHandlingUnit, "LocalFile.Unit"));
        }

        return ServiceResponse<DispatchResponsibilityModel>.Success(
            MapDefault(settings.DefaultAssignment, request.CurrentHandlingUnit),
            "Responsibility mapping was resolved from the local default assignment.");
    }

    public ServiceResponse<DispatchResponsibilityModel> Save(DispatchResponsibilityUpdateDto request)
    {
        var settings = _settingsService.Load();
        var deviceAssignments = settings.DeviceAssignments.ToList();
        var existingIndex = deviceAssignments.FindIndex(item =>
            string.Equals(item.DeviceCode, request.PointId, StringComparison.OrdinalIgnoreCase));
        var channelId = string.IsNullOrWhiteSpace(request.NotificationChannelId)
            ? ResolveExistingChannelId(settings, request.PointId, request.CurrentHandlingUnit)
            : request.NotificationChannelId.Trim();
        var updated = new DispatchResponsibilityAssignmentSettings(
            request.PointId.Trim(),
            request.PointName?.Trim() ?? string.Empty,
            request.CurrentHandlingUnit?.Trim() ?? string.Empty,
            request.MaintainerName?.Trim() ?? string.Empty,
            request.MaintainerPhone?.Trim() ?? string.Empty,
            request.SupervisorName?.Trim() ?? string.Empty,
            request.SupervisorPhone?.Trim() ?? string.Empty,
            channelId);

        if (existingIndex >= 0)
        {
            deviceAssignments[existingIndex] = updated;
        }
        else
        {
            deviceAssignments.Add(updated);
        }

        var nextSettings = settings with { DeviceAssignments = deviceAssignments };
        _settingsService.Save(nextSettings);

        return ServiceResponse<DispatchResponsibilityModel>.Success(
            MapAssignment(updated, request.CurrentHandlingUnit ?? string.Empty, "LocalFile.Override"),
            "Responsibility mapping saved to the local dispatch responsibility file.");
    }

    private static DispatchResponsibilityModel MapAssignment(
        DispatchResponsibilityAssignmentSettings assignment,
        string requestedHandlingUnit,
        string sourceTag)
    {
        var currentHandlingUnit = string.IsNullOrWhiteSpace(assignment.CurrentHandlingUnit)
            ? FallbackHandlingUnit(requestedHandlingUnit)
            : assignment.CurrentHandlingUnit;
        return new DispatchResponsibilityModel(
            currentHandlingUnit,
            EmptyToPlaceholder(assignment.MaintainerName, "Unassigned Maintainer"),
            EmptyToPlaceholder(assignment.MaintainerPhone, "--"),
            EmptyToPlaceholder(assignment.SupervisorName, "Unassigned Supervisor"),
            EmptyToPlaceholder(assignment.SupervisorPhone, "--"),
            EmptyToPlaceholder(assignment.NotificationChannelId, "default"),
            sourceTag);
    }

    private static DispatchResponsibilityModel MapUnitAssignment(
        DispatchResponsibilityUnitAssignmentSettings assignment,
        string requestedHandlingUnit,
        string sourceTag)
    {
        var currentHandlingUnit = string.IsNullOrWhiteSpace(assignment.CurrentHandlingUnit)
            ? FallbackHandlingUnit(requestedHandlingUnit)
            : assignment.CurrentHandlingUnit;
        return new DispatchResponsibilityModel(
            currentHandlingUnit,
            EmptyToPlaceholder(assignment.MaintainerName, "Unassigned Maintainer"),
            EmptyToPlaceholder(assignment.MaintainerPhone, "--"),
            EmptyToPlaceholder(assignment.SupervisorName, "Unassigned Supervisor"),
            EmptyToPlaceholder(assignment.SupervisorPhone, "--"),
            EmptyToPlaceholder(assignment.NotificationChannelId, "default"),
            sourceTag);
    }

    private static DispatchResponsibilityModel MapDefault(
        DispatchResponsibilityDefaultSettings assignment,
        string requestedHandlingUnit)
    {
        var currentHandlingUnit = string.IsNullOrWhiteSpace(assignment.CurrentHandlingUnit)
            ? FallbackHandlingUnit(requestedHandlingUnit)
            : assignment.CurrentHandlingUnit;
        return new DispatchResponsibilityModel(
            currentHandlingUnit,
            EmptyToPlaceholder(assignment.MaintainerName, "Unassigned Maintainer"),
            EmptyToPlaceholder(assignment.MaintainerPhone, "--"),
            EmptyToPlaceholder(assignment.SupervisorName, "Unassigned Supervisor"),
            EmptyToPlaceholder(assignment.SupervisorPhone, "--"),
            EmptyToPlaceholder(assignment.NotificationChannelId, "default"),
            "LocalFile.Default");
    }

    private static string ResolveExistingChannelId(
        DispatchResponsibilitySettings settings,
        string pointId,
        string handlingUnit)
    {
        var byDevice = settings.DeviceAssignments.FirstOrDefault(item =>
            string.Equals(item.DeviceCode, pointId, StringComparison.OrdinalIgnoreCase));
        if (byDevice is not null && !string.IsNullOrWhiteSpace(byDevice.NotificationChannelId))
        {
            return byDevice.NotificationChannelId;
        }

        var byUnit = settings.UnitAssignments.FirstOrDefault(item =>
            string.Equals(item.UnitName, handlingUnit, StringComparison.OrdinalIgnoreCase));
        if (byUnit is not null && !string.IsNullOrWhiteSpace(byUnit.NotificationChannelId))
        {
            return byUnit.NotificationChannelId;
        }

        return string.IsNullOrWhiteSpace(settings.DefaultAssignment.NotificationChannelId)
            ? "default"
            : settings.DefaultAssignment.NotificationChannelId;
    }

    private static string EmptyToPlaceholder(string? value, string placeholder)
    {
        return string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();
    }

    private static string FallbackHandlingUnit(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Pending Assignment" : value.Trim();
    }
}

public sealed class DemoDispatchResponsibilityService : IDispatchResponsibilityService
{
    private readonly Dictionary<string, DispatchResponsibilityModel> _overrides;
    private readonly IDispatchNotificationService _dispatchNotificationService;

    public DemoDispatchResponsibilityService(IDispatchNotificationService dispatchNotificationService)
    {
        _dispatchNotificationService = dispatchNotificationService;
        _overrides = new Dictionary<string, DispatchResponsibilityModel>(StringComparer.OrdinalIgnoreCase);
    }

    public ServiceResponse<DispatchResponsibilityModel> Resolve(DispatchResponsibilityQueryDto request)
    {
        if (_overrides.TryGetValue(request.PointId, out var existing))
        {
            return ServiceResponse<DispatchResponsibilityModel>.Success(existing, "Responsibility mapping loaded from the demo override cache.");
        }

        var workOrder = _dispatchNotificationService.GetWorkOrders().Data
            .FirstOrDefault(item => string.Equals(item.PointId, request.PointId, StringComparison.OrdinalIgnoreCase));
        if (workOrder is not null)
        {
            return ServiceResponse<DispatchResponsibilityModel>.Success(workOrder.Responsibility, "Responsibility mapping loaded from demo work orders.");
        }

        return ServiceResponse<DispatchResponsibilityModel>.Success(
            new DispatchResponsibilityModel(
                string.IsNullOrWhiteSpace(request.CurrentHandlingUnit) ? "Demo Handling Unit" : request.CurrentHandlingUnit,
                "Demo Maintainer",
                "--",
                "Demo Supervisor",
                "--",
                "default",
                "Demo.Default"),
            "Responsibility mapping fell back to the demo default.");
    }

    public ServiceResponse<DispatchResponsibilityModel> Save(DispatchResponsibilityUpdateDto request)
    {
        var assignment = new DispatchResponsibilityModel(
            string.IsNullOrWhiteSpace(request.CurrentHandlingUnit) ? "Demo Handling Unit" : request.CurrentHandlingUnit,
            EmptyToPlaceholder(request.MaintainerName, "Demo Maintainer"),
            EmptyToPlaceholder(request.MaintainerPhone, "--"),
            EmptyToPlaceholder(request.SupervisorName, "Demo Supervisor"),
            EmptyToPlaceholder(request.SupervisorPhone, "--"),
            string.IsNullOrWhiteSpace(request.NotificationChannelId) ? "default" : request.NotificationChannelId.Trim(),
            "Demo.Override");
        _overrides[request.PointId] = assignment;
        return ServiceResponse<DispatchResponsibilityModel>.Success(assignment, "Responsibility mapping saved to the demo override cache.");
    }

    private static string EmptyToPlaceholder(string? value, string placeholder)
    {
        return string.IsNullOrWhiteSpace(value) ? placeholder : value.Trim();
    }
}

public sealed class FallbackDispatchResponsibilityService : IDispatchResponsibilityService
{
    private readonly IDispatchResponsibilityService _primary;
    private readonly IDispatchResponsibilityService _fallback;

    public FallbackDispatchResponsibilityService(
        IDispatchResponsibilityService primary,
        IDispatchResponsibilityService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public ServiceResponse<DispatchResponsibilityModel> Resolve(DispatchResponsibilityQueryDto request)
    {
        var response = SafeExecute(() => _primary.Resolve(request), request.CurrentHandlingUnit);
        if (response.IsSuccess)
        {
            return response;
        }

        var fallback = _fallback.Resolve(request);
        return fallback.IsSuccess
            ? ServiceResponse<DispatchResponsibilityModel>.Success(
                fallback.Data,
                string.IsNullOrWhiteSpace(response.Message)
                    ? "Responsibility mapping fell back to demo data."
                    : $"{response.Message} Responsibility mapping fell back to demo data.")
            : fallback;
    }

    public ServiceResponse<DispatchResponsibilityModel> Save(DispatchResponsibilityUpdateDto request)
    {
        var response = SafeExecute(
            () => _primary.Save(request),
            request.CurrentHandlingUnit);
        if (response.IsSuccess)
        {
            return response;
        }

        var fallback = _fallback.Save(request);
        return fallback.IsSuccess
            ? ServiceResponse<DispatchResponsibilityModel>.Success(
                fallback.Data,
                string.IsNullOrWhiteSpace(response.Message)
                    ? "Responsibility update fell back to demo storage."
                    : $"{response.Message} Responsibility update fell back to demo storage.")
            : fallback;
    }

    private static ServiceResponse<DispatchResponsibilityModel> SafeExecute(
        Func<ServiceResponse<DispatchResponsibilityModel>> action,
        string handlingUnit)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return ServiceResponse<DispatchResponsibilityModel>.Failure(
                new DispatchResponsibilityModel(
                    string.IsNullOrWhiteSpace(handlingUnit) ? "Pending Assignment" : handlingUnit,
                    "Unassigned Maintainer",
                    "--",
                    "Unassigned Supervisor",
                    "--",
                    "default",
                    "Fallback.Placeholder"),
                $"Responsibility service failed: {ex.Message}");
        }
    }
}
