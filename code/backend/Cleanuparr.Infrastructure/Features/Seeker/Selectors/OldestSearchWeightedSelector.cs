namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items using rank-based weighted random sampling on search recency.
/// Items that haven't been searched recently are ranked higher and more likely to be selected.
/// Never-searched items receive the highest rank.
/// </summary>
public sealed class OldestSearchWeightedSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTime? Added, DateTime? LastSearched)> candidates, int count)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        count = Math.Min(count, candidates.Count);

        // Sort by LastSearched ascending, then oldest searches, so rank 0 = highest priority
        var ranked = candidates
            .OrderBy(c => c.LastSearched ?? DateTime.MinValue)
            .ToList();

        return WeightedRandomByRank(ranked, count);
    }

    /// <summary>
    /// Performs weighted random selection without replacement using rank-based weights.
    /// The item at rank 0 gets the highest weight (N), rank 1 gets (N-1), etc.
    /// </summary>
    internal static List<long> WeightedRandomByRank(
        List<(long Id, DateTime? Added, DateTime? LastSearched)> ranked,
        int count)
    {
        int n = ranked.Count;
        var selected = new List<long>(count);

        // Build initial weights from rank: highest rank (index 0) gets weight N, lowest gets 1
        var pool = new List<(long Id, double Weight)>(n);
        for (int i = 0; i < n; i++)
        {
            pool.Add((ranked[i].Id, n - i));
        }

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
