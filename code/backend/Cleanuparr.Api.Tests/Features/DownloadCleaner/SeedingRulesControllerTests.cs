using System.Text.Json;
using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadCleaner.Controllers;
using Cleanuparr.Api.Tests.Features.DownloadCleaner.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
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
            DeleteSourceFiles = deleteSourceFiles,
        };
    }

    private static JsonElement GetJsonBody(IActionResult result)
    {
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        return JsonDocument.Parse(json).RootElement;
    }

    private static JsonElement GetCreatedJsonBody(IActionResult result)
    {
        var createdResult = result.ShouldBeOfType<CreatedAtActionResult>();
        var json = JsonSerializer.Serialize(createdResult.Value);
        return JsonDocument.Parse(json).RootElement;
    }

    // ──────────────────────────────────────────────────────────────────────
    // GetSeedingRules
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSeedingRules_EmptyRules_ReturnsEmptyList()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);

        var result = await _controller.GetSeedingRules(client.Id);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        var array = JsonDocument.Parse(json).RootElement;
        array.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetSeedingRules_ReturnsRulesOrderedByPriority()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "Rule C", priority: 3);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "Rule A", priority: 1);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "Rule B", priority: 2);

        var result = await _controller.GetSeedingRules(client.Id);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        var array = JsonDocument.Parse(json).RootElement;
        array.GetArrayLength().ShouldBe(3);
        array[0].GetProperty("name").GetString().ShouldBe("Rule A");
        array[1].GetProperty("name").GetString().ShouldBe("Rule B");
        array[2].GetProperty("name").GetString().ShouldBe("Rule C");
    }

    [Fact]
    public async Task GetSeedingRules_NonExistentClient_ReturnsNotFound()
    {
        var result = await _controller.GetSeedingRules(Guid.NewGuid());
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSeedingRules_QBitClient_ReturnsTagFields()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id,
            tagsAny: ["hd", "private"], tagsAll: ["required"]);

        var result = await _controller.GetSeedingRules(client.Id);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        var rule = JsonDocument.Parse(json).RootElement[0];
        rule.GetProperty("tagsAny").GetArrayLength().ShouldBe(2);
        rule.GetProperty("tagsAll").GetArrayLength().ShouldBe(1);
        rule.GetProperty("tagsAll")[0].GetString().ShouldBe("required");
    }

    [Fact]
    public async Task GetSeedingRules_DelugeClient_ReturnsEmptyTagFields()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext, DownloadClientTypeName.Deluge, "Test Deluge");
        SeedingRulesTestDataFactory.AddDelugeSeedingRule(_dataContext, client.Id);

        var result = await _controller.GetSeedingRules(client.Id);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        var rule = JsonDocument.Parse(json).RootElement[0];
        rule.GetProperty("tagsAny").GetArrayLength().ShouldBe(0);
        rule.GetProperty("tagsAll").GetArrayLength().ShouldBe(0);
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

        var body = GetCreatedJsonBody(result);
        body.GetProperty("Name").GetString().ShouldBe("Movies Rule");
        body.GetProperty("Categories").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task CreateSeedingRule_AutoAssignsPriority_WhenNotProvided()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest();

        var result = await _controller.CreateSeedingRule(client.Id, request);

        var body = GetCreatedJsonBody(result);
        body.GetProperty("Priority").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task CreateSeedingRule_AutoAssignsSequentialPriority()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, priority: 1);

        var request = CreateValidRequest(name: "Second Rule", categories: ["tv"]);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        var body = GetCreatedJsonBody(result);
        body.GetProperty("Priority").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task CreateSeedingRule_DuplicatePriority_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, priority: 1);

        var request = CreateValidRequest(priority: 1);

        var result = await _controller.CreateSeedingRule(client.Id, request);
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateSeedingRule_NonExistentClient_ReturnsNotFound()
    {
        var request = CreateValidRequest();

        var result = await _controller.CreateSeedingRule(Guid.NewGuid(), request);
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateSeedingRule_EmptyCategories_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest(categories: []);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        // Validate() throws ValidationException → caught → BadRequest
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateSeedingRule_SanitizesWhitespaceInLists()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var request = CreateValidRequest(
            trackerPatterns: ["", "  ", "valid.com", " trimmed.com "]);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        var body = GetCreatedJsonBody(result);
        var patterns = body.GetProperty("TrackerPatterns");
        patterns.GetArrayLength().ShouldBe(2);
        patterns[0].GetString().ShouldBe("valid.com");
        patterns[1].GetString().ShouldBe("trimmed.com");
    }

    [Fact]
    public async Task CreateSeedingRule_ForTransmission_CreatesTransmissionRule()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext,
            DownloadClientTypeName.Transmission, "Test Transmission");
        var request = CreateValidRequest(tagsAny: ["tag1"]);

        var result = await _controller.CreateSeedingRule(client.Id, request);

        var createdResult = result.ShouldBeOfType<CreatedAtActionResult>();
        createdResult.Value.ShouldBeOfType<TransmissionSeedingRule>();
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
    public async Task UpdateSeedingRule_NonExistentRule_ReturnsNotFound()
    {
        var request = CreateValidRequest();

        var result = await _controller.UpdateSeedingRule(Guid.NewGuid(), request);
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateSeedingRule_ValidationFailure_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id);

        // Both maxRatio and maxSeedTime negative → validation failure
        var request = CreateValidRequest(maxRatio: -1, maxSeedTime: -1);

        var result = await _controller.UpdateSeedingRule(rule.Id, request);
        result.ShouldBeOfType<BadRequestObjectResult>();
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

        // Verify via GET
        var getResult = await _controller.GetSeedingRules(client.Id);
        var okResult = getResult.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        var array = JsonDocument.Parse(json).RootElement;

        array[0].GetProperty("name").GetString().ShouldBe("C");
        array[0].GetProperty("priority").GetInt32().ShouldBe(1);
        array[1].GetProperty("name").GetString().ShouldBe("B");
        array[1].GetProperty("priority").GetInt32().ShouldBe(2);
        array[2].GetProperty("name").GetString().ShouldBe("A");
        array[2].GetProperty("priority").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task ReorderSeedingRules_NonExistentClient_ReturnsNotFound()
    {
        var request = new ReorderSeedingRulesRequest { OrderedIds = [Guid.NewGuid()] };

        var result = await _controller.ReorderSeedingRules(Guid.NewGuid(), request);
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ReorderSeedingRules_DuplicateIds_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule1 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "A", priority: 1);
        var rule2 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "B", priority: 2);

        var request = new ReorderSeedingRulesRequest { OrderedIds = [rule1.Id, rule1.Id] };

        var result = await _controller.ReorderSeedingRules(client.Id, request);
        result.ShouldBeOfType<BadRequestObjectResult>();
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
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ReorderSeedingRules_UnknownRuleId_ReturnsBadRequest()
    {
        var client = SeedingRulesTestDataFactory.AddDownloadClient(_dataContext);
        var rule1 = SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "A", priority: 1);
        SeedingRulesTestDataFactory.AddQBitSeedingRule(_dataContext, client.Id, name: "B", priority: 2);

        var request = new ReorderSeedingRulesRequest { OrderedIds = [rule1.Id, Guid.NewGuid()] };

        var result = await _controller.ReorderSeedingRules(client.Id, request);
        result.ShouldBeOfType<BadRequestObjectResult>();
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

        // Verify rule no longer exists
        var getResult = await _controller.GetSeedingRules(client.Id);
        var okResult = getResult.ShouldBeOfType<OkObjectResult>();
        var json = JsonSerializer.Serialize(okResult.Value);
        var array = JsonDocument.Parse(json).RootElement;
        array.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task DeleteSeedingRule_NonExistentRule_ReturnsNotFound()
    {
        var result = await _controller.DeleteSeedingRule(Guid.NewGuid());
        result.ShouldBeOfType<NotFoundObjectResult>();
    }
}
