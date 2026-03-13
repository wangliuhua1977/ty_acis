using System.Windows;
using System.Windows.Media;
using TianyiVision.Acis.Core.Theming;

namespace TianyiVision.Acis.App;

internal static class ThemeResourceBuilder
{
    public static ResourceDictionary Build(ThemeDefinition theme)
    {
        var resources = new ResourceDictionary();

        var windowBackground = theme.GetColor(ThemeColorTokens.WindowBackground);
        var sidebarBackground = theme.GetColor(ThemeColorTokens.SidebarBackground);
        var surfacePrimary = theme.GetColor(ThemeColorTokens.SurfacePrimary);
        var surfaceSecondary = theme.GetColor(ThemeColorTokens.SurfaceSecondary);
        var borderPrimary = theme.GetColor(ThemeColorTokens.BorderPrimary);
        var borderStrong = theme.GetColor(ThemeColorTokens.BorderStrong);
        var textPrimary = theme.GetColor(ThemeColorTokens.TextPrimary);
        var textSecondary = theme.GetColor(ThemeColorTokens.TextSecondary);
        var accentPrimary = theme.GetColor(ThemeColorTokens.AccentPrimary);
        var accentSecondary = theme.GetColor(ThemeColorTokens.AccentSecondary);
        var success = theme.GetColor(ThemeColorTokens.Success);
        var warning = theme.GetColor(ThemeColorTokens.Warning);
        var danger = theme.GetColor(ThemeColorTokens.Danger);
        var inspectionActive = theme.GetColor(ThemeColorTokens.InspectionActive);
        var faultBlink = theme.GetColor(ThemeColorTokens.FaultBlink);
        var workbenchBackground = theme.GetColor(ThemeColorTokens.WorkbenchBackground);

        AddBrush(resources, ThemeBrushKeys.WindowBackgroundBrush, windowBackground);
        AddBrush(resources, ThemeBrushKeys.SidebarBackgroundBrush, sidebarBackground);
        AddBrush(resources, ThemeBrushKeys.PanelBackgroundBrush, surfacePrimary);
        AddBrush(resources, ThemeBrushKeys.PanelMutedBackgroundBrush, surfaceSecondary);
        AddBrush(resources, ThemeBrushKeys.PanelBorderBrush, borderPrimary);
        AddBrush(resources, ThemeBrushKeys.PanelBorderStrongBrush, borderStrong);
        AddBrush(resources, ThemeBrushKeys.TextPrimaryBrush, textPrimary);
        AddBrush(resources, ThemeBrushKeys.TextSecondaryBrush, textSecondary);
        AddBrush(resources, ThemeBrushKeys.AccentBrush, accentPrimary);
        AddBrush(resources, ThemeBrushKeys.AccentSecondaryBrush, accentSecondary);
        AddBrush(resources, ThemeBrushKeys.SuccessBrush, success);
        AddBrush(resources, ThemeBrushKeys.SuccessSoftBrush, WithOpacity(success, 0.18));
        AddBrush(resources, ThemeBrushKeys.WarningBrush, warning);
        AddBrush(resources, ThemeBrushKeys.WarningSoftBrush, WithOpacity(warning, 0.18));
        AddBrush(resources, ThemeBrushKeys.DangerBrush, danger);
        AddBrush(resources, ThemeBrushKeys.DangerSoftBrush, WithOpacity(danger, 0.18));
        AddBrush(resources, ThemeBrushKeys.InspectionActiveBrush, inspectionActive);
        AddBrush(resources, ThemeBrushKeys.InspectionActiveSoftBrush, WithOpacity(inspectionActive, 0.18));
        AddBrush(resources, ThemeBrushKeys.FaultBlinkBrush, faultBlink);
        AddBrush(resources, ThemeBrushKeys.WorkbenchBackgroundBrush, workbenchBackground);
        AddBrush(resources, ThemeBrushKeys.MapGridOverlayBrush, WithOpacity(borderStrong, 0.20));
        AddBrush(resources, ThemeBrushKeys.AccentSoftBrush, WithOpacity(accentPrimary, 0.18));
        AddBrush(resources, ThemeBrushKeys.SelectionBackgroundBrush, WithOpacity(accentSecondary, 0.14));
        AddBrush(resources, ThemeBrushKeys.SelectionBorderBrush, accentSecondary);

        AddBrush(resources, ThemeBrushKeys.ScrollBarTrackBrush, Blend(surfaceSecondary, workbenchBackground, 0.34));
        AddBrush(resources, ThemeBrushKeys.ScrollBarThumbBrush, WithOpacity(Blend(borderStrong, accentPrimary, 0.18), 0.62));
        AddBrush(resources, ThemeBrushKeys.ScrollBarThumbHoverBrush, WithOpacity(Blend(borderStrong, accentSecondary, 0.32), 0.84));
        AddBrush(resources, ThemeBrushKeys.ScrollBarThumbPressedBrush, Blend(accentPrimary, accentSecondary, 0.55));
        AddBrush(resources, ThemeBrushKeys.ScrollBarCornerBrush, Blend(surfaceSecondary, windowBackground, 0.42));

        AddBrush(resources, ThemeBrushKeys.InputBackgroundBrush, Blend(surfaceSecondary, workbenchBackground, 0.28));
        AddBrush(resources, ThemeBrushKeys.InputBorderBrush, WithOpacity(Blend(borderPrimary, windowBackground, 0.18), 0.86));
        AddBrush(resources, ThemeBrushKeys.InputHoverBorderBrush, Blend(borderStrong, accentPrimary, 0.24));
        AddBrush(resources, ThemeBrushKeys.InputFocusBorderBrush, Blend(accentPrimary, accentSecondary, 0.40));
        AddBrush(resources, ThemeBrushKeys.InputInvalidBorderBrush, Blend(danger, warning, 0.10));
        AddBrush(resources, ThemeBrushKeys.InputForegroundBrush, textPrimary);
        AddBrush(resources, ThemeBrushKeys.InputPlaceholderBrush, WithOpacity(textSecondary, 0.78));

        AddBrush(resources, ThemeBrushKeys.ComboBoxBackgroundBrush, Blend(surfaceSecondary, workbenchBackground, 0.30));
        AddBrush(resources, ThemeBrushKeys.ComboBoxBorderBrush, Blend(borderPrimary, windowBackground, 0.14));
        AddBrush(resources, ThemeBrushKeys.ComboBoxHoverBorderBrush, WithOpacity(Blend(borderStrong, accentPrimary, 0.12), 0.74));
        AddBrush(resources, ThemeBrushKeys.ComboBoxFocusBorderBrush, WithOpacity(Blend(borderStrong, accentSecondary, 0.18), 0.84));
        AddBrush(resources, ThemeBrushKeys.ComboBoxOpenBorderBrush, WithOpacity(Blend(borderStrong, accentSecondary, 0.24), 0.92));
        AddBrush(resources, ThemeBrushKeys.ComboBoxArrowBrush, Blend(textSecondary, accentSecondary, 0.16));
        AddBrush(resources, ThemeBrushKeys.ComboBoxArrowHoverBrush, WithOpacity(Blend(textSecondary, accentPrimary, 0.22), 0.92));
        AddBrush(resources, ThemeBrushKeys.ComboBoxArrowOpenBrush, WithOpacity(Blend(textPrimary, accentSecondary, 0.18), 0.96));
        AddBrush(resources, ThemeBrushKeys.ComboBoxDividerBrush, WithOpacity(Blend(borderPrimary, windowBackground, 0.08), 0.52));
        AddBrush(resources, ThemeBrushKeys.ComboBoxPopupBackgroundBrush, Blend(surfacePrimary, workbenchBackground, 0.10));
        AddBrush(resources, ThemeBrushKeys.ComboBoxPopupBorderBrush, WithOpacity(Blend(borderStrong, accentPrimary, 0.10), 0.72));
        AddBrush(resources, ThemeBrushKeys.ComboBoxItemHoverBrush, WithOpacity(Blend(accentPrimary, surfacePrimary, 0.14), 0.10));
        AddBrush(resources, ThemeBrushKeys.ComboBoxItemSelectedBrush, WithOpacity(Blend(accentSecondary, surfacePrimary, 0.16), 0.14));

        AddBrush(resources, ThemeBrushKeys.DataGridHeaderBackgroundBrush, Blend(surfacePrimary, borderPrimary, 0.10));
        AddBrush(resources, ThemeBrushKeys.DataGridHeaderForegroundBrush, textPrimary);
        AddBrush(resources, ThemeBrushKeys.DataGridHeaderBorderBrush, WithOpacity(borderStrong, 0.62));
        AddBrush(resources, ThemeBrushKeys.DataGridRowBackgroundBrush, Blend(surfaceSecondary, workbenchBackground, 0.18));
        AddBrush(resources, ThemeBrushKeys.DataGridRowAlternateBackgroundBrush, Blend(surfaceSecondary, windowBackground, 0.26));
        AddBrush(resources, ThemeBrushKeys.DataGridRowHoverBrush, WithOpacity(accentPrimary, 0.10));
        AddBrush(resources, ThemeBrushKeys.DataGridRowSelectedBrush, WithOpacity(accentSecondary, 0.18));
        AddBrush(resources, ThemeBrushKeys.DataGridGridLineBrush, WithOpacity(borderPrimary, 0.38));

        AddBrush(resources, ThemeBrushKeys.TabBackgroundBrush, WithOpacity(surfaceSecondary, 0.82));
        AddBrush(resources, ThemeBrushKeys.TabBorderBrush, WithOpacity(borderPrimary, 0.68));
        AddBrush(resources, ThemeBrushKeys.TabForegroundBrush, textSecondary);
        AddBrush(resources, ThemeBrushKeys.TabHoverBackgroundBrush, WithOpacity(Blend(surfacePrimary, accentPrimary, 0.12), 0.92));
        AddBrush(resources, ThemeBrushKeys.TabSelectedBackgroundBrush, WithOpacity(Blend(surfacePrimary, accentSecondary, 0.18), 0.96));
        AddBrush(resources, ThemeBrushKeys.TabSelectedForegroundBrush, textPrimary);
        AddBrush(resources, ThemeBrushKeys.TabContainerBackgroundBrush, Blend(surfaceSecondary, windowBackground, 0.36));

        resources[ThemeBrushKeys.ShellGradientBrush] = new LinearGradientBrush(
            ToColor(theme.GetColor(ThemeColorTokens.ShellGradientStart)),
            ToColor(theme.GetColor(ThemeColorTokens.ShellGradientEnd)),
            new Point(0, 0),
            new Point(1, 1));

        return resources;
    }

    private static void AddBrush(ResourceDictionary resources, string key, string colorValue)
    {
        var brush = new SolidColorBrush(ToColor(colorValue));
        brush.Freeze();
        resources[key] = brush;
    }

    private static string WithOpacity(string colorValue, double opacity)
    {
        var color = ToColor(colorValue);
        return $"#{(byte)(opacity * 255):X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static string Blend(string firstColorValue, string secondColorValue, double amount)
    {
        var first = ToColor(firstColorValue);
        var second = ToColor(secondColorValue);

        byte BlendChannel(byte start, byte end)
            => (byte)Math.Clamp(Math.Round(start + ((end - start) * amount)), byte.MinValue, byte.MaxValue);

        return $"#{BlendChannel(first.A, second.A):X2}{BlendChannel(first.R, second.R):X2}{BlendChannel(first.G, second.G):X2}{BlendChannel(first.B, second.B):X2}";
    }

    private static Color ToColor(string value)
        => (Color)ColorConverter.ConvertFromString(value)!;
}
