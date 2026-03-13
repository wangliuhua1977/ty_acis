namespace TianyiVision.Acis.Services.Configuration;

public interface IDispatchResponsibilitySettingsService
{
    DispatchResponsibilitySettings Load();

    void Save(DispatchResponsibilitySettings settings);
}
