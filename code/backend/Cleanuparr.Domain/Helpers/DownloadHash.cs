namespace Cleanuparr.Domain.Helpers;

/// <summary>
/// The canonical form of a torrent hash.
/// The *arr apps and the download clients disagree on casing, so everything we store and compare is lowercase.
/// </summary>
public static class DownloadHash
{
    /// <summary>
    /// Returns the canonical, lowercase form of a hash.
    /// </summary>
    public static string Normalize(string value) => value.ToLowerInvariant();
}
