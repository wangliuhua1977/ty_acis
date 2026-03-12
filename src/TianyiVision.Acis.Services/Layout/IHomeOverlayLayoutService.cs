namespace TianyiVision.Acis.Services.Layout;

public interface IHomeOverlayLayoutService
{
    HomeOverlayLayoutSnapshot Load();

    void Save(HomeOverlayLayoutSnapshot snapshot);

    void Reset();
}
