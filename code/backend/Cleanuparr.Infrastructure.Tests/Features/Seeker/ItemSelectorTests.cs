using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Seeker;
using Cleanuparr.Infrastructure.Features.Seeker.Selectors;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Seeker;

public sealed class ItemSelectorTests
{
    private static readonly List<(long Id, DateTime? Added, DateTime? LastSearched)> SampleCandidates =
    [
        (1, new DateTime(2024, 1, 1), new DateTime(2024, 6, 1)),
        (2, new DateTime(2024, 3, 1), new DateTime(2024, 5, 1)),
        (3, new DateTime(2024, 5, 1), null),
        (4, new DateTime(2024, 2, 1), new DateTime(2024, 7, 1)),
        (5, null, new DateTime(2024, 4, 1)),
    ];

    #region ItemSelectorFactory Tests

    [Theory]
    [InlineData(SelectionStrategy.OldestSearchFirst, typeof(OldestSearchFirstSelector))]
    [InlineData(SelectionStrategy.OldestSearchWeighted, typeof(OldestSearchWeightedSelector))]
    [InlineData(SelectionStrategy.NewestFirst, typeof(NewestFirstSelector))]
    [InlineData(SelectionStrategy.NewestWeighted, typeof(NewestWeightedSelector))]
    [InlineData(SelectionStrategy.BalancedWeighted, typeof(BalancedWeightedSelector))]
    [InlineData(SelectionStrategy.Random, typeof(RandomSelector))]
    public void Factory_Create_ReturnsCorrectSelectorType(SelectionStrategy strategy, Type expectedType)
    {
        var selector = ItemSelectorFactory.Create(strategy);

        Assert.IsType(expectedType, selector);
    }

