namespace Cleanuparr.Infrastructure.Features.Seeker.Selectors;

/// <summary>
/// Selects items by date added, newest first.
/// Good for quickly finding newly added content.
/// </summary>
public sealed class NewestFirstSelector : IItemSelector
{
    public List<long> Select(List<(long Id, DateTime? Added, DateTime? LastSearched)> candidates, int count)
    {
        return candidates
            .OrderByDescending(c => c.Added ?? DateTime.MinValue)
            .Take(count)
            .Select(c => c.Id)
            .ToList();
    }
}
