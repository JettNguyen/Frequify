namespace Frequify.Models;

/// <summary>
/// Represents a mastering genre preset definition.
/// </summary>
public sealed class GenrePreset
{
    /// <summary>
    /// Initializes a new preset.
    /// </summary>
    /// <param name="name">Display name.</param>
    /// <param name="description">Plain-language intent text.</param>
    /// <param name="apply">Action that applies parameters.</param>
    public GenrePreset(string name, string description, Action<MasteringSettings> apply)
    {
        Name = name;
        Description = description;
        Apply = apply;
    }

    /// <summary>
    /// Gets preset display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets plain-language preset explanation.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets deterministic parameter adjustment action.
    /// </summary>
    public Action<MasteringSettings> Apply { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
