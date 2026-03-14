namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items randomly using Fisher-Yates shuffle.
/// Simplest strategy with no bias.
/// </summary>
public sealed class RandomSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTime? Added, DateTime? LastSearched)> candidates, int count)
    {
        count = Math.Min(count, candidates.Count);
        var shuffled = new List<(long Id, DateTime? Added, DateTime? LastSearched)>(candidates);

        // Fisher-Yates shuffle
        for (int i = 0; i < count; i++)
        {
            int j = Random.Shared.Next(i, shuffled.Count);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled
            .Take(count)
            .Select(c => c.Id)
            .ToList();
    }
}
