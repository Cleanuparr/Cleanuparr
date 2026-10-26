namespace Cleanuparr.Domain.Entities.Arr;

public sealed record AlbumStatistics
{
    public int TrackFileCount { get; init; }

    public int TrackCount { get; init; }
}
