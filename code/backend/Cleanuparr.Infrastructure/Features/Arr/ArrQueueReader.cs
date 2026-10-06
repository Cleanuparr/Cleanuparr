using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public sealed class ArrQueueReader : IArrQueueReader
{
    private readonly ILogger<ArrQueueReader> _logger;
    
    public ArrQueueReader(ILogger<ArrQueueReader> logger)
    {
        _logger = logger;
    }
    
    public async Task<List<QueueRecord>> ReadAllAsync(IArrClient arrClient, ArrInstance arrInstance)
    {
        const ushort maxPage = 100;
        ushort page = 1;
        int totalRecords = 0;
        List<QueueRecord> records = [];

        do
        {
            QueueListResponse queueResponse = await arrClient.GetQueueItemsAsync(arrInstance, page);
            
            if (totalRecords is 0)
            {
                totalRecords = queueResponse.TotalRecords;
                
                _logger.LogDebug(
                    "{items} items found in queue | {url}",
                    queueResponse.TotalRecords, arrInstance.Url);
            }

            if (queueResponse.Records.Count is 0)
            {
                break;
            }
            
            records.AddRange(queueResponse.Records);

            if (records.Count >= totalRecords)
            {
                break;
            }

            page++;
        } while (records.Count < totalRecords && page < maxPage);

        return records;
    }
}