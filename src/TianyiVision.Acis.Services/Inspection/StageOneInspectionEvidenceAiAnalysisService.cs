using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TianyiVision.Acis.Services.Inspection;

public sealed class StageOneInspectionEvidenceAiAnalysisService : IInspectionEvidenceAiAnalysisService
{
    public InspectionPointAiAnalysisResult Analyze(InspectionPointAiAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var evidenceItems = (request.EvidenceItems ?? Array.Empty<InspectionPointEvidenceMetadataModel>())
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.LocalFilePath))
            .ToList();
        var availableEvidenceItems = evidenceItems
            .Where(item => File.Exists(item.LocalFilePath))
            .ToList();
        var abnormalTags = new List<string>();
        var signalScore = 0d;

        if (evidenceItems.Count == 0)
        {
            abnormalTags.Add("no_evidence_items");
            return BuildUnavailableResult(abnormalTags);
        }

        if (availableEvidenceItems.Count == 0)
        {
            abnormalTags.Add("evidence_files_missing");
            return BuildUnavailableResult(abnormalTags);
        }

        if (availableEvidenceItems.Count < evidenceItems.Count)
        {
            abnormalTags.Add("partial_evidence_missing");
            signalScore += 0.08d;
        }

        if (availableEvidenceItems.Any(item => string.Equals(item.EvidenceKind, InspectionEvidenceValueKeys.EvidenceKindFailureSnapshot, StringComparison.Ordinal)))
        {
            abnormalTags.Add("failure_snapshot");
            signalScore += 0.46d;
        }

        var evidenceText = string.Join(" ", availableEvidenceItems.Select(item =>
            $"{item.EvidenceSummary} {Path.GetFileNameWithoutExtension(item.LocalFilePath)}"));
        signalScore += CollectContextSignals($"{request.CurrentPointContextSummary} {request.EvidenceSummary} {evidenceText}", abnormalTags);

        var isAbnormalDetected = signalScore >= 0.45d;
        var confidence = isAbnormalDetected
            ? Math.Min(0.98d, 0.55d + signalScore)
            : Math.Max(0.62d, 0.86d - (signalScore * 0.5d));
        var suggestedAction = ResolveSuggestedAction(isAbnormalDetected, abnormalTags);
        var summary = BuildSummary(isAbnormalDetected, abnormalTags, confidence, suggestedAction, availableEvidenceItems.Count);

        return new InspectionPointAiAnalysisResult(
            InspectionEvidenceValueKeys.AiAnalysisCompleted,
            summary,
            isAbnormalDetected,
            abnormalTags,
            confidence,
            suggestedAction,
            true,
            isAbnormalDetected,
            isAbnormalDetected && ShouldRouteToDispatch(abnormalTags),
            isAbnormalDetected || abnormalTags.Contains("partial_evidence_missing", StringComparer.Ordinal));
    }

    private static InspectionPointAiAnalysisResult BuildUnavailableResult(IReadOnlyList<string> abnormalTags)
    {
        const string suggestedAction = "补充截图后进入人工复核";
        return new InspectionPointAiAnalysisResult(
            InspectionEvidenceValueKeys.AiAnalysisFailed,
            "AI画面分析未完成，当前没有可用证据文件，建议补充截图后人工复核。",
            false,
            abnormalTags,
            0.18d,
            suggestedAction,
            false,
            true,
            false,
            true);
    }

    private static double CollectContextSignals(string text, ICollection<string> abnormalTags)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0d;
        }

        var normalized = text.Trim();
        var score = 0d;

        score += TryAddSignal(normalized, abnormalTags, "offline_context", 0.32d, "离线", "未在线");
        score += TryAddSignal(normalized, abnormalTags, "playback_failure_context", 0.22d, "播放失败", "播放超时", "无流地址", "协议切换后仍失败");
        score += TryAddSignal(normalized, abnormalTags, "image_abnormal_context", 0.26d, "画面异常", "异常", "故障");
        score += TryAddSignal(normalized, abnormalTags, "manual_review_context", 0.12d, "人工复核", "补充截图");

        return score;
    }

    private static double TryAddSignal(
        string text,
        ICollection<string> abnormalTags,
        string tag,
        double score,
        params string[] keywords)
    {
        if (keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            if (!abnormalTags.Contains(tag, StringComparer.Ordinal))
            {
                abnormalTags.Add(tag);
            }

            return score;
        }

        return 0d;
    }

    private static string ResolveSuggestedAction(bool isAbnormalDetected, IReadOnlyCollection<string> abnormalTags)
    {
        if (!isAbnormalDetected)
        {
            return "保留截图证据并继续观察";
        }

        if (abnormalTags.Contains("offline_context", StringComparer.Ordinal))
        {
            return "等待设备恢复后重新巡检";
        }

        if (abnormalTags.Contains("failure_snapshot", StringComparer.Ordinal)
            || abnormalTags.Contains("playback_failure_context", StringComparer.Ordinal))
        {
            return "保留失败证据并进入人工复核";
        }

        return "进入人工复核并评估是否转入异常流转";
    }

    private static bool ShouldRouteToDispatch(IReadOnlyCollection<string> abnormalTags)
    {
        return abnormalTags.Contains("offline_context", StringComparer.Ordinal)
            || abnormalTags.Contains("image_abnormal_context", StringComparer.Ordinal)
            || abnormalTags.Contains("playback_failure_context", StringComparer.Ordinal);
    }

    private static string BuildSummary(
        bool isAbnormalDetected,
        IReadOnlyList<string> abnormalTags,
        double confidence,
        string suggestedAction,
        int evidenceItemCount)
    {
        var abnormalText = isAbnormalDetected ? "AI识别异常" : "AI未识别异常";
        var tagsText = abnormalTags.Count == 0 ? "无" : string.Join("、", abnormalTags);
        return $"{abnormalText}，本次分析使用 {evidenceItemCount} 份证据，标签：{tagsText}，置信度 {confidence:P0}，建议：{suggestedAction}。";
    }
}
