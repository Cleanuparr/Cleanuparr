﻿using Domain.Models.Deluge.Response;

namespace Infrastructure.Extensions;

public static class DelugeExtensions
{
    public static bool ShouldIgnore(this TorrentStatus download, IReadOnlyList<string> ignoredDownloads)
    {
        foreach (string value in ignoredDownloads)
        {
            if (download.Hash?.Equals(value, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }

            if (download.Label?.Equals(value, StringComparison.InvariantCultureIgnoreCase) ?? false)
            {
                return true;
            }
        }

        return false;
    }
}