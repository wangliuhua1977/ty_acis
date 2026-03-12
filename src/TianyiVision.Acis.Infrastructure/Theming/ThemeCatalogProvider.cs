using TianyiVision.Acis.Core.Contracts;
using TianyiVision.Acis.Core.Theming;

namespace TianyiVision.Acis.Infrastructure.Theming;

public sealed class ThemeCatalogProvider : IThemeCatalogProvider
{
    public IReadOnlyList<ThemeDefinition> GetThemes()
    {
        return
        [
            new ThemeDefinition(
                "deep-ocean-blue",
                "电信深海蓝",
                "稳重、专业、适合日常办公与领导查看。",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ThemeColorTokens.WindowBackground] = "#081525",
                    [ThemeColorTokens.ShellGradientStart] = "#0B1D34",
                    [ThemeColorTokens.ShellGradientEnd] = "#07111E",
                    [ThemeColorTokens.SidebarBackground] = "#0A1728",
                    [ThemeColorTokens.SurfacePrimary] = "#102338",
                    [ThemeColorTokens.SurfaceSecondary] = "#0D1D30",
                    [ThemeColorTokens.BorderPrimary] = "#214A6A",
                    [ThemeColorTokens.BorderStrong] = "#2C648E",
                    [ThemeColorTokens.TextPrimary] = "#E8F4FF",
                    [ThemeColorTokens.TextSecondary] = "#9FB9D2",
                    [ThemeColorTokens.AccentPrimary] = "#2FA8FF",
                    [ThemeColorTokens.AccentSecondary] = "#55D3FF",
                    [ThemeColorTokens.Success] = "#34D6A2",
                    [ThemeColorTokens.Warning] = "#F3B45B",
                    [ThemeColorTokens.Danger] = "#FF6C7A",
                    [ThemeColorTokens.InspectionActive] = "#5EC8FF",
                    [ThemeColorTokens.FaultBlink] = "#FF5A68",
                    [ThemeColorTokens.WorkbenchBackground] = "#08131F"
                }),
            new ThemeDefinition(
                "polar-night-fusion",
                "极夜科技蓝紫",
                "科技感更强，适合演示与态势展示。",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ThemeColorTokens.WindowBackground] = "#090A17",
                    [ThemeColorTokens.ShellGradientStart] = "#141635",
                    [ThemeColorTokens.ShellGradientEnd] = "#080913",
                    [ThemeColorTokens.SidebarBackground] = "#0F1027",
                    [ThemeColorTokens.SurfacePrimary] = "#191C3A",
                    [ThemeColorTokens.SurfaceSecondary] = "#12142D",
                    [ThemeColorTokens.BorderPrimary] = "#414889",
                    [ThemeColorTokens.BorderStrong] = "#5560AE",
                    [ThemeColorTokens.TextPrimary] = "#EFF0FF",
                    [ThemeColorTokens.TextSecondary] = "#A9AFD8",
                    [ThemeColorTokens.AccentPrimary] = "#6BA7FF",
                    [ThemeColorTokens.AccentSecondary] = "#9B7DFF",
                    [ThemeColorTokens.Success] = "#37D5C0",
                    [ThemeColorTokens.Warning] = "#FFB15A",
                    [ThemeColorTokens.Danger] = "#FF6F8E",
                    [ThemeColorTokens.InspectionActive] = "#74D4FF",
                    [ThemeColorTokens.FaultBlink] = "#FF4C74",
                    [ThemeColorTokens.WorkbenchBackground] = "#0A0B1A"
                }),
            new ThemeDefinition(
                "glacier-silver-gold",
                "冰川银蓝暖金",
                "清爽耐看，适合长时间值守与精细操作。",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ThemeColorTokens.WindowBackground] = "#0A1620",
                    [ThemeColorTokens.ShellGradientStart] = "#102432",
                    [ThemeColorTokens.ShellGradientEnd] = "#0B141B",
                    [ThemeColorTokens.SidebarBackground] = "#10202A",
                    [ThemeColorTokens.SurfacePrimary] = "#172C38",
                    [ThemeColorTokens.SurfaceSecondary] = "#10212B",
                    [ThemeColorTokens.BorderPrimary] = "#49677C",
                    [ThemeColorTokens.BorderStrong] = "#7598B1",
                    [ThemeColorTokens.TextPrimary] = "#F4FAFF",
                    [ThemeColorTokens.TextSecondary] = "#B3C6D4",
                    [ThemeColorTokens.AccentPrimary] = "#73C0FF",
                    [ThemeColorTokens.AccentSecondary] = "#DDB064",
                    [ThemeColorTokens.Success] = "#3CC7AA",
                    [ThemeColorTokens.Warning] = "#E2B45C",
                    [ThemeColorTokens.Danger] = "#FF7C7C",
                    [ThemeColorTokens.InspectionActive] = "#8CD5FF",
                    [ThemeColorTokens.FaultBlink] = "#FF6A5A",
                    [ThemeColorTokens.WorkbenchBackground] = "#0C1A23"
                })
        ];
    }
}
