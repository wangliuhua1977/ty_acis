namespace TianyiVision.Acis.Core.Localization;

public sealed class TerminologyProfile
{
    public TerminologyProfile(
        string id,
        string displayName,
        IReadOnlyDictionary<string, string> textEntries,
        IReadOnlyDictionary<string, string> variables)
    {
        Id = id;
        DisplayName = displayName;
        TextEntries = textEntries;
        Variables = variables;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public IReadOnlyDictionary<string, string> TextEntries { get; }

    public IReadOnlyDictionary<string, string> Variables { get; }

    public string Resolve(string key, IReadOnlyDictionary<string, string>? variableOverrides = null)
    {
        if (!TextEntries.TryGetValue(key, out var template))
        {
            return key;
        }

        var resolved = template;
        foreach (var pair in Variables)
        {
            resolved = resolved.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.Ordinal);
        }

        if (variableOverrides is null)
        {
            return resolved;
        }

        foreach (var pair in variableOverrides)
        {
            resolved = resolved.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.Ordinal);
        }

        return resolved;
    }
}
