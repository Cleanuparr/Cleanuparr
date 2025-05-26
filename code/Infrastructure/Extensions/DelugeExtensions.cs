﻿using Data.Models.Deluge.Response;
using Infrastructure.Helpers;
using Infrastructure.Services;

namespace Infrastructure.Extensions;

public static class DelugeExtensions
{
    public static bool ShouldIgnore(this DownloadStatus download, IReadOnlyList<string> ignoredDownloads)
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
            
            if (download.Trackers.Any(x => UriService.GetDomain(x.Url)?.EndsWith(value, StringComparison.InvariantCultureIgnoreCase) ?? false))
            {
                return true;
            }
        }

        return false;
    }
}