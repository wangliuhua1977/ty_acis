using System.Net.Http;
using System.Text;
using System.Text.Json;
using TianyiVision.Acis.Services.Configuration;
using TianyiVision.Acis.Services.Contracts;

namespace TianyiVision.Acis.Services.Dispatch;

public interface IDispatchNotificationSender
{
    ServiceResponse<DispatchNotificationResult> SendFaultNotification(DispatchNotificationRequestDto request);

    ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request);
}

public sealed class EnterpriseWeChatDispatchNotificationSender : IDispatchNotificationSender
{
    private readonly HttpClient _httpClient;
    private readonly INotificationSettingsService _notificationSettingsService;

    public EnterpriseWeChatDispatchNotificationSender(
        HttpClient httpClient,
        INotificationSettingsService notificationSettingsService)
    {
        _httpClient = httpClient;
        _notificationSettingsService = notificationSettingsService;
    }

    public ServiceResponse<DispatchNotificationResult> SendFaultNotification(DispatchNotificationRequestDto request)
    {
        return Send(request, true);
    }

    public ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request)
    {
        return Send(request, false);
    }

    private ServiceResponse<DispatchNotificationResult> Send(DispatchNotificationRequestDto request, bool isFaultNotification)
    {
        var sentAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var settings = _notificationSettingsService.Load();
        var issues = settings.GetConfigurationIssues();
        if (issues.Count > 0)
        {
            return Failure(sentAt, $"Webhook configuration is incomplete: {string.Join(" | ", issues)}", request);
        }

        var channel = ResolveChannel(settings, request.NotificationChannelId);
        if (channel is null)
        {
            return Failure(sentAt, "No enabled webhook channel is available for the dispatch notification.", request);
        }

        try
        {
            var payload = new EnterpriseWeChatWebhookRequest
            {
                msgtype = "markdown",
                markdown = new EnterpriseWeChatWebhookMarkdown
                {
                    content = BuildMarkdown(request, channel, isFaultNotification)
                }
            };
            var json = JsonSerializer.Serialize(payload);
            using var message = new HttpRequestMessage(HttpMethod.Post, channel.WebhookUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = _httpClient.Send(message);
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return Failure(
                    sentAt,
                    $"Webhook returned HTTP {(int)response.StatusCode}: {body}",
                    request,
                    channel.DisplayName);
            }

            var webhookResponse = JsonSerializer.Deserialize<EnterpriseWeChatWebhookResponse>(body);
            if (webhookResponse is null)
            {
                return Failure(sentAt, "Webhook returned an empty response.", request, channel.DisplayName);
            }

            if (webhookResponse.errcode != 0)
            {
                return Failure(
                    sentAt,
                    $"Webhook rejected the request: {webhookResponse.errmsg}",
                    request,
                    channel.DisplayName);
            }

            var statusText = isFaultNotification
                ? "Enterprise WeChat dispatch notification sent."
                : "Enterprise WeChat recovery notification sent.";
            return ServiceResponse<DispatchNotificationResult>.Success(
                new DispatchNotificationResult(
                    sentAt,
                    statusText,
                    $"{channel.DisplayName} / {request.CurrentHandlingUnit}"),
                channel.DisplayName);
        }
        catch (Exception ex)
        {
            return Failure(
                sentAt,
                $"Webhook dispatch failed: {ex.Message}",
                request,
                channel?.DisplayName);
        }
    }

    private static NotificationChannelSettings? ResolveChannel(
        DispatchNotificationSettings settings,
        string preferredChannelId)
    {
        var enabledChannels = settings.Channels
            .Where(channel => channel.IsEnabled && !string.IsNullOrWhiteSpace(channel.WebhookUrl))
            .ToList();
        if (enabledChannels.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredChannelId))
        {
            var match = enabledChannels.FirstOrDefault(channel =>
                string.Equals(channel.ChannelId, preferredChannelId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return enabledChannels[0];
    }

    private static string BuildMarkdown(
        DispatchNotificationRequestDto request,
        NotificationChannelSettings channel,
        bool isFaultNotification)
    {
        var title = isFaultNotification
            ? "AI巡检故障派单通知"
            : "AI巡检恢复通知";
        var screenshotText = string.IsNullOrWhiteSpace(request.ScreenshotTitle)
            ? "暂未接入真实截图，当前使用占位说明"
            : request.ScreenshotTitle.Trim();
        var safeTitle = Escape(title);
        return $@"# {safeTitle}
> 通知通道：{Escape(channel.DisplayName)}
> 当前处理单位：{Escape(request.CurrentHandlingUnit)}
> 排障维护人：{Escape(request.MaintainerName)} {Escape(request.MaintainerPhone)}
> 上级负责人：{Escape(request.SupervisorName)} {Escape(request.SupervisorPhone)}
> 点位名称：{Escape(request.PointName)}
> 设备编码：{Escape(request.PointId)}
> 故障类型：{Escape(request.FaultType)}
> 最近一次故障时间：{Escape(request.FaultDetectedAt.ToString("yyyy-MM-dd HH:mm"))}
> 故障截图：{Escape(screenshotText)}";
    }

    private static string Escape(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "--"
            : value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static ServiceResponse<DispatchNotificationResult> Failure(
        string sentAt,
        string message,
        DispatchNotificationRequestDto request,
        string? channelName = null)
    {
        var result = new DispatchNotificationResult(
            sentAt,
            message,
            string.IsNullOrWhiteSpace(channelName)
                ? request.CurrentHandlingUnit
                : $"{channelName} / {request.CurrentHandlingUnit}");
        return ServiceResponse<DispatchNotificationResult>.Failure(result, message);
    }

    private sealed class EnterpriseWeChatWebhookRequest
    {
        public string msgtype { get; init; } = string.Empty;

        public EnterpriseWeChatWebhookMarkdown markdown { get; init; } = new();
    }

    private sealed class EnterpriseWeChatWebhookMarkdown
    {
        public string content { get; init; } = string.Empty;
    }

    private sealed class EnterpriseWeChatWebhookResponse
    {
        public int errcode { get; init; }

        public string errmsg { get; init; } = string.Empty;
    }
}
