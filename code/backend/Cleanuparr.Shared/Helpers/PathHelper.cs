namespace Cleanuparr.Shared.Helpers;

/// <summary>
/// Helpers for working with file system paths.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Remaps a file path by replacing a source directory prefix with a target directory prefix.
    /// Checks path-segment boundaries to avoid false matches
    /// (e.g. source "/downloads" does not match "/downloads-other/file.mkv").
    /// </summary>
    /// <param name="filePath">The file path to remap.</param>
    /// <param name="source">The directory prefix to replace (e.g. "/downloads").</param>
    /// <param name="target">The replacement directory prefix (e.g. "/mnt/media").</param>
    /// <returns>The remapped path, or <paramref name="filePath"/> unchanged if no match.</returns>
    public static string RemapPath(string filePath, string? source, string? target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            return filePath;
        }

        var normSource = source.TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
        var normTarget = target.TrimEnd('/', '\\');

        // Exact match: filePath is exactly the source directory (no trailing separator)
        if (filePath.Equals(normSource.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return normTarget;
        }

        // Prefix match with path-segment boundary: filePath starts with "source/"
        if (filePath.StartsWith(normSource, StringComparison.OrdinalIgnoreCase))
        {
            return normTarget + Path.DirectorySeparatorChar + filePath[normSource.Length..];
        }

        return filePath;
    }
}
