using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Cleanuparr.Infrastructure.Features.Files;

public class WindowsHardLinkFileService : IWindowsHardLinkFileService, IDisposable
{
    private readonly ILogger<WindowsHardLinkFileService> _logger;
    private readonly ConcurrentDictionary<ulong, (int Count, ConcurrentBag<string> Files)> _fileIndexCounts = new();
    private readonly ConcurrentDictionary<string, byte> _processedPaths = new();

    public WindowsHardLinkFileService(ILogger<WindowsHardLinkFileService> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public long GetHardLinkCount(string filePath, bool ignoreRootDir)
    {
        try
        {
            using SafeFileHandle fileStream = File.OpenHandle(filePath);

            if (!GetFileInformationByHandle(fileStream, out var file))
            {
                _logger.LogDebug("failed to get file handle {file}", filePath);
                return -1;
            }

            if (!ignoreRootDir)
            {
                _logger.LogDebug("stat file | hardlinks: {nlink} | {file}", file.NumberOfLinks, filePath);
                return file.NumberOfLinks == 1 ? 0 : 1;
            }

            // Get unique file ID (combination of high and low indices)
            ulong fileIndex = ((ulong)file.FileIndexHigh << 32) | file.FileIndexLow;
            
            // get the number of hardlinks in the same root directory
            int linksInIgnoredDir;
            if (_fileIndexCounts.TryGetValue(fileIndex, out var indexData))
            {
                linksInIgnoredDir = indexData.Count;
                _logger.LogDebug("stat file | hardlinks: {links} | ignored: {ignored} | {file}", file.NumberOfLinks, linksInIgnoredDir, filePath);
                _logger.LogDebug("linked files in ignored directory: {linkedFiles}", string.Join(", ", indexData.Files));
            }
            else
            {
                linksInIgnoredDir = 1; // default to 1 if not found
                _logger.LogDebug("stat file | hardlinks: {links} | ignored: {ignored} | {file}", file.NumberOfLinks, linksInIgnoredDir, filePath);
            }

            return file.NumberOfLinks - linksInIgnoredDir;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "failed to stat file {file}", filePath);
            return -1;
        }
    }

    /// <inheritdoc/>
    public void PopulateFileCounts(string directoryPath)
    {
        try
        {
            // traverse all files in the ignored path and subdirectories
            foreach (string file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                AddFileIndexToCount(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to populate file index counts from {dir}", directoryPath);
        }
    }

    private void AddFileIndexToCount(string path)
    {
        try
        {
            if (!_processedPaths.TryAdd(path, 0))
            {
                _logger.LogDebug("skipping already processed path: {path}", path);
                return;
            }

            using SafeFileHandle fileStream = File.OpenHandle(path);
            if (GetFileInformationByHandle(fileStream, out var file))
            {
                ulong fileIndex = ((ulong)file.FileIndexHigh << 32) | file.FileIndexLow;
                _fileIndexCounts.AddOrUpdate(
                    fileIndex,
                    _ => (1, new ConcurrentBag<string> { path }),
                    (_, existing) =>
                    {
                        existing.Files.Add(path);
                        return (existing.Count + 1, existing.Files);
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't stat {path} during file index counting", path);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation
    );

    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
    
    public void Dispose()
    {
        _fileIndexCounts.Clear();
        _processedPaths.Clear();
    }
}