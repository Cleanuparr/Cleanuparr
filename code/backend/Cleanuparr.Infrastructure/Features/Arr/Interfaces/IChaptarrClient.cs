using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

/// <summary>
/// Chaptarr is a Readarr fork and shares its API.
/// </summary>
public interface IChaptarrClient : IReadarrClient
{
    /// <summary>
    /// Streams authors from a Chaptarr instance one item at a time
    /// </summary>
    IAsyncEnumerable<SearchableAuthor> StreamAllAuthorsAsync(ArrInstance arrInstance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the ids of books Chaptarr grabbed since the given time.
    /// </summary>
    Task<HashSet<long>> GetRecentlyGrabbedBookIdsAsync(ArrInstance arrInstance, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every book Chaptarr lists as missing, filtered by monitored state.
    /// </summary>
    Task<List<SearchableBook>> GetMissingBooksAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches every book Chaptarr lists as a cutoff-unmet upgrade, filtered by monitored state.
    /// </summary>
    Task<List<SearchableBook>> GetCutoffUnmetBooksAsync(ArrInstance arrInstance, bool monitored, CancellationToken cancellationToken = default);
}
