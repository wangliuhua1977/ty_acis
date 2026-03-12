using TianyiVision.Acis.Core.Contracts;
using TianyiVision.Acis.Core.Localization;

namespace TianyiVision.Acis.Infrastructure.Localization;

public sealed class TerminologyCatalogProvider : ITerminologyCatalogProvider
{
    public IReadOnlyList<TerminologyProfile> GetProfiles()
    {
        var baseTextEntries = CreateBaseTextEntries();

        return
        [
            new TerminologyProfile(
                "telecom",
                "标准电信版",
                baseTextEntries,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [TerminologyVariables.PointNameTerm] = "点位",
                    [TerminologyVariables.InspectionTerm] = "巡检",
                    [TerminologyVariables.FaultTerm] = "故障",
                    [TerminologyVariables.HandlerTerm] = "排障维护人"
                }),
            new TerminologyProfile(
                "security",
                "通用安防版",
                baseTextEntries,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [TerminologyVariables.PointNameTerm] = "监控点",
                    [TerminologyVariables.InspectionTerm] = "巡查",
                    [TerminologyVariables.FaultTerm] = "告警",
                    [TerminologyVariables.HandlerTerm] = "处置人员"
                }),
            new TerminologyProfile(
                "tourism",
                "文旅景区版",
                baseTextEntries,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [TerminologyVariables.PointNameTerm] = "景观点",
                    [TerminologyVariables.InspectionTerm] = "巡看",
                    [TerminologyVariables.FaultTerm] = "异常",
                    [TerminologyVariables.HandlerTerm] = "保障人员"
                })
        ];
    }

    private static IReadOnlyDictionary<string, string> CreateBaseTextEntries()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TextTokens.ApplicationName] = "天翼视联 AI 智能巡检系统",
            [TextTokens.ShellCurrentUserLabel] = "当前用户",
            [TextTokens.ShellCurrentUserValue] = "本地管理员",
            [TextTokens.ShellCurrentTimeLabel] = "当前时间",
            [TextTokens.ShellSearchPlaceholder] = "全局搜索入口预留",
            [TextTokens.ShellThemeEntry] = "主题入口",
            [TextTokens.ShellSettingsEntry] = "设置入口",
            [TextTokens.ShellHeaderInspectionTasks] = "今日{inspection_term}任务",
            [TextTokens.ShellHeaderFaults] = "今日{fault_term}数",
            [TextTokens.ShellHeaderOutstanding] = "未恢复{fault_term}",
            [TextTokens.ShellHeaderRecovered] = "今日恢复数",
            [TextTokens.NavigationHome] = "首页",
            [TextTokens.NavigationInspection] = "AI{inspection_term}",
            [TextTokens.NavigationDispatch] = "{fault_term}派单处理",
            [TextTokens.NavigationReports] = "报表中心",
            [TextTokens.NavigationSettings] = "系统设置",
            [TextTokens.HomeTitle] = "首页总览",
            [TextTokens.HomeDescription] = "围绕首版三大业务模块提供总览入口，本轮仅交付稳定可启动的总览壳层。",
            [TextTokens.HomeMetricTasks] = "今日{inspection_term}任务数",
            [TextTokens.HomeMetricPoints] = "今日{point_name_term}数",
            [TextTokens.HomeMetricFaults] = "今日{fault_term}数",
            [TextTokens.HomeMetricRecovered] = "今日恢复数",
            [TextTokens.HomeTrendTitle] = "{fault_term}趋势区",
            [TextTokens.HomeTrendDescription] = "预留趋势图与模块联动入口，后续接入真实统计数据。",
            [TextTokens.HomeMapSummaryTitle] = "{inspection_term}地图摘要",
            [TextTokens.HomeMapSummaryDescription] = "预留地图摘要卡位，后续接入高德地图中台与状态联动。",
            [TextTokens.HomeFocusListTitle] = "重点未恢复{fault_term}清单",
            [TextTokens.HomeFocusListDescription] = "预留重点事项清单与右侧详情联动能力。",
            [TextTokens.InspectionTitle] = "AI{inspection_term}",
            [TextTokens.InspectionDescription] = "遵循基线的三栏式工作区，后续接入地图中台、点位联动与视频预览。",
            [TextTokens.InspectionTasksTitle] = "{inspection_term}任务区",
            [TextTokens.InspectionTasksDescription] = "保留巡检组切换、策略摘要、执行控制与计划时间入口。",
            [TextTokens.InspectionGroupTitle] = "{inspection_term}组切换",
            [TextTokens.InspectionGroupDescription] = "后续承接接口设备组合与组级策略配置。",
            [TextTokens.InspectionStrategyTitle] = "{inspection_term}策略摘要",
            [TextTokens.InspectionStrategyDescription] = "预留首次执行时间、每日执行次数、任务间隔和结果处理模式。",
            [TextTokens.InspectionExecutionTitle] = "执行控制",
            [TextTokens.InspectionExecutionDescription] = "预留串行调度、运行状态、下一次计划执行时间与人工触发入口。",
            [TextTokens.InspectionWorkbenchTitle] = "地图中台预留区",
            [TextTokens.InspectionWorkbenchDescription] = "本轮不接地图，仅保留中台工作区、状态图例和未来联动说明。",
            [TextTokens.InspectionLegendPending] = "待{inspection_term}",
            [TextTokens.InspectionLegendActive] = "{inspection_term}中高亮",
            [TextTokens.InspectionLegendFault] = "{fault_term}红灯闪烁",
            [TextTokens.InspectionDetailsTitle] = "当前{point_name_term}详情",
            [TextTokens.InspectionDetailsDescription] = "预留点位摘要、状态说明与地图双向联动后的详情刷新。",
            [TextTokens.InspectionPreviewTitle] = "视频预览区",
            [TextTokens.InspectionPreviewDescription] = "后续承接点位点击后的浮窗预览与播放状态说明。",
            [TextTokens.InspectionRecordTitle] = "{inspection_term}记录摘要",
            [TextTokens.InspectionRecordDescription] = "预留最近检查结果、简要异常说明与复核去向。",
            [TextTokens.DispatchTitle] = "{fault_term}派单处理",
            [TextTokens.DispatchDescription] = "遵循左筛选区、中工单列表、右详情区结构，不引入复杂状态机。",
            [TextTokens.DispatchFilterTitle] = "{fault_term}池筛选区",
            [TextTokens.DispatchFilterDescription] = "预留按巡检组、单位、责任人、故障类型和快捷状态筛选。",
            [TextTokens.DispatchStatusTitle] = "状态快捷筛选",
            [TextTokens.DispatchStatusDescription] = "保留待派单、已派单、未恢复、已恢复、自动派单、人工派单等入口。",
            [TextTokens.DispatchListTitle] = "工单列表区",
            [TextTokens.DispatchListDescription] = "后续展示极简工单主状态与恢复状态，并承接同点位同故障合并策略。",
            [TextTokens.DispatchMergeTitle] = "重复{fault_term}合并规则",
            [TextTokens.DispatchMergeDescription] = "预留首次时间、最近时间、重复次数与最新截图字段。",
            [TextTokens.DispatchDetailTitle] = "工单详情区",
            [TextTokens.DispatchDetailDescription] = "后续承接基础信息、责任归属、通知记录与恢复确认。",
            [TextTokens.DispatchEvidenceTitle] = "{fault_term}证据区",
            [TextTokens.DispatchEvidenceDescription] = "预留截图、说明和恢复前后证据对比能力。",
            [TextTokens.ReportsTitle] = "报表中心",
            [TextTokens.ReportsDescription] = "首版只围绕巡检执行、故障统计、派单处置、责任归属和重点未恢复清单。",
            [TextTokens.ReportsFilterTitle] = "筛选区",
            [TextTokens.ReportsFilterDescription] = "预留时间范围、巡检组、当前处理单位、故障类型筛选。",
            [TextTokens.ReportsMetricInspection] = "{inspection_term}执行报表",
            [TextTokens.ReportsMetricFault] = "{fault_term}统计报表",
            [TextTokens.ReportsMetricDispatch] = "派单处置报表",
            [TextTokens.ReportsMetricOutstanding] = "重点未恢复{fault_term}清单",
            [TextTokens.ReportsChartTrendTitle] = "{fault_term}趋势图",
            [TextTokens.ReportsChartTrendDescription] = "预留趋势数据展示和图表联动过滤逻辑。",
            [TextTokens.ReportsChartTypeTitle] = "{fault_term}类型占比图",
            [TextTokens.ReportsChartTypeDescription] = "后续接入离线、播放失败、画面异常等首版故障类型。",
            [TextTokens.ReportsChartRecoveryTitle] = "派单与恢复趋势图",
            [TextTokens.ReportsChartRecoveryDescription] = "预留派单量、恢复量与闭环结果联动展示。",
            [TextTokens.ReportsTableTitle] = "明细表区",
            [TextTokens.ReportsTableDescription] = "预留五类报表切换、查看与导出入口。",
            [TextTokens.SettingsTitle] = "系统设置",
            [TextTokens.SettingsDescription] = "本轮不展示底层接入参数，仅保留主题、术语和本地配置说明入口。",
            [TextTokens.SettingsThemeCenterTitle] = "主题中心预留",
            [TextTokens.SettingsThemeCenterDescription] = "基于统一主题令牌预置三套主题方案，后续支持复制、预览和另存为自定义主题。",
            [TextTokens.SettingsTerminologyCenterTitle] = "术语中心预留",
            [TextTokens.SettingsTerminologyCenterDescription] = "基于术语变量和方案级覆盖，后续支持行业术语切换与单项微调。",
            [TextTokens.SettingsConfigTitle] = "本地配置说明",
            [TextTokens.SettingsConfigDescription] = "API 地址、Token、Key、webhook 等底层参数统一走本地配置文件读取，不在界面展示。",
            [TextTokens.SettingsStructureTitle] = "骨架扩展说明",
            [TextTokens.SettingsStructureDescription] = "当前解决方案已为 AI 巡检、派单处理、报表、主题中心和术语中心预留稳定分层与页面入口。",
            [TextTokens.SettingsActiveTheme] = "当前主题",
            [TextTokens.SettingsActiveTerminology] = "当前术语方案",
            [TextTokens.SettingsAvailableThemes] = "预设主题",
            [TextTokens.SettingsAvailableTerminologies] = "预设术语方案"
        };
    }
}
