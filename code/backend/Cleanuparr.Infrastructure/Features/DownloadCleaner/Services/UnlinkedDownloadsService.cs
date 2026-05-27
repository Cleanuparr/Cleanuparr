using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Files;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadCleaner.Services;

/// <inheritdoc cref="IUnlinkedDownloadsService" />
public sealed class UnlinkedDownloadsService : IUnlinkedDownloadsService
{
    private readonly ILogger<UnlinkedDownloadsService> _logger;
    private readonly DataContext _dataContext;
    private readonly IHardLinkFileService _hardLinkFileService;

    public UnlinkedDownloadsService(
        ILogger<UnlinkedDownloadsService> logger,
        DataContext dataContext,
        IHardLinkFileService hardLinkFileService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _hardLinkFileService = hardLinkFileService;
    }

    public async Task ProcessAsync(IDownloadService downloadService, List<ITorrentItemWrapper> clientDownloads)
    {
        UnlinkedConfig? unlinkedConfig = await _dataContext.UnlinkedConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.DownloadClientConfigId == downloadService.ClientConfig.Id);

        if (unlinkedConfig is not { Enabled: true })
        {
            return;
        }
        
        if (unlinkedConfig.Categories.Count is 0)
        {
            _logger.LogWarning("Unlinked config is enabled but no categories are configured for {name}", downloadService.ClientConfig.Name);
            return;
        }

        if (unlinkedConfig.IgnoredRootDirs.Count > 0)
        {
            _hardLinkFileService.PopulateFileCounts(unlinkedConfig.IgnoredRootDirs);
        }

        try
        {
            List<ITorrentItemWrapper>? downloadsToChangeCategory = downloadService
                .FilterDownloadsToChangeCategoryAsync(clientDownloads, unlinkedConfig);

            if (downloadsToChangeCategory?.Count is null or 0)
            {
                return;
            }

            _logger.LogInformation("Evaluating {count} downloads for hardlinks", downloadsToChangeCategory.Count);

            try
            {
                await downloadService.CreateCategoryAsync(unlinkedConfig.TargetCategory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create category {category}", unlinkedConfig.TargetCategory);
            }

            await downloadService.ChangeCategoryForNoHardLinksAsync(downloadsToChangeCategory, unlinkedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process unlinked downloads for {clientName}", downloadService.ClientConfig.Name);
        }

        _logger.LogInformation("Finished hardlinks evaluation");
    }
}
