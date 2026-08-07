using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.Queue;
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
/// <see cref="SonarrClient"/> instead of duplicating it - the two only diverge on the episode
/// search command bug fix below.
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
    /// Same as <see cref="SonarrClient"/>'s implementation, except the existing command lookup
    /// is scoped to <see cref="SeriesSearchType.Episode"/> instead of taking whatever command
    /// happens to be first. Without the scoping, a season or series item processed before an
    /// episode item causes the episode search to get merged into the wrong command and dropped.
    /// </summary>
    protected override List<SonarrCommand> GetSearchCommands(HashSet<SeriesSearchItem> items)
    {
        const string episodeSearch = "EpisodeSearch";
        const string seasonSearch = "SeasonSearch";
        const string seriesSearch = "SeriesSearch";

        List<SonarrCommand> commands = new();

        foreach (SeriesSearchItem item in items)
        {
            SonarrCommand command = item.SearchType is SeriesSearchType.Episode
                ? commands.FirstOrDefault(x => x.SearchType is SeriesSearchType.Episode) ?? new() { Name = episodeSearch, EpisodeIds = new() }
                : new();

            switch (item.SearchType)
            {
                case SeriesSearchType.Episode when command.EpisodeIds is null:
                    command.EpisodeIds = [item.Id];
                    break;

                case SeriesSearchType.Episode when command.EpisodeIds is not null:
                    command.EpisodeIds.Add(item.Id);
                    break;

                case SeriesSearchType.Season:
                    command.Name = seasonSearch;
                    command.SeasonNumber = item.Id;
                    command.SeriesId = ((SeriesSearchItem)item).SeriesId;
                    break;

                case SeriesSearchType.Series:
                    command.Name = seriesSearch;
                    command.SeriesId = item.Id;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(item.SearchType), item.SearchType, null);
            }

            if (item.SearchType is SeriesSearchType.Episode && commands.Any(x => x.SearchType is SeriesSearchType.Episode))
            {
                // only one command will be generated for episodes search
                continue;
            }

            command.SearchType = item.SearchType;
            commands.Add(command);
        }

        return commands;
    }
}
