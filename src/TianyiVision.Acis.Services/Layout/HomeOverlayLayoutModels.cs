namespace TianyiVision.Acis.Services.Layout;

public sealed record HomeOverlayPanelLayout(string PanelId, double X, double Y, bool IsVisible);

public sealed record HomeOverlayLayoutSnapshot(IReadOnlyList<HomeOverlayPanelLayout> Panels);
