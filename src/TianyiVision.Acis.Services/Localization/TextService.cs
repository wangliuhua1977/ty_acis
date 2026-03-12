using TianyiVision.Acis.Core.Contracts;
using TianyiVision.Acis.Core.Localization;

namespace TianyiVision.Acis.Services.Localization;

public sealed class TextService : ITextService
{
    private readonly IReadOnlyList<TerminologyProfile> _profiles;

    public TextService(ITerminologyCatalogProvider provider)
    {
        _profiles = provider.GetProfiles();
        ActiveProfile = _profiles.First();
    }

    public TerminologyProfile ActiveProfile { get; private set; }

    public IReadOnlyList<TerminologyProfile> GetAvailableProfiles() => _profiles;

    public string Resolve(string key, IReadOnlyDictionary<string, string>? variableOverrides = null)
        => ActiveProfile.Resolve(key, variableOverrides);

    public void SetProfile(string profileId)
    {
        ActiveProfile = _profiles.FirstOrDefault(profile => profile.Id == profileId) ?? ActiveProfile;
    }
}
