using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.State;

namespace Cleanuparr.Infrastructure.Features.Jobs;

internal sealed record SeriesFetch(SearchableSeries Series, List<SearchableEpisode> Episodes, List<ArrEpisodeFile> EpisodeFiles, bool Failed);
