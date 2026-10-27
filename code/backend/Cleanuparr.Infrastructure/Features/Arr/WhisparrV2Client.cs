using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class WhisparrV2Client : SonarrClient, IWhisparrV2Client
{
    public WhisparrV2Client(
        ILogger<WhisparrV2Client> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }

    /// <inheritdoc/>
    public override async Task<List<SearchableEpisode>> GetEpisodesAsync(ArrInstance arrInstance, long seriesId, CancellationToken cancellationToken = default) =>
        WithAirDates(await base.GetEpisodesAsync(arrInstance, seriesId, cancellationToken));

    /// <inheritdoc/>
    public override async Task<List<SearchableEpisode>> GetMissingEpisodesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default) =>
        WithAirDates(await base.GetMissingEpisodesAsync(arrInstance, monitored, cancellationToken));

    /// <inheritdoc/>
    public override async Task<List<SearchableEpisode>> GetCutoffUnmetEpisodesAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default) =>
        WithAirDates(await base.GetCutoffUnmetEpisodesAsync(arrInstance, monitored, cancellationToken));

    private static List<SearchableEpisode> WithAirDates(List<SearchableEpisode> episodes) =>
        episodes
            .Select(e => e with
            {
                AirDateUtc = e.AirDateUtc
                    ?? (e.ReleaseDate is { } releaseDate ? new DateTimeOffset(releaseDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null),
            })
            .ToList();
}
