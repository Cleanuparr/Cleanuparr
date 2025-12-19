namespace Cleanuparr.Infrastructure.Features.Files;

public interface IHardLinkFileService
{
    /// <summary>
    /// Populates the inode counts for Unix and the file index counts for Windows.
    /// Needs to be called before <see cref="GetHardLinkCount"/> to populate the inode counts.
    /// </summary>
    /// <param name="directoryPath">The root directory where to search for hardlinks.</param>
    void PopulateFileCounts(string directoryPath);

    /// <summary>
    /// Populates the inode counts for Unix and the file index counts for Windows from multiple directories.
    /// </summary>
    /// <param name="directoryPaths">The root directories where to search for hardlinks.</param>
    void PopulateFileCounts(IEnumerable<string> directoryPaths);
    
    /// <summary>
    /// Get the hardlink count of a file.
    /// </summary>
    /// <param name="filePath">File path.</param>
    /// <param name="ignoreRootDir">Whether to ignore hardlinks found in the same root dir.</param>
    /// <returns>-1 on error, 0 if there are no hardlinks and 1 otherwise.</returns>
    long GetHardLinkCount(string filePath, bool ignoreRootDir);
}