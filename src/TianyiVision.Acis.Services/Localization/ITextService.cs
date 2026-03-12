using TianyiVision.Acis.Core.Localization;

namespace TianyiVision.Acis.Services.Localization;

public interface ITextService
{
    TerminologyProfile ActiveProfile { get; }

    IReadOnlyList<TerminologyProfile> GetAvailableProfiles();

    string Resolve(string key, IReadOnlyDictionary<string, string>? variableOverrides = null);

    void SetProfile(string profileId);
}
