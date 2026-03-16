using TianyiVision.Acis.Services.Storage;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class FileInspectionSettingsService : IInspectionSettingsService
{
    private static readonly string[] SupportedAlertTypeIds =
    [
        InspectionSettingsValueKeys.AlertTypeImageAbnormal,
        InspectionSettingsValueKeys.AlertTypeRegionIntrusion,
        InspectionSettingsValueKeys.AlertTypeFire,
        InspectionSettingsValueKeys.AlertTypePassengerFlow,
        InspectionSettingsValueKeys.AlertTypeFaceOrPlate
    ];

    private readonly AcisLocalDataPaths _paths;
    private readonly JsonFileDocumentStore _documentStore;

    public FileInspectionSettingsService(AcisLocalDataPaths paths, JsonFileDocumentStore documentStore)
    {
        _paths = paths;
        _documentStore = documentStore;
    }

    public InspectionSettingsSnapshot Load()
    {
        var settings = _documentStore.LoadOrCreate(_paths.InspectionSettingsFile, CreateDefaultSettings);
        var normalized = Normalize(settings);
        _documentStore.Save(_paths.InspectionSettingsFile, normalized);
        return normalized;
    }

    public void Save(InspectionSettingsSnapshot settings)
    {
        _documentStore.Save(_paths.InspectionSettingsFile, Normalize(settings));
    }

    private static InspectionSettingsSnapshot CreateDefaultSettings()
    {
        return new InspectionSettingsSnapshot(
            [
                new InspectionScopePlanSettings(
                    "scope-river-priority",
                    "沿江重点视频巡检",
                    "纳管沿江重点区域与重点点位，适合主城区高频巡检场景。",
                    true,
                    true,
                    ["沿江重点区域", "防汛重点区域"],
                    ["沿江目录", "桥梁目录"],
                    ["POINT-RIVER-001", "POINT-RIVER-002", "POINT-BRIDGE-008"],
                    ["POINT-RIVER-099"],
                    ["POINT-RIVER-001", "POINT-BRIDGE-008"]),
                new InspectionScopePlanSettings(
                    "scope-night-view",
                    "夜景保障专项巡检",
                    "聚焦夜景和文旅点位，适合夜间保障和值守巡检。",
                    true,
                    false,
                    ["文旅夜景区域"],
                    ["夜景目录", "文旅目录"],
                    ["POINT-NIGHT-018", "POINT-NIGHT-021"],
                    [],
                    ["POINT-NIGHT-018"])
            ],
            new InspectionAlertStrategySettings(
                [
                    InspectionSettingsValueKeys.AlertTypeImageAbnormal,
                    InspectionSettingsValueKeys.AlertTypeRegionIntrusion,
                    InspectionSettingsValueKeys.AlertTypeFire
                ],
                new InspectionPointPolicySettings(
                    true,
                    true,
                    true,
                    true,
                    true),
                [
                    new InspectionPointPolicyOverrideSettings(
                        "policy-river-001",
                        "POINT-RIVER-001",
                        "沿江码头北口",
                        true,
                        [],
                        new InspectionPointPolicySettings(true, true, true, true, true)),
                    new InspectionPointPolicyOverrideSettings(
                        "policy-night-018",
                        "POINT-NIGHT-018",
                        "夜景广场主屏",
                        false,
                        [
                            InspectionSettingsValueKeys.AlertTypeImageAbnormal,
                            InspectionSettingsValueKeys.AlertTypePassengerFlow
                        ],
                        new InspectionPointPolicySettings(true, true, true, true, false))
                ]),
            new InspectionDispatchStrategySettings(
                InspectionSettingsValueKeys.DispatchModeManualConfirm,
                [
                    new InspectionDispatchOverrideSettings(
                        "dispatch-river-001",
                        "POINT-RIVER-001",
                        "沿江码头北口",
                        InspectionSettingsValueKeys.DispatchModeAuto)
                ]),
            new InspectionVideoInspectionSettings(
                12,
                3,
                4,
                2,
                10,
                InspectionSettingsValueKeys.EvidenceRetentionKeepDays,
                30,
                true,
                true),
            new InspectionTaskExecutionSettings(
                true,
                true,
                true,
                4,
                "AIXC-{yyyyMMdd}-{mode}-{pointId}",
                true));
    }

    private static InspectionSettingsSnapshot Normalize(InspectionSettingsSnapshot settings)
    {
        var scopePlans = NormalizeScopePlans(settings.ScopePlans);
        var defaultPlanId = scopePlans.FirstOrDefault(plan => plan.IsDefault)?.PlanId ?? scopePlans[0].PlanId;
        scopePlans = scopePlans
            .Select(plan => plan with { IsDefault = string.Equals(plan.PlanId, defaultPlanId, StringComparison.OrdinalIgnoreCase) })
            .ToList();

        return new InspectionSettingsSnapshot(
            scopePlans,
            NormalizeAlertStrategy(settings.AlertStrategy),
            NormalizeDispatchStrategy(settings.DispatchStrategy),
            NormalizeVideoInspection(settings.VideoInspection),
            NormalizeTaskExecution(settings.TaskExecution));
    }

    private static IReadOnlyList<InspectionScopePlanSettings> NormalizeScopePlans(IReadOnlyList<InspectionScopePlanSettings>? plans)
    {
        var sourcePlans = plans ?? Array.Empty<InspectionScopePlanSettings>();
        var normalized = sourcePlans
            .Select(plan => new InspectionScopePlanSettings(
                string.IsNullOrWhiteSpace(plan.PlanId) ? CreateId("scope") : plan.PlanId.Trim(),
                string.IsNullOrWhiteSpace(plan.PlanName) ? "未命名方案" : plan.PlanName.Trim(),
                plan.Description?.Trim() ?? string.Empty,
                plan.IsEnabled,
                plan.IsDefault,
                NormalizeList(plan.IncludedRegions),
                NormalizeList(plan.IncludedDirectories),
                NormalizeList(plan.IncludedPointIds),
                NormalizeList(plan.ExcludedPointIds),
                NormalizeList(plan.FocusPointIds)))
            .GroupBy(plan => plan.PlanId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (normalized.Count > 0)
        {
            return normalized;
        }

        return CreateDefaultSettings().ScopePlans;
    }

    private static InspectionAlertStrategySettings NormalizeAlertStrategy(InspectionAlertStrategySettings? settings)
    {
        settings ??= CreateDefaultSettings().AlertStrategy;
        return new InspectionAlertStrategySettings(
            NormalizeAlertTypeIds(settings.EnabledAlertTypeIds),
            NormalizePolicy(settings.GlobalPolicy),
            (settings.PointOverrides ?? Array.Empty<InspectionPointPolicyOverrideSettings>())
                .Select(overrideSettings => new InspectionPointPolicyOverrideSettings(
                    string.IsNullOrWhiteSpace(overrideSettings.OverrideId) ? CreateId("policy") : overrideSettings.OverrideId.Trim(),
                    overrideSettings.PointId?.Trim() ?? string.Empty,
                    overrideSettings.PointName?.Trim() ?? string.Empty,
                    overrideSettings.UseGlobalAlertTypes,
                    overrideSettings.UseGlobalAlertTypes
                        ? Array.Empty<string>()
                        : NormalizeAlertTypeIds(overrideSettings.EnabledAlertTypeIds),
                    NormalizePolicy(overrideSettings.Policy)))
                .Where(overrideSettings => !string.IsNullOrWhiteSpace(overrideSettings.PointId))
                .GroupBy(overrideSettings => overrideSettings.PointId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList());
    }

    private static InspectionDispatchStrategySettings NormalizeDispatchStrategy(InspectionDispatchStrategySettings? settings)
    {
        settings ??= CreateDefaultSettings().DispatchStrategy;
        var globalMode = NormalizeDispatchMode(settings.GlobalDispatchMode);
        return new InspectionDispatchStrategySettings(
            globalMode,
            (settings.PointOverrides ?? Array.Empty<InspectionDispatchOverrideSettings>())
                .Select(overrideSettings => new InspectionDispatchOverrideSettings(
                    string.IsNullOrWhiteSpace(overrideSettings.OverrideId) ? CreateId("dispatch") : overrideSettings.OverrideId.Trim(),
                    overrideSettings.PointId?.Trim() ?? string.Empty,
                    overrideSettings.PointName?.Trim() ?? string.Empty,
                    NormalizeDispatchMode(overrideSettings.DispatchMode)))
                .Where(overrideSettings => !string.IsNullOrWhiteSpace(overrideSettings.PointId))
                .GroupBy(overrideSettings => overrideSettings.PointId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList());
    }

    private static InspectionVideoInspectionSettings NormalizeVideoInspection(InspectionVideoInspectionSettings? settings)
    {
        settings ??= CreateDefaultSettings().VideoInspection;
        return new InspectionVideoInspectionSettings(
            Math.Max(3, settings.PlaybackTimeoutSeconds),
            Math.Max(1, settings.ScreenshotCount),
            Math.Max(1, settings.ScreenshotIntervalSeconds),
            Math.Max(0, settings.PlaybackFailureRetryCount),
            Math.Max(1, settings.ReinspectionIntervalMinutes),
            NormalizeEvidenceRetentionMode(settings.EvidenceRetentionMode),
            Math.Max(1, settings.EvidenceRetentionDays),
            settings.AllowManualSupplementScreenshot,
            settings.EnableProtocolFallbackRetry);
    }

    private static InspectionTaskExecutionSettings NormalizeTaskExecution(InspectionTaskExecutionSettings? settings)
    {
        settings ??= CreateDefaultSettings().TaskExecution;
        return new InspectionTaskExecutionSettings(
            settings.EnableSinglePointInspection,
            settings.EnableBatchInspection,
            settings.ReserveScheduledTasks,
            Math.Max(1, settings.ReservedMaxConcurrency),
            string.IsNullOrWhiteSpace(settings.DefaultTaskNamePattern)
                ? "AIXC-{yyyyMMdd}-{mode}-{pointId}"
                : settings.DefaultTaskNamePattern.Trim(),
            true);
    }

    private static InspectionPointPolicySettings NormalizePolicy(InspectionPointPolicySettings? settings)
    {
        settings ??= new InspectionPointPolicySettings(true, true, true, true, true);
        return settings with
        {
            RequireOnlineStatusCheck = true
        };
    }

    private static IReadOnlyList<string> NormalizeAlertTypeIds(IReadOnlyList<string>? alertTypeIds)
    {
        var normalized = NormalizeList(alertTypeIds)
            .Where(id => SupportedAlertTypeIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0
            ? [InspectionSettingsValueKeys.AlertTypeImageAbnormal]
            : normalized;
    }

    private static string NormalizeDispatchMode(string? mode)
    {
        return string.Equals(mode, InspectionSettingsValueKeys.DispatchModeAuto, StringComparison.OrdinalIgnoreCase)
            ? InspectionSettingsValueKeys.DispatchModeAuto
            : InspectionSettingsValueKeys.DispatchModeManualConfirm;
    }

    private static string NormalizeEvidenceRetentionMode(string? mode)
    {
        if (string.Equals(mode, InspectionSettingsValueKeys.EvidenceRetentionKeepLatestTask, StringComparison.OrdinalIgnoreCase))
        {
            return InspectionSettingsValueKeys.EvidenceRetentionKeepLatestTask;
        }

        if (string.Equals(mode, InspectionSettingsValueKeys.EvidenceRetentionManualCleanup, StringComparison.OrdinalIgnoreCase))
        {
            return InspectionSettingsValueKeys.EvidenceRetentionManualCleanup;
        }

        return InspectionSettingsValueKeys.EvidenceRetentionKeepDays;
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? items)
    {
        return (items ?? Array.Empty<string>())
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string CreateId(string prefix)
        => $"{prefix}-{Guid.NewGuid():N}";
}
