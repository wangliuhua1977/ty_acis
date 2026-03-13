namespace TianyiVision.Acis.Services.Configuration;

public interface INotificationSettingsService
{
    DispatchNotificationSettings Load();

    void Save(DispatchNotificationSettings settings);
}
