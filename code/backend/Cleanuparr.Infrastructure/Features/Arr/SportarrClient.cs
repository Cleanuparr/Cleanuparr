using Cleanuparr.Domain.Entities.Sonarr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

/// <summary>
/// Arr client for Sportarr instances. Sportarr speaks the exact same wire protocol as Sonarr
/// (same v3 API shape, same command/queue contracts), so this client inherits directly from
/// <see cref="SonarrClient"/> instead of duplicating it. The only behavioral difference is how
/// an episode search command gets merged into an existing command in the same batch — see the
/// two overrides below.
/// </summary>
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

    /// <summary>
    /// Scopes the lookup to an existing episode command specifically, instead of
    /// <see cref="SonarrClient"/>'s base behavior of reusing whichever command happens to be
    /// first. Without the scoping, a season or series item processed before an episode item
    /// causes the episode search to get merged into the wrong command and dropped.
    /// </summary>
    protected override SonarrCommand? FindExistingEpisodeCommand(List<SonarrCommand> commands) =>
        commands.FirstOrDefault(x => x.SearchType is SeriesSearchType.Episode);

    /// <inheritdoc cref="FindExistingEpisodeCommand"/>
    protected override bool HasExistingEpisodeCommand(List<SonarrCommand> commands) =>
        commands.Any(x => x.SearchType is SeriesSearchType.Episode);
}
