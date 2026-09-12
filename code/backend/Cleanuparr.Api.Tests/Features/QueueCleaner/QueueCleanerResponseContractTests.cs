using Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.QueueCleaner.Contracts.Responses;
using Cleanuparr.Api.Features.QueueCleaner.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.QueueCleaner;

/// <summary>
/// Records today's wire shape, leaks included.
/// </summary>
public class QueueCleanerResponseContractTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly QueueCleanerConfigController _configController;
    private readonly QueueRulesController _rulesController;

    public QueueCleanerResponseContractTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();

        _configController = new QueueCleanerConfigController(
            Substitute.For<ILogger<QueueCleanerConfigController>>(),
            _dataContext,
            Substitute.For<IJobManagementService>());
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_configController);

        IRuleIntervalValidator validator = Substitute.For<IRuleIntervalValidator>();
        validator.ValidateStallRuleIntervals(Arg.Any<StallRule>(), Arg.Any<List<StallRule>>())
            .Returns(ValidationResult.Success());
        validator.ValidateSlowRuleIntervals(Arg.Any<SlowRule>(), Arg.Any<List<SlowRule>>())
            .Returns(ValidationResult.Success());

        _rulesController = new QueueRulesController(
            Substitute.For<ILogger<QueueRulesController>>(),
            _dataContext,
            validator);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_rulesController);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetQueueCleanerConfig_ReturnsTheDocumentedKeys()
    {
        IActionResult result = await _configController.GetQueueCleanerConfig();

        ResponseContract.Keys(result).ShouldBe(
        [
            "cronExpression",
            "downloadingMetadataMaxStrikes",
            "enabled",
            "failedImport",
            "ignoredDownloads",
            "processNoContentId",
            "useAdvancedScheduling",
        ]);
    }

    [Fact]
    public async Task GetStallRules_ReturnsTheDocumentedKeys()
    {
        await _rulesController.CreateStallRule(NewStallDto("contract"));

        IActionResult result = await _rulesController.GetStallRules();

        ResponseContract.FirstItemKeys(result).ShouldBe(StallRuleKeys);
    }

    [Fact]
    public async Task CreateStallRule_ReturnsTheDocumentedKeys()
    {
        IActionResult result = await _rulesController.CreateStallRule(NewStallDto("contract"));

        ResponseContract.Keys(result).ShouldBe(StallRuleKeys);
    }

    [Fact]
    public async Task UpdateStallRule_ReturnsTheDocumentedKeys()
    {
        IActionResult created = await _rulesController.CreateStallRule(NewStallDto("contract"));
        Guid id = created.ShouldBeOfType<CreatedAtActionResult>().Value.ShouldBeOfType<StallRuleResponse>().Id;

        IActionResult result = await _rulesController.UpdateStallRule(id, NewStallDto("renamed"));

        ResponseContract.Keys(result).ShouldBe(StallRuleKeys);
    }

    [Fact]
    public async Task GetSlowRules_ReturnsTheDocumentedKeys()
    {
        await _rulesController.CreateSlowRule(NewSlowDto("contract"));

        IActionResult result = await _rulesController.GetSlowRules();

        ResponseContract.FirstItemKeys(result).ShouldBe(SlowRuleKeys);
    }

    [Fact]
    public async Task CreateSlowRule_ReturnsTheDocumentedKeys()
    {
        IActionResult result = await _rulesController.CreateSlowRule(NewSlowDto("contract"));

        ResponseContract.Keys(result).ShouldBe(SlowRuleKeys);
    }

    [Fact]
    public async Task UpdateSlowRule_ReturnsTheDocumentedKeys()
    {
        IActionResult created = await _rulesController.CreateSlowRule(NewSlowDto("contract"));
        Guid id = created.ShouldBeOfType<CreatedAtActionResult>().Value.ShouldBeOfType<SlowRuleResponse>().Id;

        IActionResult result = await _rulesController.UpdateSlowRule(id, NewSlowDto("renamed"));

        ResponseContract.Keys(result).ShouldBe(SlowRuleKeys);
    }

    private static readonly string[] StallRuleKeys =
    [
        "changeCategory",
        "deletePrivateTorrentsFromClient",
        "enabled",
        "id",
        "maxCompletionPercentage",
        "maxStrikes",
        "minCompletionPercentage",
        "minimumProgress",
        "name",
        "privacyType",
        "resetStrikesOnProgress",
    ];

    private static readonly string[] SlowRuleKeys =
    [
        "changeCategory",
        "deletePrivateTorrentsFromClient",
        "enabled",
        "id",
        "ignoreAboveSize",
        "ignoreWhileAltSpeedActive",
        "maxCompletionPercentage",
        "maxStrikes",
        "maxTimeHours",
        "minCompletionPercentage",
        "minSpeed",
        "name",
        "privacyType",
        "resetStrikesOnProgress",
    ];

    private static StallRuleDto NewStallDto(string name) => new()
    {
        Name = name,
        Enabled = true,
        MaxStrikes = 3,
        PrivacyType = TorrentPrivacyType.Public,
        MinCompletionPercentage = 0,
        MaxCompletionPercentage = 100,
        ResetStrikesOnProgress = true,
    };

    private static SlowRuleDto NewSlowDto(string name) => new()
    {
        Name = name,
        Enabled = true,
        MaxStrikes = 3,
        PrivacyType = TorrentPrivacyType.Public,
        MinCompletionPercentage = 0,
        MaxCompletionPercentage = 100,
        ResetStrikesOnProgress = true,
        MinSpeed = "1MB",
    };
}
