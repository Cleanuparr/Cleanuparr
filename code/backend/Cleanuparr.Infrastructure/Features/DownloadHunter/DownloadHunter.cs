using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Interfaces;
using Cleanuparr.Infrastructure.Features.DownloadHunter.Models;
using Cleanuparr.Persistence;
using Data.Models.Arr;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Infrastructure.Features.DownloadHunter;

public sealed class DownloadHunter : IDownloadHunter
{
    private readonly DataContext _dataContext;
    private readonly ArrClientFactory _arrClientFactory;
    
    public DownloadHunter(
        DataContext dataContext,
        ArrClientFactory arrClientFactory
    )
    {
        _dataContext = dataContext;
        _arrClientFactory = arrClientFactory;
    }
    
    public async Task HuntDownloadsAsync<T>(DownloadHuntRequest<T> request)
        where T : SearchItem
    {
        var generalConfig = await _dataContext.GeneralConfigs
            .AsNoTracking()
            .FirstAsync();

        if (!generalConfig.SearchEnabled)
        {
            return;
        }
        
        var arrClient = _arrClientFactory.GetClient(request.InstanceType);
        await arrClient.SearchItemsAsync(request.Instance, [request.SearchItem]);
        
        // prevent tracker spamming
        await Task.Delay(TimeSpan.FromSeconds(generalConfig.SearchDelay));
    }
}