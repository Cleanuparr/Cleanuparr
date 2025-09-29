namespace Cleanuparr.Domain.Entities;

public interface ITorrentInfo
{
    string Hash { get; }
    string Name { get; }
    bool IsPrivate { get; }
    long Size { get; }
    double CompletionPercentage { get; }
    IReadOnlyList<string> Trackers { get; }
    long DownloadedBytes { get; }
}