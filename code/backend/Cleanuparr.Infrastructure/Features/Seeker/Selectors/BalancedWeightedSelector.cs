namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items using weighted random sampling that combines search recency and add date.
/// Each item is ranked on both dimensions, and the average rank determines its weight.
/// Items that are both recently added and haven't been searched get the highest combined weight.
/// </summary>
public sealed class BalancedWeightedSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTimeOffset? Added, DateTimeOffset? LastSearched, double Gap)> candidates, int count)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        count = Math.Min(count, candidates.Count);
        int n = candidates.Count;

        // Rank by search recency: never-searched / oldest-searched first (ascending)
        var searchRanks = candidates
            .OrderBy(c => c.LastSearched ?? DateTimeOffset.MinValue)
            .Select((c, index) => (c.Id, Rank: n - index))
            .ToDictionary(x => x.Id, x => x.Rank);

        // Rank by add date: newest first (descending)
        var ageRanks = candidates
            .OrderByDescending(c => c.Added ?? DateTimeOffset.MinValue)
            .Select((c, index) => (c.Id, Rank: n - index))
            .ToDictionary(x => x.Id, x => x.Rank);

        // Composite weight = average of both ranks (higher = more likely to be selected)
        var pool = candidates
            .Select(c => (c.Id, Weight: (searchRanks[c.Id] + ageRanks[c.Id]) / 2.0))
            .ToList();

        return OldestSearchWeightedSelector.WeightedRandom(pool, count);
    }
}
