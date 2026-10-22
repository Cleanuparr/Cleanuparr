namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items using weighted random sampling that combines the custom format gap and search recency.
/// Half the weight is the item's gap relative to the largest gap in the pool, half is its search recency rank.
/// Items far below their custom format cutoff that haven't been searched recently get the highest combined weight.
/// </summary>
public sealed class CustomFormatGapWeightedSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTimeOffset? Added, DateTimeOffset? LastSearched, double Gap)> candidates, int count)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        count = Math.Min(count, candidates.Count);
        int n = candidates.Count;

        Dictionary<long, int> searchRanks = candidates
            .OrderBy(c => c.LastSearched ?? DateTimeOffset.MinValue)
            .Select((c, index) => (c.Id, Rank: n - index))
            .ToDictionary(x => x.Id, x => x.Rank);

        double maxGap = candidates.Max(c => c.Gap);

        // The recency half is at least 1/n, so every weight stays above zero
        List<long> selected = new(count);
        List<(long Id, double Weight)> pool = candidates
            .Select(c => (c.Id, Weight: 0.5 * (maxGap > 0 ? c.Gap / maxGap : 0) + 0.5 * searchRanks[c.Id] / n))
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
