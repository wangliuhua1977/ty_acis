namespace TianyiVision.Acis.Core.Theming;

public sealed class ThemeDefinition
{
    public ThemeDefinition(
        string id,
        string displayName,
        string description,
        IReadOnlyDictionary<string, string> colors)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Colors = colors;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public IReadOnlyDictionary<string, string> Colors { get; }

    public string GetColor(string token)
    {
        if (!Colors.TryGetValue(token, out var value))
        {
            throw new KeyNotFoundException($"Theme color token '{token}' is not defined for theme '{Id}'.");
        }

        return value;
    }
}
