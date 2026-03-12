using TianyiVision.Acis.Core.Localization;

namespace TianyiVision.Acis.Core.Contracts;

public interface ITerminologyCatalogProvider
{
    IReadOnlyList<TerminologyProfile> GetProfiles();
}
