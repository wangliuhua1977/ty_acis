using TianyiVision.Acis.Services.Devices;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class ReservedInspectionPointCheckExecutor : IInspectionPointCheckExecutor
{
    public Task<InspectionPointCheckResult> ExecuteAsync(
        InspectionPointCheckRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var reservedSteps = BuildReservedSteps(request);
        var point = request.Point;

        if (request.Policy.RequireOnlineStatusCheck && point.IsOnline is null)
        {
            return Task.FromResult(new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Skipped,
                InspectionPointFailureCategoryModel.OnlineStatusPending,
                "点位在线状态待确认，本轮任务骨架先保留后续检查步骤。",
                reservedSteps));
        }

        if (request.Policy.RequireOnlineStatusCheck && point.IsOnline == false)
        {
            return Task.FromResult(new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Failed,
                InspectionPointFailureCategoryModel.DeviceOffline,
                "点位离线，任务骨架已记录为待后续恢复复检。",
                reservedSteps));
        }

        if (request.Policy.RequirePlaybackCheck
            && point.IsOnline == true
            && !ContainsPlayableFlag(point.PlaybackStatusText))
        {
            return Task.FromResult(new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Failed,
                InspectionPointFailureCategoryModel.PlaybackCheckFailed,
                "播放可达性未通过，已为下一轮协议切换重试和截图留出接入点。",
                reservedSteps));
        }

        if (LooksLikeImageAbnormal(point))
        {
            return Task.FromResult(new InspectionPointCheckResult(
                InspectionPointExecutionStatusModel.Failed,
                InspectionPointFailureCategoryModel.ImageAbnormalDetected,
                "已识别为画面异常风险，后续将在此接入截图与 AI 判定。",
                reservedSteps));
        }

        return Task.FromResult(new InspectionPointCheckResult(
            InspectionPointExecutionStatusModel.Succeeded,
            InspectionPointFailureCategoryModel.None,
            "任务骨架已完成基础编排，后续可直接挂接流地址、播放检查、截图和 AI 判定。",
            reservedSteps));
    }

    private static IReadOnlyList<InspectionReservedStepModel> BuildReservedSteps(InspectionPointCheckRequest request)
    {
        return
        [
            new(
                InspectionExecutionReservationStepModel.StreamAddress,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 流地址获取",
                "预留流地址获取调用点，下一轮接入临时流地址解析。"),
            new(
                InspectionExecutionReservationStepModel.PlaybackReachability,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 播放可达性检查",
                request.Policy.RequirePlaybackCheck
                    ? "当前策略要求播放检查，下一轮在此接入真实播放可达性验证。"
                    : "当前策略未启用播放检查，入口已保留。"),
            new(
                InspectionExecutionReservationStepModel.ProtocolFallbackRetry,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 协议切换重试",
                request.VideoInspection.EnableProtocolFallbackRetry
                    ? "已启用协议切换重试，下一轮接入 HLS/FLV/RTMP 等协议降级链。"
                    : "协议切换重试当前关闭，但接口边界已保留。"),
            new(
                InspectionExecutionReservationStepModel.AutoScreenshot,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 自动截图",
                $"预留自动截图步骤，当前策略计划截图 {request.VideoInspection.ScreenshotCount} 张。"),
            new(
                InspectionExecutionReservationStepModel.ManualSupplementScreenshot,
                "IInspectionPointCheckExecutor.ExecuteAsync -> 人工补截图",
                request.VideoInspection.AllowManualSupplementScreenshot
                    ? "已允许人工补截图，后续可直接承接人工复核取证。"
                    : "当前未允许人工补截图，但接口边界已保留。"),
            new(
                InspectionExecutionReservationStepModel.AiDecision,
                "IInspectionPointCheckExecutor.ExecuteAsync -> AI 判定",
                request.Policy.EnableInterfaceAiDecision || request.Policy.EnableLocalScreenshotAnalysis
                    ? "已为接口判定和本地截图分析预留统一 AI 判定入口。"
                    : "AI 判定当前未启用，但执行器入口已保留。")
        ];
    }

    private static bool ContainsPlayableFlag(string playbackStatusText)
    {
        return !string.IsNullOrWhiteSpace(playbackStatusText)
            && playbackStatusText.Contains("可播", StringComparison.Ordinal);
    }

    private static bool LooksLikeImageAbnormal(PointWorkspaceItemModel point)
    {
        return (!string.IsNullOrWhiteSpace(point.ImageStatusText)
                && point.ImageStatusText.Contains("异常", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(point.CurrentFaultType)
                && point.CurrentFaultType.Contains("画面", StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(point.CurrentFaultSummary)
                && point.CurrentFaultSummary.Contains("画面", StringComparison.Ordinal));
    }
}
