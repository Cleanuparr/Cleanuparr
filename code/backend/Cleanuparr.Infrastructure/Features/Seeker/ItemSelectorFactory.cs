using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Seeker.Selectors;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// Factory that returns the appropriate item selector based on the configured strategy
/// </summary>
public static class ItemSelectorFactory
{
    public static IItemSelector Create(SelectionStrategy strategy)
    {
        return strategy switch
        {
            SelectionStrategy.OldestSearchFirst => new OldestSearchFirstSelector(),
            SelectionStrategy.OldestSearchWeighted => new OldestSearchWeightedSelector(),
            SelectionStrategy.NewestFirst => new NewestFirstSelector(),
            SelectionStrategy.NewestWeighted => new NewestWeightedSelector(),
            SelectionStrategy.BalancedWeighted => new BalancedWeightedSelector(),
            SelectionStrategy.Random => new RandomSelector(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown selection strategy")
        };
    }
}
