namespace Cleanuparr.Domain.Enums;

public enum SelectionStrategy
{
    /// <summary>
    /// Weighted random selection combining search recency and add date.
    /// Items that are both recently added and haven't been searched
    /// get the highest priority. Best all-around strategy for mixed libraries.
    /// </summary>
    BalancedWeighted,
    
    /// <summary>
    /// Deterministic selection of items with the oldest (or no) search history first.
    /// Provides systematic, sequential coverage of your entire library.
    /// </summary>
    OldestSearchFirst,

    /// <summary>
    /// Weighted random selection based on search recency.
    /// Items that haven't been searched recently are ranked higher and more likely to be selected,
    /// while recently-searched items still have a chance proportional to their rank.
    /// </summary>
    OldestSearchWeighted,

    /// <summary>
    /// Deterministic selection of the most recently added items first.
    /// Always picks the newest content in your library.
    /// </summary>
    NewestFirst,

    /// <summary>
    /// Weighted random selection based on when items were added.
    /// Recently added items are ranked higher and more likely to be selected,
    /// while older items still have a chance proportional to their rank.
    /// </summary>
    NewestWeighted,

    /// <summary>
    /// Pure random selection with no weighting or bias.
    /// Every eligible item has an equal chance of being selected.
    /// </summary>
    Random,
}
