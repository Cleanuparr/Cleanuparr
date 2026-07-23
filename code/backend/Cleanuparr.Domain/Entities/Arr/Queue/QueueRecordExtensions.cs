namespace Cleanuparr.Domain.Entities.Arr.Queue;

public static class QueueRecordExtensions
{
    public static bool IsIgnored(this QueueRecord record, IReadOnlyList<string> ignoredDownloads)
    {
        bool hasDownloadClient = !string.IsNullOrWhiteSpace(record.DownloadClient);

        foreach (string ignored in ignoredDownloads)
        {
            if (record.DownloadId.Equals(ignored, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            if (hasDownloadClient && record.DownloadClient!.Equals(ignored, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
