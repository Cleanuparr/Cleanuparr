namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// Interface for selecting items to search based on a strategy
/// </summary>
public interface IItemSelector
{
    /// <summary>
    /// Selects up to <paramref name="count"/> item IDs from the candidates
    /// </summary>
    /// <param name="candidates">List of (id, dateAdded, lastSearched) tuples</param>
    /// <param name="count">Maximum number of items to select</param>
    /// <returns>Selected item IDs</returns>
    List<long> Select(List<(long Id, DateTime? Added, DateTime? LastSearched)> candidates, int count);
}
