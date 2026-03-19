namespace Cleanuparr.Domain.Entities.Arr;

public sealed record ArrQualityProfile
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int CutoffFormatScore { get; init; }
}