    [Fact]
    public void Factory_Create_InvalidStrategy_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ItemSelectorFactory.Create((SelectionStrategy)999));
    }

    #endregion

    #region NewestFirstSelector Tests

    [Fact]
    public void NewestFirst_Select_OrdersByAddedDescending()
    {
        var selector = new NewestFirstSelector();

        var result = selector.Select(SampleCandidates, 3);

        // Newest added: 3 (May), 2 (Mar), 4 (Feb)
        Assert.Equal([3, 2, 4], result);
    }

    [Fact]
    public void NewestFirst_Select_NullAddedDates_TreatedAsOldest()
    {
        var selector = new NewestFirstSelector();

        // Select all — item 5 (null Added) should be last
        var result = selector.Select(SampleCandidates, 5);

        Assert.Equal(5, result.Last());
    }

    [Fact]
    public void NewestFirst_Select_ReturnsRequestedCount()
    {
        var selector = new NewestFirstSelector();

        var result = selector.Select(SampleCandidates, 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void NewestFirst_Select_EmptyInput_ReturnsEmptyList()
    {
        var selector = new NewestFirstSelector();

        var result = selector.Select([], 5);

        Assert.Empty(result);
    }

    [Fact]
    public void NewestFirst_Select_CountExceedsCandidates_ReturnsAll()
    {
        var selector = new NewestFirstSelector();

        var result = selector.Select(SampleCandidates, 100);

        Assert.Equal(5, result.Count);
    }

    #endregion

    #region OldestSearchFirstSelector Tests

    [Fact]
    public void OldestSearchFirst_Select_OrdersByLastSearchedAscending()
    {
        var selector = new OldestSearchFirstSelector();

        var result = selector.Select(SampleCandidates, 3);

        // Never searched first (null → MinValue), then oldest: 3 (null), 5 (Apr), 2 (May)
        Assert.Equal([3, 5, 2], result);
    }

    [Fact]
    public void OldestSearchFirst_Select_NullLastSearched_PrioritizedFirst()
    {
        var selector = new OldestSearchFirstSelector();

        var result = selector.Select(SampleCandidates, 1);

        // Item 3 has LastSearched = null, should be first
        Assert.Equal(3, result[0]);
    }

    [Fact]
    public void OldestSearchFirst_Select_ReturnsRequestedCount()
    {
        var selector = new OldestSearchFirstSelector();

        var result = selector.Select(SampleCandidates, 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void OldestSearchFirst_Select_EmptyInput_ReturnsEmptyList()
    {
        var selector = new OldestSearchFirstSelector();

        var result = selector.Select([], 5);

        Assert.Empty(result);
    }

    #endregion

    #region RandomSelector Tests

    [Fact]
    public void Random_Select_ReturnsRequestedCount()
    {
        var selector = new RandomSelector();

        var result = selector.Select(SampleCandidates, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Random_Select_CountExceedsCandidates_ReturnsAll()
    {
        var selector = new RandomSelector();

        var result = selector.Select(SampleCandidates, 100);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Random_Select_EmptyInput_ReturnsEmptyList()
    {
        var selector = new RandomSelector();

        var result = selector.Select([], 5);

        Assert.Empty(result);
    }

    [Fact]
    public void Random_Select_NoDuplicateIds()
    {
        var selector = new RandomSelector();

        var result = selector.Select(SampleCandidates, 5);

        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void Random_Select_ResultsAreSubsetOfInput()
    {
        var selector = new RandomSelector();
        var inputIds = SampleCandidates.Select(c => c.Id).ToHashSet();

        var result = selector.Select(SampleCandidates, 3);

        Assert.All(result, id => Assert.Contains(id, inputIds));
    }

    #endregion

    #region NewestWeightedSelector Tests

    [Fact]
    public void NewestWeighted_Select_ReturnsRequestedCount()
    {
        var selector = new NewestWeightedSelector();

        var result = selector.Select(SampleCandidates, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void NewestWeighted_Select_EmptyInput_ReturnsEmptyList()
    {
        var selector = new NewestWeightedSelector();

        var result = selector.Select([], 5);

        Assert.Empty(result);
    }

    [Fact]
    public void NewestWeighted_Select_CountExceedsCandidates_ReturnsAll()
    {
        var selector = new NewestWeightedSelector();

        var result = selector.Select(SampleCandidates, 100);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void NewestWeighted_Select_NoDuplicateIds()
    {
        var selector = new NewestWeightedSelector();

        var result = selector.Select(SampleCandidates, 5);

        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void NewestWeighted_Select_SingleCandidate_ReturnsThatCandidate()
    {
        var selector = new NewestWeightedSelector();
        List<(long Id, DateTime? Added, DateTime? LastSearched)> single = [(42, DateTime.UtcNow, null)];

        var result = selector.Select(single, 1);

        Assert.Single(result);
        Assert.Equal(42, result[0]);
    }

    #endregion

    #region OldestSearchWeightedSelector Tests

    [Fact]
    public void OldestSearchWeighted_Select_ReturnsRequestedCount()
    {
        var selector = new OldestSearchWeightedSelector();

        var result = selector.Select(SampleCandidates, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void OldestSearchWeighted_Select_EmptyInput_ReturnsEmptyList()
    {
        var selector = new OldestSearchWeightedSelector();

        var result = selector.Select([], 5);

        Assert.Empty(result);
    }

    [Fact]
    public void OldestSearchWeighted_Select_CountExceedsCandidates_ReturnsAll()
    {
        var selector = new OldestSearchWeightedSelector();

        var result = selector.Select(SampleCandidates, 100);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void OldestSearchWeighted_Select_NoDuplicateIds()
    {
        var selector = new OldestSearchWeightedSelector();

        var result = selector.Select(SampleCandidates, 5);

        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void OldestSearchWeighted_Select_SingleCandidate_ReturnsThatCandidate()
    {
        var selector = new OldestSearchWeightedSelector();
        List<(long Id, DateTime? Added, DateTime? LastSearched)> single = [(42, DateTime.UtcNow, null)];

        var result = selector.Select(single, 1);

        Assert.Single(result);
        Assert.Equal(42, result[0]);
    }

    #endregion

    #region BalancedWeightedSelector Tests

    [Fact]
    public void BalancedWeighted_Select_ReturnsRequestedCount()
    {
        var selector = new BalancedWeightedSelector();

        var result = selector.Select(SampleCandidates, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void BalancedWeighted_Select_EmptyInput_ReturnsEmptyList()
    {
        var selector = new BalancedWeightedSelector();

        var result = selector.Select([], 5);

        Assert.Empty(result);
    }

    [Fact]
    public void BalancedWeighted_Select_CountExceedsCandidates_ReturnsAll()
    {
        var selector = new BalancedWeightedSelector();

        var result = selector.Select(SampleCandidates, 100);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void BalancedWeighted_Select_NoDuplicateIds()
    {
        var selector = new BalancedWeightedSelector();

        var result = selector.Select(SampleCandidates, 5);

        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void BalancedWeighted_Select_SingleCandidate_ReturnsThatCandidate()
    {
        var selector = new BalancedWeightedSelector();
        List<(long Id, DateTime? Added, DateTime? LastSearched)> single = [(42, DateTime.UtcNow, null)];

        var result = selector.Select(single, 1);

        Assert.Single(result);
        Assert.Equal(42, result[0]);
    }

    [Fact]
    public void BalancedWeighted_Select_ResultsAreSubsetOfInput()
    {
        var selector = new BalancedWeightedSelector();
        var inputIds = SampleCandidates.Select(c => c.Id).ToHashSet();

        var result = selector.Select(SampleCandidates, 3);

        Assert.All(result, id => Assert.Contains(id, inputIds));
    }

    #endregion

    #region WeightedRandomByRank Tests

    [Fact]
    public void WeightedRandomByRank_ReturnsRequestedCount()
    {
        var ranked = SampleCandidates.OrderBy(c => c.LastSearched ?? DateTime.MinValue).ToList();

        var result = OldestSearchWeightedSelector.WeightedRandomByRank(ranked, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void WeightedRandomByRank_CountExceedsCandidates_ReturnsAll()
    {
        var ranked = SampleCandidates.OrderBy(c => c.LastSearched ?? DateTime.MinValue).ToList();

        var result = OldestSearchWeightedSelector.WeightedRandomByRank(ranked, 100);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void WeightedRandomByRank_NoDuplicateIds()
    {
        var ranked = SampleCandidates.OrderBy(c => c.LastSearched ?? DateTime.MinValue).ToList();

        var result = OldestSearchWeightedSelector.WeightedRandomByRank(ranked, 5);

        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void WeightedRandomByRank_EmptyInput_ReturnsEmptyList()
    {
        var result = OldestSearchWeightedSelector.WeightedRandomByRank([], 5);

        Assert.Empty(result);
    }

    #endregion
}
