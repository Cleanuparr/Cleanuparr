using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Responses;
using Cleanuparr.Api.Features.DownloadCleaner.Controllers;
using Cleanuparr.Api.Tests.Features.DownloadCleaner.TestHelpers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.DownloadCleaner;

public class SeedingRulesControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly SeedingRulesController _controller;

    public SeedingRulesControllerTests()
    {
        _dataContext = SeedingRulesTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<SeedingRulesController>>();
        _controller = new SeedingRulesController(logger, _dataContext);
        ControllerTestContext.Attach(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private static SeedingRuleRequest CreateValidRequest(
        string name = "Test Rule",
        List<string>? categories = null,
        List<string>? trackerPatterns = null,
        List<string>? tagsAny = null,
        List<string>? tagsAll = null,
        int? priority = null,
        double maxRatio = 2.0,
        double minSeedTime = 0,
        double maxSeedTime = -1,
        int minSeeders = 0,
        bool deleteSourceFiles = true)
    {
        return new SeedingRuleRequest
        {
            Name = name,
            Categories = categories ?? ["movies"],
            TrackerPatterns = trackerPatterns ?? [],
            TagsAny = tagsAny ?? [],
            TagsAll = tagsAll ?? [],
            Priority = priority,
            PrivacyType = TorrentPrivacyType.Both,
            MaxRatio = maxRatio,
            MinSeedTime = minSeedTime,
            MaxSeedTime = maxSeedTime,
            MinSeeders = minSeeders,
            DeleteSourceFiles = deleteSourceFiles,
        };
    }

    private static List<SeedingRuleResponse> GetRulesFromOk(IActionResult result)
    {
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        IEnumerable<SeedingRuleResponse> rules = okResult.Value.ShouldBeAssignableTo<IEnumerable<SeedingRuleResponse>>()!;
        return rules.ToList();
    }

    private static T GetCreatedRule<T>(IActionResult result) where T : ISeedingRule
    {
        var createdResult = result.ShouldBeOfType<CreatedAtActionResult>();
        return createdResult.Value.ShouldBeOfType<T>();
    }

    // ──────────────────────────────────────────────────────────────────────
    // GetSeedingRules
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSeedingRules_EmptyRules_ReturnsEmptyList()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);

        var result = await _controller.GetSeedingRules(client.Id);

        GetRulesFromOk(result).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSeedingRules_ReturnsRulesOrderedByPriority()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "Rule C", priority: 3);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "Rule A", priority: 1);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "Rule B", priority: 2);

        var result = await _controller.GetSeedingRules(client.Id);

        List<SeedingRuleResponse> rules = GetRulesFromOk(result);
        rules.Count.ShouldBe(3);
        rules[0].Name.ShouldBe("Rule A");
        rules[1].Name.ShouldBe("Rule B");
        rules[2].Name.ShouldBe("Rule C");
    }

    [Fact]
    public async Task GetSeedingRules_NonExistentClient_ReturnsNotFound()
    {
        var result = await _controller.GetSeedingRules(Guid.NewGuid());
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetSeedingRules_QBitClient_ReturnsTagFields()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id,
            tagsAny: ["hd", "private"], tagsAll: ["required"]);

        var result = await _controller.GetSeedingRules(client.Id);

        SeedingRuleResponse rule = GetRulesFromOk(result).Single();
        rule.TagsAny.ShouldBe(new List<string> { "hd", "private" });
        rule.TagsAll.ShouldBe(new List<string> { "required" });
    }

    [Fact]
    public async Task GetSeedingRules_ReturnsMinSeeders()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, minSeeders: 5);

        var result = await _controller.GetSeedingRules(client.Id);

        SeedingRuleResponse rule = GetRulesFromOk(result).Single();
        rule.MinSeeders.ShouldBe(5);
    }

    [Fact]
    public async Task GetSeedingRules_DelugeClient_ReturnsEmptyTagFields()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext, DownloadClientTypeName.Deluge, "Test Deluge");
        SeedingRulesTestDataFactory.AddDelugeSeedingRule(_dataContext, client.Id);

        var result = await _controller.GetSeedingRules(client.Id);

        SeedingRuleResponse rule = GetRulesFromOk(result).Single();
        rule.TagsAny.ShouldBeEmpty();
        rule.TagsAll.ShouldBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────
    // CreateSeedingRule
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSeedingRule_ValidRequest_ReturnsCreated()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest(name: "Movies Rule", categories: ["movies", "films"]);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        var createdResult = result.ShouldBeOfType<CreatedAtActionResult>();
        createdResult.StatusCode.ShouldBe(201);

        QBitSeedingRule rule = GetCreatedRule<QBitSeedingRule>(result);
        rule.Name.ShouldBe("Movies Rule");
        rule.Categories.ShouldBe(new List<string> { "movies", "films" });
    }

    [Fact]
    public async Task CreateSeedingRule_AutoAssignsPriority_WhenNotProvided()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest();

        var result = await _controller.CreateSeedingRule(client.Id, request);

        GetCreatedRule<QBitSeedingRule>(result).Priority.ShouldBe(1);
    }

    [Fact]
    public async Task CreateSeedingRule_SetsMinSeeders()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest(minSeeders: 5);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        GetCreatedRule<QBitSeedingRule>(result).MinSeeders.ShouldBe(5);
    }

    [Fact]
    public async Task CreateSeedingRule_AutoAssignsSequentialPriority()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, priority: 1);

        var request = CreateValidRequest(name: "Second Rule", categories: ["tv"]);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        GetCreatedRule<QBitSeedingRule>(result).Priority.ShouldBe(2);
    }

    [Fact]
    public async Task CreateSeedingRule_DuplicatePriority_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, priority: 1);

        var request = CreateValidRequest(priority: 1);

        var result = await _controller.CreateSeedingRule(client.Id, request);
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateSeedingRule_NonExistentClient_ReturnsNotFound()
    {
        var request = CreateValidRequest();

        var result = await _controller.CreateSeedingRule(Guid.NewGuid(), request);
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CreateSeedingRule_EmptyCategories_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest(categories: []);

        await Should.ThrowAsync<ValidationException>(() => _controller.CreateSeedingRule(client.Id, request));
    }

    [Fact]
    public async Task CreateSeedingRule_SanitizesWhitespaceInLists()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest(
            trackerPatterns: ["", "  ", "valid.com", " trimmed.com "]);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        QBitSeedingRule rule = GetCreatedRule<QBitSeedingRule>(result);
        rule.TrackerPatterns.ShouldBe(new List<string> { "valid.com", "trimmed.com" });
    }

    [Fact]
    public async Task CreateSeedingRule_ForTransmission_CreatesTransmissionRule()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext,
            DownloadClientTypeName.Transmission, "Test Transmission");
        var request = CreateValidRequest(tagsAny: ["tag1"]);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        GetCreatedRule<TransmissionSeedingRule>(result).TagsAny.ShouldBe(new List<string> { "tag1" });
    }

    // ──────────────────────────────────────────────────────────────────────
    // UpdateSeedingRule
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSeedingRule_ValidRequest_ReturnsOk()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        var request = CreateValidRequest(name: "Updated Name", categories: ["tv", "anime"]);

        var result = await _controller.UpdateSeedingRule(rule.Id, request);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var updated = okResult.Value.ShouldBeOfType<QBitSeedingRule>();
        updated.Name.ShouldBe("Updated Name");
        updated.Categories.ShouldBe(new List<string> { "tv", "anime" });
    }

    [Fact]
    public async Task UpdateSeedingRule_DoesNotChangePriority()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, priority: 5);

        var request = CreateValidRequest(priority: 1);

        var result = await _controller.UpdateSeedingRule(rule.Id, request);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var updated = okResult.Value.ShouldBeOfType<QBitSeedingRule>();
        updated.Priority.ShouldBe(5);
    }

    [Fact]
    public async Task UpdateSeedingRule_UpdatesTagsForTagFilterableClient()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        var request = CreateValidRequest(tagsAny: ["new-tag"], tagsAll: ["must-have"]);

        var result = await _controller.UpdateSeedingRule(rule.Id, request);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var updated = okResult.Value.ShouldBeOfType<QBitSeedingRule>();
        updated.TagsAny.ShouldBe(new List<string> { "new-tag" });
        updated.TagsAll.ShouldBe(new List<string> { "must-have" });
    }

    [Fact]
    public async Task UpdateSeedingRule_UpdatesMinSeeders()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        var request = CreateValidRequest(minSeeders: 5);

        var result = await _controller.UpdateSeedingRule(rule.Id, request);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var updated = okResult.Value.ShouldBeOfType<QBitSeedingRule>();
        updated.MinSeeders.ShouldBe(5);
    }

    [Fact]
    public async Task UpdateSeedingRule_NonExistentRule_ReturnsNotFound()
    {
        var request = CreateValidRequest();

        var result = await _controller.UpdateSeedingRule(Guid.NewGuid(), request);
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateSeedingRule_ValidationFailure_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        // Both maxRatio and maxSeedTime negative → validation failure
        var request = CreateValidRequest(maxRatio: -1, maxSeedTime: -1);

        await Should.ThrowAsync<ValidationException>(() => _controller.UpdateSeedingRule(rule.Id, request));
    }

    // ──────────────────────────────────────────────────────────────────────
    // ReorderSeedingRules
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderSeedingRules_ValidRequest_ReturnsNoContent()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule1 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "A", priority: 1);
        var rule2 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "B", priority: 2);

        var request = new ReorderSeedingRulesRequest { OrderedIds = [rule2.Id, rule1.Id] };

        var result = await _controller.ReorderSeedingRules(client.Id, request);
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ReorderSeedingRules_AssignsSequentialPriorities()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule1 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "A", priority: 1);
        var rule2 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "B", priority: 2);
        var rule3 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "C", priority: 3);

        // Reverse order
        var request = new ReorderSeedingRulesRequest { OrderedIds = [rule3.Id, rule2.Id, rule1.Id] };
        await _controller.ReorderSeedingRules(client.Id, request);

        List<SeedingRuleResponse> rules = GetRulesFromOk(await _controller.GetSeedingRules(client.Id));

        rules[0].Name.ShouldBe("C");
        rules[0].Priority.ShouldBe(1);
        rules[1].Name.ShouldBe("B");
        rules[1].Priority.ShouldBe(2);
        rules[2].Name.ShouldBe("A");
        rules[2].Priority.ShouldBe(3);
    }

    [Fact]
    public async Task ReorderSeedingRules_NonExistentClient_ReturnsNotFound()
    {
        var request = new ReorderSeedingRulesRequest { OrderedIds = [Guid.NewGuid()] };

        var result = await _controller.ReorderSeedingRules(Guid.NewGuid(), request);
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ReorderSeedingRules_DuplicateIds_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule1 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "A", priority: 1);
        var rule2 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "B", priority: 2);

        var request = new ReorderSeedingRulesRequest { OrderedIds = [rule1.Id, rule1.Id] };

        var result = await _controller.ReorderSeedingRules(client.Id, request);
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ReorderSeedingRules_WrongCount_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule1 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "A", priority: 1);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "B", priority: 2);

        // Only send 1 of 2 IDs
        var request = new ReorderSeedingRulesRequest { OrderedIds = [rule1.Id] };

        var result = await _controller.ReorderSeedingRules(client.Id, request);
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ReorderSeedingRules_UnknownRuleId_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule1 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "A", priority: 1);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "B", priority: 2);

        var request = new ReorderSeedingRulesRequest { OrderedIds = [rule1.Id, Guid.NewGuid()] };

        var result = await _controller.ReorderSeedingRules(client.Id, request);
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DeleteSeedingRule
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSeedingRule_ExistingRule_ReturnsNoContent()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        var result = await _controller.DeleteSeedingRule(rule.Id);
        result.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteSeedingRule_VerifiesRuleRemoved()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        await _controller.DeleteSeedingRule(rule.Id);

        GetRulesFromOk(await _controller.GetSeedingRules(client.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteSeedingRule_NonExistentRule_ReturnsNotFound()
    {
        var result = await _controller.DeleteSeedingRule(Guid.NewGuid());
        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }
}
