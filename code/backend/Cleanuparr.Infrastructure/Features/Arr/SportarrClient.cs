using Cleanuparr.Domain.Entities.Sonarr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class SportarrClient : SonarrClient, ISportarrClient
{
    public SportarrClient(
        ILogger<SportarrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    // scope to episode commands only, unlike the base class
    protected override SonarrCommand? FindExistingEpisodeCommand(List<SonarrCommand> commands) =>
        commands.FirstOrDefault(x => x.SearchType is SeriesSearchType.Episode);

    protected override bool HasExistingEpisodeCommand(List<SonarrCommand> commands) =>
        commands.Any(x => x.SearchType is SeriesSearchType.Episode);
}
