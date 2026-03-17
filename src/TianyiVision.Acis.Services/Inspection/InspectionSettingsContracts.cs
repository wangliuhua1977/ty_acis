namespace TianyiVision.Acis.Services.Inspection;

public static class InspectionSettingsValueKeys
{
    public const string AlertTypeImageAbnormal = "image_abnormal";
    public const string AlertTypeRegionIntrusion = "region_intrusion";
    public const string AlertTypeFire = "fire_alarm";
    public const string AlertTypePassengerFlow = "passenger_flow";
    public const string AlertTypeFaceOrPlate = "face_or_plate";

    public const string DispatchModeAuto = "auto_dispatch";
    public const string DispatchModeManualConfirm = "manual_confirm_dispatch";

    public const string EvidenceRetentionKeepDays = "keep_days";
    public const string EvidenceRetentionKeepLatestTask = "keep_latest_task";
    public const string EvidenceRetentionManualCleanup = "manual_cleanup";
}

public sealed record InspectionScopePlanSettings(
    string PlanId,
    string PlanName,
    string Description,
    bool IsEnabled,
    bool IsDefault,
    IReadOnlyList<string> IncludedRegions,
    IReadOnlyList<string> IncludedDirectories,
    IReadOnlyList<string> IncludedPointIds,
    IReadOnlyList<string> ExcludedPointIds,
    IReadOnlyList<string> FocusPointIds);

public sealed record InspectionPointPolicySettings(
    bool RequireOnlineStatusCheck,
    bool RequirePlaybackCheck,
    bool EnableInterfaceAiDecision,
    bool EnableLocalScreenshotAnalysis,
    bool RouteAbnormalPointsToReviewWall);

public sealed record InspectionPointPolicyOverrideSettings(
    string OverrideId,
    string PointId,
    string PointName,
    bool UseGlobalAlertTypes,
    IReadOnlyList<string> EnabledAlertTypeIds,
    InspectionPointPolicySettings Policy);

public sealed record InspectionAlertStrategySettings(
    IReadOnlyList<string> EnabledAlertTypeIds,
    InspectionPointPolicySettings GlobalPolicy,
    IReadOnlyList<InspectionPointPolicyOverrideSettings> PointOverrides);

public sealed record InspectionDispatchOverrideSettings(
    string OverrideId,
    string PointId,
    string PointName,
    string DispatchMode);

public sealed record InspectionDispatchStrategySettings(
    string GlobalDispatchMode,
    IReadOnlyList<InspectionDispatchOverrideSettings> PointOverrides);

public sealed record InspectionVideoInspectionSettings(
    int PlaybackTimeoutSeconds,
    int ScreenshotCount,
    int ScreenshotIntervalSeconds,
    int PlaybackFailureRetryCount,
    int ReinspectionIntervalMinutes,
    string EvidenceRetentionMode,
    int EvidenceRetentionDays,
    bool AllowManualSupplementScreenshot,
    bool EnableProtocolFallbackRetry)
{
    public int EffectivePlaybackTimeoutSeconds => Math.Max(3, PlaybackTimeoutSeconds);

    public int EffectivePlaybackFailureRetryCount => Math.Max(0, PlaybackFailureRetryCount);
}

public sealed record InspectionTaskExecutionSettings(
    bool EnableSinglePointInspection,
    bool EnableBatchInspection,
    bool ReserveScheduledTasks,
    int ReservedMaxConcurrency,
    string DefaultTaskNamePattern,
    bool EnforceGroupSerialExecution)
{
    public int MaxConcurrentTaskCount => ReservedMaxConcurrency;

    public string DefaultTaskNamingPattern => DefaultTaskNamePattern;
}

public sealed record InspectionSettingsSnapshot(
    IReadOnlyList<InspectionScopePlanSettings> ScopePlans,
    InspectionAlertStrategySettings AlertStrategy,
    InspectionDispatchStrategySettings DispatchStrategy,
    InspectionVideoInspectionSettings VideoInspection,
    InspectionTaskExecutionSettings TaskExecution);

public interface IInspectionSettingsService
{
    InspectionSettingsSnapshot Load();

    void Save(InspectionSettingsSnapshot settings);
}
