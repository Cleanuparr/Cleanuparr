namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items using weighted random sampling that combines search recency and add date.
/// Each item is ranked on both dimensions, and the average rank determines its weight.
/// Items that are both recently added and haven't been searched get the highest combined weight.
/// </summary>
public sealed class BalancedWeightedSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTime? Added, DateTime? LastSearched)> candidates, int count)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        count = Math.Min(count, candidates.Count);
        int n = candidates.Count;

        // Rank by search recency: never-searched / oldest-searched first (ascending)
        var searchRanks = candidates
            .OrderBy(c => c.LastSearched ?? DateTime.MinValue)
            .Select((c, index) => (c.Id, Rank: n - index))
            .ToDictionary(x => x.Id, x => x.Rank);

        // Rank by add date: newest first (descending)
        var ageRanks = candidates
            .OrderByDescending(c => c.Added ?? DateTime.MinValue)
            .Select((c, index) => (c.Id, Rank: n - index))
            .ToDictionary(x => x.Id, x => x.Rank);

        // Composite weight = average of both ranks (higher = more likely to be selected)
        var selected = new List<long>(count);
        var pool = candidates
            .Select(c => (c.Id, Weight: (searchRanks[c.Id] + ageRanks[c.Id]) / 2.0))
            .ToList();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            double totalWeight = 0;
            for (int j = 0; j < pool.Count; j++)
            {
                totalWeight += pool[j].Weight;
            }

            double target = Random.Shared.NextDouble() * totalWeight;
            double cumulative = 0;
            int selectedIndex = pool.Count - 1;

            for (int j = 0; j < pool.Count; j++)
            {
                cumulative += pool[j].Weight;
                if (cumulative >= target)
                {
                    selectedIndex = j;
                    break;
                }
            }

            selected.Add(pool[selectedIndex].Id);
            pool.RemoveAt(selectedIndex);
        }

        return selected;
    }
}
