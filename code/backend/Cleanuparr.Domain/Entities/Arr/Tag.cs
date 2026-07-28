namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// A tag of an *arr application.
/// </summary>
public sealed record Tag
{
    /// <summary>
    /// The ID of the tag.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The name of the tag.
    /// </summary>
    public string Label { get; init; } = string.Empty;
}
