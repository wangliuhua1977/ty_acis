using TianyiVision.Acis.Core.Contracts;
using TianyiVision.Acis.Core.Localization;

namespace TianyiVision.Acis.Services.Localization;

public sealed class TextService : ITextService
{
    private readonly List<TerminologyProfile> _profiles;

    public TextService(ITerminologyCatalogProvider provider)
    {
        _profiles = provider.GetProfiles().ToList();
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

    public void SetProfile(TerminologyProfile profile)
    {
        var existingIndex = _profiles.FindIndex(item => item.Id == profile.Id);
        if (existingIndex >= 0)
        {
            _profiles[existingIndex] = profile;
        }
        else
        {
            _profiles.Add(profile);
        }

        ActiveProfile = profile;
    }
}
