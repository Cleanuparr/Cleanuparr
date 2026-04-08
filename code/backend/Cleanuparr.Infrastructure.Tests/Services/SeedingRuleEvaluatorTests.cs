using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class SeedingRuleEvaluatorTests
{
    private readonly SeedingRuleEvaluator _sut = new();

    private static ITorrentItemWrapper CreateTorrent(
        string? category = "movies",
        bool isPrivate = false,
        IReadOnlyList<string>? trackerDomains = null,
        IReadOnlyList<string>? tags = null)
    {
        var torrent = Substitute.For<ITorrentItemWrapper>();
        torrent.Category.Returns(category);
        torrent.IsPrivate.Returns(isPrivate);
        torrent.TrackerDomains.Returns(trackerDomains ?? Array.Empty<string>());
        torrent.Tags.Returns(tags ?? Array.Empty<string>());
        return torrent;
    }

    private static QBitSeedingRule CreateQBitRule(
        int priority = 1,
        List<string>? categories = null,
        List<string>? trackerPatterns = null,
        List<string>? tagsAny = null,
        List<string>? tagsAll = null,
        TorrentPrivacyType privacyType = TorrentPrivacyType.Both)
    {
        return new QBitSeedingRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Rule",
            Priority = priority,
            Categories = categories ?? ["movies"],
            TrackerPatterns = trackerPatterns ?? [],
            TagsAny = tagsAny ?? [],
            TagsAll = tagsAll ?? [],
            PrivacyType = privacyType,
        };
    }

    private static DelugeSeedingRule CreateDelugeRule(
        int priority = 1,
        List<string>? categories = null,
        List<string>? trackerPatterns = null,
        TorrentPrivacyType privacyType = TorrentPrivacyType.Both)
    {
        return new DelugeSeedingRule
        {
            Id = Guid.NewGuid(),
            Name = "Test Rule",
            Priority = priority,
            Categories = categories ?? ["movies"],
            TrackerPatterns = trackerPatterns ?? [],
            PrivacyType = privacyType,
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // Empty / null rules
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_EmptyRules_ReturnsNull()
    {
        var torrent = CreateTorrent();
        _sut.GetMatchingRule(torrent, []).ShouldBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Category filtering
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_CategoryMatches_ReturnsRule()
    {
        var torrent = CreateTorrent(category: "movies");
        var rule = CreateDelugeRule(categories: ["movies"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_CategoryMismatch_ReturnsNull()
    {
        var torrent = CreateTorrent(category: "tv");
        var rule = CreateDelugeRule(categories: ["movies"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBeNull();
    }

    [Fact]
    public void GetMatchingRule_CategoryMatchIsCaseInsensitive()
    {
        var torrent = CreateTorrent(category: "MOVIES");
        var rule = CreateDelugeRule(categories: ["movies"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TorrentMatchesAnyCategory()
    {
        var torrent = CreateTorrent(category: "tv");
        var rule = CreateDelugeRule(categories: ["movies", "tv", "music"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Tracker pattern filtering
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_EmptyTrackerPatterns_MatchesAnyTracker()
    {
        var torrent = CreateTorrent(trackerDomains: ["tracker.example.com"]);
        var rule = CreateDelugeRule(trackerPatterns: []);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TrackerPatternSuffixMatches()
    {
        var torrent = CreateTorrent(trackerDomains: ["tracker.example.com"]);
        var rule = CreateDelugeRule(trackerPatterns: ["example.com"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TrackerPatternNoMatch_ReturnsNull()
    {
        var torrent = CreateTorrent(trackerDomains: ["tracker.other.org"]);
        var rule = CreateDelugeRule(trackerPatterns: ["example.com"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBeNull();
    }

    [Fact]
    public void GetMatchingRule_TrackerPatternMatchIsCaseInsensitive()
    {
        var torrent = CreateTorrent(trackerDomains: ["TRACKER.EXAMPLE.COM"]);
        var rule = CreateDelugeRule(trackerPatterns: ["example.com"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TorrentMatchesAnyTrackerPattern()
    {
        var torrent = CreateTorrent(trackerDomains: ["tracker.private.org"]);
        var rule = CreateDelugeRule(trackerPatterns: ["example.com", "private.org"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Privacy type filtering
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TorrentPrivacyType.Public, false, true)]
    [InlineData(TorrentPrivacyType.Public, true, false)]
    [InlineData(TorrentPrivacyType.Private, false, false)]
    [InlineData(TorrentPrivacyType.Private, true, true)]
    [InlineData(TorrentPrivacyType.Both, false, true)]
    [InlineData(TorrentPrivacyType.Both, true, true)]
    public void GetMatchingRule_PrivacyType(TorrentPrivacyType rulePrivacy, bool torrentIsPrivate, bool shouldMatch)
    {
        var torrent = CreateTorrent(isPrivate: torrentIsPrivate);
        var rule = CreateDelugeRule(privacyType: rulePrivacy);

        var result = _sut.GetMatchingRule(torrent, [rule]);
        if (shouldMatch)
            result.ShouldBe(rule);
        else
            result.ShouldBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────
    // TagsAny filtering (ITagFilterable — QBit/Transmission only)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_TagsAny_EmptyList_MatchesAnyTorrent()
    {
        var torrent = CreateTorrent(tags: ["some-tag"]);
        var rule = CreateQBitRule(tagsAny: []);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TagsAny_TorrentHasMatchingTag_Matches()
    {
        var torrent = CreateTorrent(tags: ["hd", "private"]);
        var rule = CreateQBitRule(tagsAny: ["private"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TagsAny_TorrentLacksAllTags_ReturnsNull()
    {
        var torrent = CreateTorrent(tags: ["hd"]);
        var rule = CreateQBitRule(tagsAny: ["private", "special"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBeNull();
    }

    [Fact]
    public void GetMatchingRule_TagsAny_MatchIsCaseInsensitive()
    {
        var torrent = CreateTorrent(tags: ["PRIVATE"]);
        var rule = CreateQBitRule(tagsAny: ["private"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    // ──────────────────────────────────────────────────────────────────────
    // TagsAll filtering (ITagFilterable — QBit/Transmission only)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_TagsAll_EmptyList_MatchesAnyTorrent()
    {
        var torrent = CreateTorrent(tags: []);
        var rule = CreateQBitRule(tagsAll: []);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TagsAll_TorrentHasAllTags_Matches()
    {
        var torrent = CreateTorrent(tags: ["hd", "private", "bonus"]);
        var rule = CreateQBitRule(tagsAll: ["hd", "private"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_TagsAll_TorrentMissingOneTag_ReturnsNull()
    {
        var torrent = CreateTorrent(tags: ["hd"]);
        var rule = CreateQBitRule(tagsAll: ["hd", "private"]);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────
    // ITagFilterable not applied for non-tag clients (e.g. Deluge)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_DelugeRule_TagsIgnored_Matches()
    {
        // Deluge doesn't implement ITagFilterable, so tag properties on the rule are not checked
        var torrent = CreateTorrent(tags: []);
        var rule = CreateDelugeRule(); // no tag support

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Priority ordering
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_ReturnsLowestPriorityRule_WhenMultipleMatch()
    {
        var torrent = CreateTorrent(category: "movies");
        var highPriority = CreateDelugeRule(priority: 1, categories: ["movies"]);
        var lowPriority = CreateDelugeRule(priority: 5, categories: ["movies"]);

        // Pass in reverse order to ensure sorting is applied
        var result = _sut.GetMatchingRule(torrent, [lowPriority, highPriority]);

        result.ShouldBe(highPriority);
    }

    [Fact]
    public void GetMatchingRule_OnlyMatchingRuleReturned_OtherFiltered()
    {
        var torrent = CreateTorrent(
            category: "movies",
            trackerDomains: ["tracker.private.org"]);

        var publicRule = CreateDelugeRule(
            priority: 1,
            categories: ["movies"],
            trackerPatterns: ["public.com"]); // does not match tracker

        var privateRule = CreateDelugeRule(
            priority: 2,
            categories: ["movies"],
            trackerPatterns: ["private.org"]); // matches tracker

        var result = _sut.GetMatchingRule(torrent, [publicRule, privateRule]);

        result.ShouldBe(privateRule);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Combined criteria (AND logic)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatchingRule_AllCriteriaMustMatch()
    {
        var torrent = CreateTorrent(
            category: "movies",
            isPrivate: true,
            trackerDomains: ["tracker.example.com"],
            tags: ["hd"]);

        var rule = CreateQBitRule(
            categories: ["movies"],
            trackerPatterns: ["example.com"],
            tagsAny: ["hd"],
            privacyType: TorrentPrivacyType.Private);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBe(rule);
    }

    [Fact]
    public void GetMatchingRule_OneCriterionFails_ReturnsNull()
    {
        var torrent = CreateTorrent(
            category: "movies",
            isPrivate: true,
            trackerDomains: ["tracker.example.com"],
            tags: ["hd"]);

        // tracker pattern doesn't match
        var rule = CreateQBitRule(
            categories: ["movies"],
            trackerPatterns: ["other.com"],
            tagsAny: ["hd"],
            privacyType: TorrentPrivacyType.Private);

        _sut.GetMatchingRule(torrent, [rule]).ShouldBeNull();
    }
}
