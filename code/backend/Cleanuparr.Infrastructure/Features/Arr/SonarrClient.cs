using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class SonarrClient : SonarrClientBase, IUpgradeDetectionCapable
{
    public SonarrClient(
        ILogger<SonarrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    /// <inheritdoc/>
    public bool? IsUpgrade(IReadOnlyList<QueueRecord> records)
    {
        int withFile = records.Count(record => record.Episode?.HasFile == true);

        if (records.Count == 0 || (withFile > 0 && withFile < records.Count))
        {
            return null;
        }

        return withFile == records.Count;
    }
}
