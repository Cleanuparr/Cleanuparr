using System.IO.Compression;
using Serilog.Sinks.File;

namespace Cleanuparr.Infrastructure.Logging;

// Enhanced from Serilog.Sinks.File.Archive https://github.com/cocowalla/serilog-sinks-file-archive/blob/master/src/Serilog.Sinks.File.Archive/ArchiveHooks.cs
public class ArchiveHooks : FileLifecycleHooks
{
    private readonly CompressionLevel _compressionLevel;
    private readonly ushort _retainedFileCountLimit;
    private readonly TimeSpan? _retainedFileTimeLimit;

    public ArchiveHooks(
        ushort retainedFileCountLimit,
        TimeSpan? retainedFileTimeLimit,
        CompressionLevel compressionLevel = CompressionLevel.Fastest
    )
    {
        if (compressionLevel is CompressionLevel.NoCompression)
        {
            throw new ArgumentException($"{nameof(compressionLevel)} cannot be {CompressionLevel.NoCompression}");
        }
        
        if (retainedFileCountLimit is 0 && retainedFileTimeLimit is null)
        {
            throw new ArgumentException($"At least one of {nameof(retainedFileCountLimit)} or {nameof(retainedFileTimeLimit)} must be set");
        }
        
        _retainedFileCountLimit = retainedFileCountLimit;
        _retainedFileTimeLimit = retainedFileTimeLimit;
        _compressionLevel = compressionLevel;
    }

    public override void OnFileDeleting(string path)
    {
        FileInfo originalFileInfo = new FileInfo(path);
        string newFilePath = $"{path}.gz";

        using (FileStream originalFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (FileStream newFileStream = new FileStream(newFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                using (GZipStream archiveStream = new GZipStream(newFileStream, _compressionLevel))
                {
                    originalFileStream.CopyTo(archiveStream);
                }
            }
        }
        
        File.SetLastWriteTime(newFilePath, originalFileInfo.LastWriteTime);
        File.SetLastWriteTimeUtc(newFilePath, originalFileInfo.LastWriteTimeUtc);
        
        RemoveExcessFiles(Path.GetDirectoryName(path)!);
    }
    
    private void RemoveExcessFiles(string folder)
    {
        string searchPattern = _compressionLevel != CompressionLevel.NoCompression ? "*.gz" : "*.*";
        IEnumerable<FileInfo> filesToDeleteQuery = Directory.GetFiles(folder, searchPattern)
            .Select((Func<string, FileInfo>)(f => new FileInfo(f)))
            .OrderByDescending((Func<FileInfo, FileInfo>)(f => f), LogFileComparer.Default);

        if (_retainedFileCountLimit > 0)
        {
            filesToDeleteQuery = filesToDeleteQuery
                .Skip(_retainedFileCountLimit);
        }

        if (_retainedFileTimeLimit is not null)
        {
            filesToDeleteQuery = filesToDeleteQuery
                .Where(file => file.LastWriteTimeUtc < DateTime.UtcNow - _retainedFileTimeLimit);
        }
        
        List<FileInfo> filesToDelete = filesToDeleteQuery.ToList();
        
        foreach (FileInfo fileInfo in filesToDelete)
        {
            fileInfo.Delete();
        }
    }

    private class LogFileComparer : IComparer<FileInfo>
    {
        public static readonly IComparer<FileInfo> Default = new LogFileComparer();

        public int Compare(FileInfo? x, FileInfo? y)
        {
            if (x == null && y == null)
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null || x.LastWriteTimeUtc > y.LastWriteTimeUtc)
            {
                return 1;
            }
            
            return x.LastWriteTimeUtc < y.LastWriteTimeUtc ? -1 : 0;
        }
    }
}