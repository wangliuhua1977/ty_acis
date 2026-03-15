using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Diagnostics;
using TianyiVision.Acis.Services.Inspection;

namespace TianyiVision.Acis.Services.Demo;

public sealed class DemoInspectionTaskService : IInspectionTaskService
{
    public ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace()
    {
        var snapshot = new InspectionWorkspaceSnapshot(
        [
            new(
                new InspectionGroupModel("g-telecom-river", "沿江慢直播保障一组", "12 项监看范围 · 上墙复核 · 自动派单", true),
                new InspectionStrategyModel("08:30", "4 次 / 日", "每 4 小时", "上墙复核", "自动派单"),
                new InspectionExecutionModel("2 / 4", "待执行", "14:30", "可在当前演示环境中继续推进模拟巡检。", true),
                new InspectionRunSummaryModel("沿江慢直播保障一组", "2026-03-12 09:10"),
                "2026-03-12 09:46",
                [
                    new("p-101", "江滨观景台 1 号位", "沿江运维一中心", "沿江维护班组", 90, 150, InspectionPointStatusModel.Normal, InspectionPointStatusModel.Normal, true, true, false, true, "2026-03-11 21:30", true),
                    new("p-102", "轮渡码头北口", "沿江运维一中心", "沿江维护班组", 270, 120, InspectionPointStatusModel.Fault, InspectionPointStatusModel.Fault, true, false, false, false, "2026-03-12 08:42", true),
                    new("p-103", "跨江大桥东塔", "桥梁联防中心", "桥梁值守班", 430, 190, InspectionPointStatusModel.Inspecting, InspectionPointStatusModel.Normal, true, true, false, true, "2026-03-10 18:12", false),
                    new("p-104", "城市阳台主广场", "文旅联合中心", "文旅夜景保障组", 600, 130, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Fault, true, true, true, false, "2026-03-09 20:44", true),
                    new("p-105", "滨江步道南段", "沿江运维二中心", "沿江维护班组", 700, 260, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Normal, true, true, false, true, "2026-03-08 16:02", false),
                    new("p-106", "地铁口联防点", "轨交换乘保障组", "轨道联动班", 220, 280, InspectionPointStatusModel.Silent, InspectionPointStatusModel.Silent, true, true, false, true, "--", false),
                    new("p-107", "防汛泵站外侧", "防汛保障中心", "防汛应急班", 360, 330, InspectionPointStatusModel.PausedUntilRecovery, InspectionPointStatusModel.PausedUntilRecovery, false, false, false, false, "2026-03-12 07:10", true),
                    new("p-108", "江心灯塔监看点", "航道监护中心", "航道维护组", 560, 320, InspectionPointStatusModel.Fault, InspectionPointStatusModel.Fault, false, false, false, false, "2026-03-12 06:51", true),
                    new("p-109", "文化展厅西侧", "文旅联合中心", "文旅夜景保障组", 790, 180, InspectionPointStatusModel.Normal, InspectionPointStatusModel.Normal, true, true, false, true, "2026-03-07 11:13", false),
                    new("p-110", "亲水平台北端", "沿江运维二中心", "沿江维护班组", 860, 310, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Normal, true, true, false, true, "--", false),
                    new("p-111", "演艺广场东门", "文旅联合中心", "文旅夜景保障组", 980, 150, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Fault, true, false, true, false, "--", true),
                    new("p-112", "景观桥步道口", "桥梁联防中心", "桥梁值守班", 1040, 260, InspectionPointStatusModel.Normal, InspectionPointStatusModel.Normal, true, true, false, true, "2026-03-06 13:27", false)
                ],
                [
                    new("p-102", "轮渡码头北口", "播放失败", "2026-03-12 08:42"),
                    new("p-108", "江心灯塔监看点", "设备离线", "2026-03-12 06:51"),
                    new("p-107", "防汛泵站外侧", "设备离线", "2026-03-12 07:10")
                ]),
            new(
                new InspectionGroupModel("g-city-night", "城区夜景值守二组", "10 项监看范围 · 直接派单 · 人工派单", true),
                new InspectionStrategyModel("19:00", "3 次 / 日", "每 3 小时", "直接进入派单", "人工派单"),
                new InspectionExecutionModel("1 / 3", "待执行", "22:00", "当前为夜景组演示任务，可继续触发模拟巡检。", true),
                new InspectionRunSummaryModel("城区夜景值守二组", "2026-03-12 19:05"),
                "2026-03-12 19:37",
                [
                    new("p-201", "城市中轴灯光秀主屏", "城区值守中心", "夜景值守班", 140, 140, InspectionPointStatusModel.Normal, InspectionPointStatusModel.Normal, true, true, false, true, "2026-03-05 09:20", false),
                    new("p-202", "会展中心南入口", "城区值守中心", "夜景值守班", 280, 210, InspectionPointStatusModel.Inspecting, InspectionPointStatusModel.Normal, true, true, false, true, "--", false),
                    new("p-203", "商业街 3 号塔", "商圈联防中心", "商圈维护组", 430, 120, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Fault, true, true, true, false, "--", true),
                    new("p-204", "游客集散广场", "文旅联合中心", "文旅夜景保障组", 580, 220, InspectionPointStatusModel.Fault, InspectionPointStatusModel.Fault, true, false, false, false, "2026-03-12 18:36", true),
                    new("p-205", "滨湖步道转角", "湖区保障组", "湖区值守班", 720, 160, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Normal, true, true, false, true, "--", false),
                    new("p-206", "交通枢纽东平台", "城区值守中心", "交通联动班", 860, 260, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Normal, true, true, false, true, "--", false),
                    new("p-207", "东湖观景塔", "湖区保障组", "湖区值守班", 980, 110, InspectionPointStatusModel.Silent, InspectionPointStatusModel.Silent, true, true, false, true, "--", false),
                    new("p-208", "主会场外场大屏", "商圈联防中心", "商圈维护组", 1030, 280, InspectionPointStatusModel.PausedUntilRecovery, InspectionPointStatusModel.PausedUntilRecovery, false, false, false, false, "2026-03-12 17:10", true),
                    new("p-209", "东门宣传屏", "城区值守中心", "夜景值守班", 650, 330, InspectionPointStatusModel.Normal, InspectionPointStatusModel.Normal, true, true, false, true, "2026-03-03 11:20", false),
                    new("p-210", "会展中心北侧广角位", "城区值守中心", "夜景值守班", 380, 330, InspectionPointStatusModel.Pending, InspectionPointStatusModel.Fault, true, false, true, false, "--", true)
                ],
                [
                    new("p-204", "游客集散广场", "播放失败", "2026-03-12 18:36"),
                    new("p-208", "主会场外场大屏", "设备离线", "2026-03-12 17:10")
                ])
        ]);

        return ServiceResponse<InspectionWorkspaceSnapshot>.Success(snapshot);
    }

