using TianyiVision.Acis.Services.Contracts;
using TianyiVision.Acis.Services.Home;

namespace TianyiVision.Acis.Services.Demo;

public sealed class DemoHomeDashboardService : IHomeDashboardService
{
    public ServiceResponse<HomeDashboardSnapshot> GetDashboard()
    {
        var snapshot = new HomeDashboardSnapshot(
            "沿江慢直播保障一组",
            "8 / 12",
            "2 个待复核任务，优先关注沿江保障组与夜景值守组。",
            "5 条待派单故障，当前以播放失败和离线类为主。",
            [
                new("home-101", "江滨观景台 1 号位", "沿江运维一中心", HomeMapPointKindModel.Normal, 140, 210, "正常", "无故障", "当前画面和在线状态稳定，适合作为首页正常态示例。", "--", false),
                new("home-102", "轮渡码头北口", "沿江运维一中心", HomeMapPointKindModel.Fault, 320, 150, "故障", "播放失败", "当前以播放失败为主，后续应承接协议切换与重试过程。", "2026-03-12 08:42", true),
                new("home-103", "跨江大桥东塔", "桥梁联防中心", HomeMapPointKindModel.Inspecting, 530, 250, "巡检中", "无故障", "当前点位处于巡检中，用于展示首页态势联动骨架。", "--", false),
                new("home-104", "城市阳台主广场", "文旅联合中心", HomeMapPointKindModel.Key, 790, 130, "正常", "画面异常", "当前属于重点区域点位，首页优先保留其可视化态势。", "2026-03-12 07:54", true),
                new("home-105", "防汛泵站外侧", "防汛保障中心", HomeMapPointKindModel.Fault, 450, 390, "恢复前暂停巡检", "设备离线", "点位当前离线且处于恢复前暂停巡检状态。", "2026-03-12 07:10", true),
                new("home-106", "江心灯塔监看点", "航道监护中心", HomeMapPointKindModel.Fault, 700, 420, "故障", "设备离线", "该点位在首页维持高亮告警，用于强调地图主舞台的故障态势。", "2026-03-12 06:51", true),
                new("home-107", "文化展厅西侧", "文旅联合中心", HomeMapPointKindModel.Key, 980, 230, "正常", "无故障", "重点区域点位，当前状态正常。", "--", false),
                new("home-108", "景观桥步道口", "桥梁联防中心", HomeMapPointKindModel.Normal, 1120, 360, "正常", "无故障", "桥梁点位当前可稳定显示，用于首页整体态势铺陈。", "--", false)
            ],
            [
                new("home-102", "轮渡码头北口", "播放失败", "2026-03-12 08:42"),
                new("home-105", "防汛泵站外侧", "设备离线", "2026-03-12 07:10"),
                new("home-106", "江心灯塔监看点", "设备离线", "2026-03-12 06:51"),
                new("home-104", "城市阳台主广场", "画面异常", "2026-03-12 07:54")
            ]);

        return ServiceResponse<HomeDashboardSnapshot>.Success(snapshot);
    }
}
