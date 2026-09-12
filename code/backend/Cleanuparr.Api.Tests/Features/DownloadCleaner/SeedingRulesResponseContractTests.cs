using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Controllers;
using Cleanuparr.Api.Tests.Features.DownloadCleaner.TestHelpers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.DownloadCleaner;

/// <summary>
/// Create and update hand back the raw entity, a different shape than the list DTO.
/// </summary>
public class SeedingRulesResponseContractTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly SeedingRulesController _controller;

    public SeedingRulesResponseContractTests()
    {
        _dataContext = SeedingRulesTestDataFactory.CreateDataContext();

        _controller = new SeedingRulesController(
            Substitute.For<ILogger<SeedingRulesController>>(),
            _dataContext);
        ControllerTestContext.Attach(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetSeedingRules_ReturnsTheDocumentedListDtoKeys()
    {
        DownloadClientConfig client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        IActionResult result = await _controller.GetSeedingRules(client.Id);

        ResponseContract.FirstItemKeys(result).ShouldBe(ListDtoKeys);
    }

    [Fact]
    public async Task CreateSeedingRule_ReturnsEntityKeysNotTheListDtoKeys()
    {
        DownloadClientConfig client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);

        IActionResult result = await _controller.CreateSeedingRule(client.Id, NewRuleRequest("contract"));

        ResponseContract.Keys(result).ShouldBe(QBitEntityKeys);
        ResponseContract.Keys(result).ShouldNotBe(ListDtoKeys);
    }

    [Fact]
    public async Task UpdateSeedingRule_ReturnsEntityKeysNotTheListDtoKeys()
    {
        DownloadClientConfig client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        QBitSeedingRule rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        IActionResult result = await _controller.UpdateSeedingRule(rule.Id, NewRuleRequest("renamed"));

        ResponseContract.Keys(result).ShouldBe(QBitEntityKeys);
        ResponseContract.Keys(result).ShouldNotBe(ListDtoKeys);
    }



    private static readonly string[] ListDtoKeys =
    [
        "action",
        "categories",
        "deleteSourceFiles",
        "id",
        "maxInactiveDays",
        "maxRatio",
        "maxSeedTime",
        "minSeedTime",
        "minSeeders",
        "name",
        "priority",
        "privacyType",
        "tagsAll",
        "tagsAny",
        "trackerPatterns",
    ];

    private static readonly string[] QBitEntityKeys =
    [
        "action",
        "categories",
        "deleteSourceFiles",
        "downloadClientConfig",
        "downloadClientConfigId",
        "id",
        "maxInactiveDays",
        "maxRatio",
        "maxSeedTime",
        "minSeedTime",
        "minSeeders",
        "name",
        "priority",
        "privacyType",
        "tagsAll",
        "tagsAny",
        "trackerPatterns",
    ];


    private static SeedingRuleRequest NewRuleRequest(string name) => new()
    {
        Name = name,
        Categories = ["movies"],
        TrackerPatterns = [],
        TagsAny = [],
        TagsAll = [],
        PrivacyType = TorrentPrivacyType.Both,
        MaxRatio = 2.0,
        MinSeedTime = 0,
        MaxSeedTime = -1,
        MinSeeders = 0,
        MaxInactiveDays = -1,
        DeleteSourceFiles = true,
        Action = SeedingRuleAction.Delete,
    };
}
