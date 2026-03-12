namespace TianyiVision.Acis.Services.Settings;

public interface IAppPreferencesService
{
    AppPreferencesSnapshot Load();

    void Save(AppPreferencesSnapshot snapshot);

    void Reset();
}
