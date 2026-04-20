using System.Text.Json;
using Cleanuparr.Api.Features.Seeker.Contracts.Responses;
using Cleanuparr.Api.Features.Seeker.Controllers;
using Cleanuparr.Api.Tests.Features.Seeker.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Seeker;

public class CustomFormatScoreControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly CustomFormatScoreController _controller;

    public CustomFormatScoreControllerTests()
    {
        _dataContext = SeekerTestDataFactory.CreateDataContext();
        _controller = new CustomFormatScoreController(_dataContext);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static JsonElement GetResponseBody(IActionResult result)
    {
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        return JsonDocument.Parse(json).RootElement;
    }

    #region GetCustomFormatScores Tests

    [Fact]
    public async Task GetCustomFormatScores_WithPageBelowMinimum_ClampsToOne()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Movie A", currentScore: 100, cutoffScore: 500);
        AddScoreEntry(radarr.Id, 2, "Movie B", currentScore: 200, cutoffScore: 500);

        var result = await _controller.GetCustomFormatScores(page: -5, pageSize: 50);
        var body = GetResponseBody(result);

        body.GetProperty("Page").GetInt32().ShouldBe(1);
        body.GetProperty("Items").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task GetCustomFormatScores_WithPageSizeAboveMaximum_ClampsToHundred()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Movie A", currentScore: 100, cutoffScore: 500);

        var result = await _controller.GetCustomFormatScores(page: 1, pageSize: 999);
        var body = GetResponseBody(result);

        body.GetProperty("PageSize").GetInt32().ShouldBe(500);
    }

    [Fact]
    public async Task GetCustomFormatScores_WithHideMetTrue_ExcludesItemsAtOrAboveCutoff()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Below Cutoff", currentScore: 100, cutoffScore: 500);
        AddScoreEntry(radarr.Id, 2, "At Cutoff", currentScore: 500, cutoffScore: 500);
        AddScoreEntry(radarr.Id, 3, "Above Cutoff", currentScore: 600, cutoffScore: 500);

        var result = await _controller.GetCustomFormatScores(cutoffFilter: CutoffFilter.Below);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
        body.GetProperty("Items")[0].GetProperty("Title").GetString().ShouldBe("Below Cutoff");
    }

    [Fact]
    public async Task GetCustomFormatScores_WithHideUnmonitoredTrue_ExcludesUnmonitoredItems()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Monitored Movie", currentScore: 100, cutoffScore: 500, isMonitored: true);
        AddScoreEntry(radarr.Id, 2, "Unmonitored Movie", currentScore: 200, cutoffScore: 500, isMonitored: false);
        AddScoreEntry(radarr.Id, 3, "Another Monitored", currentScore: 300, cutoffScore: 500, isMonitored: true);

        var result = await _controller.GetCustomFormatScores(monitoredFilter: MonitoredFilter.Monitored);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(2);
        var items = body.GetProperty("Items");
        items.GetArrayLength().ShouldBe(2);
        items[0].GetProperty("Title").GetString().ShouldBe("Another Monitored");
        items[1].GetProperty("Title").GetString().ShouldBe("Monitored Movie");
    }

    [Fact]
    public async Task GetCustomFormatScores_WithSearchFilter_ReturnsMatchingTitlesOnly()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "The Matrix", currentScore: 100, cutoffScore: 500);
        AddScoreEntry(radarr.Id, 2, "Inception", currentScore: 200, cutoffScore: 500);
        AddScoreEntry(radarr.Id, 3, "The Matrix Reloaded", currentScore: 300, cutoffScore: 500);

        var result = await _controller.GetCustomFormatScores(search: "matrix");
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task GetCustomFormatScores_WithSortByDate_OrdersByLastSyncedDescending()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Older", currentScore: 100, cutoffScore: 500,
            lastSynced: DateTime.UtcNow.AddHours(-2));
        AddScoreEntry(radarr.Id, 2, "Newer", currentScore: 200, cutoffScore: 500,
            lastSynced: DateTime.UtcNow.AddHours(-1));

        var result = await _controller.GetCustomFormatScores(sortBy: CfScoresSortBy.LastSyncedAt);
        var body = GetResponseBody(result);

        body.GetProperty("Items")[0].GetProperty("Title").GetString().ShouldBe("Newer");
        body.GetProperty("Items")[1].GetProperty("Title").GetString().ShouldBe("Older");
    }

    [Fact]
    public async Task GetCustomFormatScores_WithInstanceIdFilter_ReturnsOnlyThatInstance()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        var sonarr = SeekerTestDataFactory.AddSonarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Movie", currentScore: 100, cutoffScore: 500);
        AddScoreEntry(sonarr.Id, 2, "Series", currentScore: 200, cutoffScore: 500,
            itemType: InstanceType.Sonarr);

        var result = await _controller.GetCustomFormatScores(instanceId: radarr.Id);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
        body.GetProperty("Items")[0].GetProperty("Title").GetString().ShouldBe("Movie");
    }

    [Fact]
    public async Task GetCustomFormatScores_ReturnsCorrectTotalPagesCalculation()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        for (int i = 1; i <= 7; i++)
        {
            AddScoreEntry(radarr.Id, i, $"Movie {i}", currentScore: 100, cutoffScore: 500);
        }

        var result = await _controller.GetCustomFormatScores(page: 1, pageSize: 3);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(7);
        body.GetProperty("TotalPages").GetInt32().ShouldBe(3); // ceil(7/3) = 3
        body.GetProperty("Items").GetArrayLength().ShouldBe(3);
    }

    #endregion

    #region GetRecentUpgrades Tests

    [Fact]
    public async Task GetRecentUpgrades_WithNoHistory_ReturnsEmptyList()
    {
        var result = await _controller.GetRecentUpgrades();
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(0);
        body.GetProperty("Items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetRecentUpgrades_WithSingleEntryPerItem_ReturnsNoUpgrades()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 100, recordedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.GetRecentUpgrades();
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetRecentUpgrades_WithScoreIncrease_DetectsUpgrade()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 100, recordedAt: DateTime.UtcNow.AddDays(-2));
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 250, recordedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.GetRecentUpgrades();
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
        var upgrade = body.GetProperty("Items")[0];
        upgrade.GetProperty("PreviousScore").GetInt32().ShouldBe(100);
        upgrade.GetProperty("NewScore").GetInt32().ShouldBe(250);
    }

    [Fact]
    public async Task GetRecentUpgrades_WithScoreDecrease_DoesNotCountAsUpgrade()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 300, recordedAt: DateTime.UtcNow.AddDays(-2));
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 150, recordedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.GetRecentUpgrades();
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetRecentUpgrades_WithMultipleUpgradesInSameGroup_CountsEach()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        // 100 -> 200 -> 300 = two upgrades for the same item
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 100, recordedAt: DateTime.UtcNow.AddDays(-3));
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 200, recordedAt: DateTime.UtcNow.AddDays(-2));
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 300, recordedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.GetRecentUpgrades();
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task GetRecentUpgrades_WithDaysFilter_ExcludesOlderHistory()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        // Old upgrade (outside 7-day window)
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 100, recordedAt: DateTime.UtcNow.AddDays(-20));
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 250, recordedAt: DateTime.UtcNow.AddDays(-15));
        // Recent upgrade (inside 7-day window)
        AddHistoryEntry(radarr.Id, externalItemId: 2, score: 100, recordedAt: DateTime.UtcNow.AddDays(-3));
        AddHistoryEntry(radarr.Id, externalItemId: 2, score: 300, recordedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.GetRecentUpgrades(days: 7);
        var body = GetResponseBody(result);

        body.GetProperty("TotalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task GetRecentUpgrades_ReturnsSortedByMostRecentFirst()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        // Item 1: upgrade happened 5 days ago
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 100, recordedAt: DateTime.UtcNow.AddDays(-6));
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 200, recordedAt: DateTime.UtcNow.AddDays(-5));
        // Item 2: upgrade happened 1 day ago
        AddHistoryEntry(radarr.Id, externalItemId: 2, score: 100, recordedAt: DateTime.UtcNow.AddDays(-2));
        AddHistoryEntry(radarr.Id, externalItemId: 2, score: 300, recordedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.GetRecentUpgrades();
        var body = GetResponseBody(result);

        var items = body.GetProperty("Items");
        items.GetArrayLength().ShouldBe(2);
        // Most recent upgrade (item 2) should be first
        items[0].GetProperty("NewScore").GetInt32().ShouldBe(300);
        items[1].GetProperty("NewScore").GetInt32().ShouldBe(200);
    }

    #endregion

    #region GetStats Tests

    [Fact]
    public async Task GetStats_WithNoEntries_ReturnsZeroes()
    {
        var result = await _controller.GetStats();
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var stats = okResult.Value.ShouldBeOfType<CustomFormatScoreStatsResponse>();

        stats.TotalTracked.ShouldBe(0);
        stats.BelowCutoff.ShouldBe(0);
        stats.AtOrAboveCutoff.ShouldBe(0);
        stats.RecentUpgrades.ShouldBe(0);
    }

    [Fact]
    public async Task GetStats_CorrectlyCategorizesBelowAndAboveCutoff()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Below", currentScore: 100, cutoffScore: 500);
        AddScoreEntry(radarr.Id, 2, "At", currentScore: 500, cutoffScore: 500);
        AddScoreEntry(radarr.Id, 3, "Above", currentScore: 600, cutoffScore: 500);

        var result = await _controller.GetStats();
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var stats = okResult.Value.ShouldBeOfType<CustomFormatScoreStatsResponse>();

        stats.TotalTracked.ShouldBe(3);
        stats.BelowCutoff.ShouldBe(1);
        stats.AtOrAboveCutoff.ShouldBe(2);
    }

    [Fact]
    public async Task GetStats_CountsRecentUpgradesFromLast7Days()
    {
        var radarr = SeekerTestDataFactory.AddRadarrInstance(_dataContext);
        AddScoreEntry(radarr.Id, 1, "Movie", currentScore: 300, cutoffScore: 500);

        // Upgrade within 7 days
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 100, recordedAt: DateTime.UtcNow.AddDays(-3));
        AddHistoryEntry(radarr.Id, externalItemId: 1, score: 300, recordedAt: DateTime.UtcNow.AddDays(-1));

        // Upgrade outside 7 days (should not be counted)
        AddHistoryEntry(radarr.Id, externalItemId: 2, score: 50, recordedAt: DateTime.UtcNow.AddDays(-20));
        AddHistoryEntry(radarr.Id, externalItemId: 2, score: 200, recordedAt: DateTime.UtcNow.AddDays(-15));

        var result = await _controller.GetStats();
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var stats = okResult.Value.ShouldBeOfType<CustomFormatScoreStatsResponse>();

        stats.RecentUpgrades.ShouldBe(1);
    }

    #endregion

    #region Helpers

    private void AddScoreEntry(
        Guid arrInstanceId,
        long externalItemId,
        string title,
        int currentScore,
        int cutoffScore,
        InstanceType itemType = InstanceType.Radarr,
        DateTime? lastSynced = null,
        bool isMonitored = true)
    {
        _dataContext.CustomFormatScoreEntries.Add(new CustomFormatScoreEntry
        {
            ArrInstanceId = arrInstanceId,
            ExternalItemId = externalItemId,
            EpisodeId = 0,
            ItemType = itemType,
            Title = title,
            FileId = externalItemId * 10,
            CurrentScore = currentScore,
            CutoffScore = cutoffScore,
            QualityProfileName = "HD",
            IsMonitored = isMonitored,
            LastSyncedAt = lastSynced ?? DateTime.UtcNow
        });
        _dataContext.SaveChanges();
    }

    private void AddHistoryEntry(
        Guid arrInstanceId,
        long externalItemId,
        int score,
        DateTime recordedAt,
        long episodeId = 0,
        int cutoffScore = 500,
        InstanceType itemType = InstanceType.Radarr)
    {
        _dataContext.CustomFormatScoreHistory.Add(new CustomFormatScoreHistory
        {
            ArrInstanceId = arrInstanceId,
            ExternalItemId = externalItemId,
            EpisodeId = episodeId,
            ItemType = itemType,
            Title = $"Item {externalItemId}",
            Score = score,
            CutoffScore = cutoffScore,
            RecordedAt = recordedAt
        });
        _dataContext.SaveChanges();
    }

    #endregion
}
