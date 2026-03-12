using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Dispatch;

namespace TianyiVision.Acis.Services.Demo;

public sealed class DemoDispatchNotificationService : IDispatchNotificationService
{
    public ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>> GetWorkOrders()
    {
        IReadOnlyList<DispatchWorkOrderModel> workOrders =
        [
            new(
                "dispatch-001",
                "point-102",
                "轮渡码头北口",
                "播放失败",
                "沿江慢直播保障一组",
                "沿江片区 / 轮渡码头北口",
                "故障截图占位 A",
                "播放失败告警快照",
                "设备在线但视频播放失败，本轮已按重复故障合并处理。",
                "故障",
                true,
                true,
                DispatchMethodModel.Manual,
                DispatchWorkOrderStatusModel.PendingDispatch,
                DispatchRecoveryStatusModel.Unrecovered,
                new("沿江运维一中心", "张诚", "13800000001", "李强", "13900000001"),
                new("--", "待发送", "--", "待发送"),
                new("2026-03-12 07:08", "2026-03-12 08:42", 3)),
            new(
                "dispatch-002",
                "point-105",
                "防汛泵站外侧",
                "设备离线",
                "沿江慢直播保障一组",
                "泵站外围 / 西北角",
                "故障截图占位 B",
                "离线告警快照",
                "点位离线且恢复前暂停巡检，已模拟自动派单。",
                "故障",
                true,
                true,
                DispatchMethodModel.Automatic,
                DispatchWorkOrderStatusModel.Dispatched,
                DispatchRecoveryStatusModel.Unrecovered,
                new("防汛保障中心", "周岭", "13800000012", "王征", "13900000012"),
                new("2026-03-12 07:12", "模拟发送成功", "--", "待发送"),
                new("2026-03-12 06:12", "2026-03-12 07:10", 2)),
            new(
                "dispatch-003",
                "point-106",
                "江心灯塔监看点",
                "设备离线",
                "夜景值守组",
                "江心灯塔 / 东南水域",
                "故障截图占位 C",
                "夜景离线快照",
                "夜间值守点位离线，当前仍在未恢复阶段。",
                "故障",
                true,
                false,
                DispatchMethodModel.Automatic,
                DispatchWorkOrderStatusModel.Dispatched,
                DispatchRecoveryStatusModel.Unrecovered,
                new("航道监护中心", "陈野", "13800000023", "高航", "13900000023"),
                new("2026-03-11 22:42", "模拟发送成功", "--", "待发送"),
                new("2026-03-11 22:40", "2026-03-12 06:51", 4)),
            new(
                "dispatch-004",
                "point-104",
                "城市阳台主广场",
                "画面异常",
                "文旅值守组",
                "城市阳台 / 主广场上空",
                "故障截图占位 D",
                "画面异常快照",
                "重点点位画面异常，已进入人工派单跟踪。",
                "故障",
                true,
                true,
                DispatchMethodModel.Manual,
                DispatchWorkOrderStatusModel.Dispatched,
                DispatchRecoveryStatusModel.Recovered,
                new("文旅联合中心", "林悦", "13800000034", "宋晨", "13900000034"),
                new("2026-03-12 07:56", "模拟发送成功", "2026-03-12 09:10", "模拟发送成功"),
                new("2026-03-12 07:28", "2026-03-12 07:54", 1)),
            new(
                "dispatch-005",
                "point-109",
                "滨江步道南入口",
                "播放失败",
                "沿江慢直播保障二组",
                "滨江步道 / 南入口立杆",
                "故障截图占位 E",
                "南入口播放失败快照",
                "新进入派单池的人工派单工单，等待管理员确认发送。",
                "故障",
                true,
                true,
                DispatchMethodModel.Manual,
                DispatchWorkOrderStatusModel.PendingDispatch,
                DispatchRecoveryStatusModel.Unrecovered,
                new("沿江运维二中心", "赵宁", "13800000045", "韩征", "13900000045"),
                new("--", "待发送", "--", "待发送"),
                new("2026-03-12 10:16", "2026-03-12 10:16", 1))
        ];

        return ServiceResponse<IReadOnlyList<DispatchWorkOrderModel>>.Success(workOrders);
    }

    public ServiceResponse<DispatchNotificationResult> SendFaultNotification(DispatchNotificationRequestDto request)
    {
        var sentAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        return ServiceResponse<DispatchNotificationResult>.Success(
            new DispatchNotificationResult(
                sentAt,
                "模拟发送成功",
                $"{request.CurrentHandlingUnit} / {request.MaintainerName}"));
    }

    public ServiceResponse<DispatchNotificationResult> SendRecoveryNotification(DispatchNotificationRequestDto request)
    {
        var sentAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        return ServiceResponse<DispatchNotificationResult>.Success(
            new DispatchNotificationResult(
                sentAt,
                "模拟发送成功",
                $"{request.CurrentHandlingUnit} / {request.MaintainerName}"));
    }
}
