using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Files;

public class HardLinkFileService : IHardLinkFileService
{
    private readonly ILogger<HardLinkFileService> _logger;
    private readonly UnixHardLinkFileService _unixHardLinkFileService;
    private readonly WindowsHardLinkFileService _windowsHardLinkFileService;

    public HardLinkFileService(
        ILogger<HardLinkFileService> logger,
        UnixHardLinkFileService unixHardLinkFileService,
        WindowsHardLinkFileService windowsHardLinkFileService
    )
    {
        _logger = logger;
        _unixHardLinkFileService = unixHardLinkFileService;
        _windowsHardLinkFileService = windowsHardLinkFileService;
    }

    public void PopulateFileCounts(string directoryPath)
    {
        _logger.LogTrace("populating file counts from {dir}", directoryPath);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _windowsHardLinkFileService.PopulateFileCounts(directoryPath);
            return;
        }
        
        _unixHardLinkFileService.PopulateFileCounts(directoryPath);
    }

    public long GetHardLinkCount(string filePath, bool ignoreRootDir)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("file {file} does not exist", filePath);
            return -1;
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return _windowsHardLinkFileService.GetHardLinkCount(filePath, ignoreRootDir);
        }

        return _unixHardLinkFileService.GetHardLinkCount(filePath, ignoreRootDir);
    }
}