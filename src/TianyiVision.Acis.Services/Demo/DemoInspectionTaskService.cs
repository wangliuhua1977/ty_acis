using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Inspection;

namespace TianyiVision.Acis.Services.Demo;

public sealed class DemoInspectionTaskService : IInspectionTaskService
{
    private const string DemoGroupId = "demo-inspection-group";
    private const string DemoGroupName = "演示巡检组";

    public event EventHandler<InspectionTaskBoardChangedEventArgs>? TaskBoardChanged;

    public ServiceResponse<InspectionWorkspaceSnapshot> GetWorkspace()
    {
        var emptyTask = new InspectionTaskRecordModel(
            "demo-task-empty",
            DemoGroupId,
            DemoGroupName,
            "待发起演示任务",
            InspectionTaskTypeModel.ScopePlan,
            InspectionTaskTriggerModel.Manual,
            InspectionTaskStatusModel.Pending,
            DateTime.Now,
            null,
            null,
            null,
            "演示默认范围",
            0,
            0,
            0,
            0,
            null,
            "--",
            "演示分支未接入真实任务执行骨架。",
            []);

        var snapshot = new InspectionWorkspaceSnapshot([
            new InspectionGroupWorkspaceModel(
                new InspectionGroupModel(DemoGroupId, DemoGroupName, "演示任务分支，仅用于保底编译。", true),
                new InspectionStrategyModel("08:30", "预留演示", "同组串行", "异常先进入上墙复核", "人工确认派单"),
                new InspectionExecutionModel("0 次", "待执行", "预留定时", emptyTask.Summary, true),
                new InspectionRunSummaryModel(DemoGroupName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                "--",
                new InspectionTaskBoardModel(emptyTask, []),
                [],
                [])
        ]);

        return ServiceResponse<InspectionWorkspaceSnapshot>.Success(snapshot);
    }

    public ServiceResponse<InspectionTaskRecordModel> StartSinglePointInspection(string groupId, string pointId)
        => ServiceResponse<InspectionTaskRecordModel>.Failure(
            CreateRejectedTask(groupId, $"演示点位 {pointId}", InspectionTaskTypeModel.SinglePoint),
            "演示分支未接入任务执行。");

    public ServiceResponse<InspectionTaskRecordModel> StartBatchInspection(string groupId, IReadOnlyList<string> pointIds)
        => ServiceResponse<InspectionTaskRecordModel>.Failure(
            CreateRejectedTask(groupId, "演示批量任务", InspectionTaskTypeModel.Batch),
            "演示分支未接入任务执行。");

    public ServiceResponse<InspectionTaskRecordModel> StartDefaultScopeInspection(string groupId)
        => ServiceResponse<InspectionTaskRecordModel>.Failure(
            CreateRejectedTask(groupId, "演示范围任务", InspectionTaskTypeModel.ScopePlan),
            "演示分支未接入任务执行。");

    public ServiceResponse<InspectionTaskRecordModel> WritePointEvidence(InspectionPointEvidenceWriteRequest request)
        => ServiceResponse<InspectionTaskRecordModel>.Failure(
            CreateRejectedTask(request.TaskId, "婕旂ず鍙栬瘉鍐欏洖", InspectionTaskTypeModel.SinglePoint),
            "婕旂ず鍒嗘敮鏈帴鍏ョ湡瀹炲彇璇侀摼璺€?");

    private static InspectionTaskRecordModel CreateRejectedTask(
        string groupId,
        string taskName,
        InspectionTaskTypeModel taskType)
    {
        return new InspectionTaskRecordModel(
            $"demo-rejected-{DateTime.Now:yyyyMMddHHmmssfff}",
            string.IsNullOrWhiteSpace(groupId) ? DemoGroupId : groupId,
            DemoGroupName,
            taskName,
            taskType,
            InspectionTaskTriggerModel.Manual,
            InspectionTaskStatusModel.Cancelled,
            DateTime.Now,
            null,
            DateTime.Now,
            null,
            "演示默认范围",
            0,
            0,
            0,
            0,
            null,
            "--",
            "演示分支未接入任务执行。",
            []);
    }
}