    public ServiceResponse<SingleInspectionTaskRecordModel> StartSinglePointInspection(string pointId)
    {
        var startedAt = DateTime.Now;
        var point = GetWorkspace().Data.Groups
            .SelectMany(group => group.Points)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, pointId, StringComparison.Ordinal));

        if (point is null)
        {
            var failedRecord = CreateFailedRecord(pointId, pointId, pointId, startedAt);
            MapPointSourceDiagnostics.Write(
                "SingleInspection",
                $"demo single point inspection failed before task creation: pointId = {pointId}, reason = 点位未找到");
            return ServiceResponse<SingleInspectionTaskRecordModel>.Failure(failedRecord, $"未找到点位 {pointId}。");
        }

        var completedAt = DateTime.Now;
        var taskId = BuildTaskId();
        var completedRecord = new SingleInspectionTaskRecordModel(
            taskId,
            point.DeviceCode,
            point.Name,
            InspectionTaskStatusModel.Completed,
            startedAt,
            completedAt,
            "待接入");

        MapPointSourceDiagnostics.WriteLines("SingleInspection", [
            $"demo single point inspection task created: taskId = {taskId}, pointId = {point.Id}, deviceCode = {point.DeviceCode}, deviceName = {point.Name}, status = {InspectionTaskStatusModel.Pending}",
            $"demo single point inspection task started: taskId = {taskId}, pointId = {point.Id}, status = {InspectionTaskStatusModel.Running}",
            $"demo single point inspection task completed: taskId = {taskId}, pointId = {point.Id}, status = {completedRecord.TaskStatus}, startedAt = {startedAt:yyyy-MM-dd HH:mm:ss}, finishedAt = {completedAt:yyyy-MM-dd HH:mm:ss}, resultSummary = {completedRecord.ResultSummary}"
        ]);

        return ServiceResponse<SingleInspectionTaskRecordModel>.Success(completedRecord);
    }

    private static string BuildTaskId()
        => $"sip-{DateTime.Now:yyyyMMddHHmmssfff}";

    private static SingleInspectionTaskRecordModel CreateFailedRecord(
        string pointId,
        string deviceCode,
        string deviceName,
        DateTime startedAt)
    {
        var taskId = $"{BuildTaskId()}-failed";
        return new SingleInspectionTaskRecordModel(
            taskId,
            string.IsNullOrWhiteSpace(deviceCode) ? pointId : deviceCode,
            string.IsNullOrWhiteSpace(deviceName) ? pointId : deviceName,
            InspectionTaskStatusModel.Failed,
            startedAt,
            startedAt,
            "待接入");
    }
}
