namespace Cleanuparr.Domain.Entities.Arr;

public sealed record ArrTrackFile
{
    public long Id { get; init; }

    public long AlbumId { get; init; }

    public int CustomFormatScore { get; init; }
}
