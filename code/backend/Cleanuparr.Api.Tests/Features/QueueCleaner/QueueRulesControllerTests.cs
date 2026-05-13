using Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;
using Cleanuparr.Api.Features.QueueCleaner.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.QueueCleaner;

public class QueueRulesControllerTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly IRuleIntervalValidator _validator;
    private readonly QueueRulesController _controller;

    public QueueRulesControllerTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        var logger = Substitute.For<ILogger<QueueRulesController>>();
        _validator = Substitute.For<IRuleIntervalValidator>();
        _validator.ValidateStallRuleIntervals(Arg.Any<StallRule>(), Arg.Any<List<StallRule>>()).Returns(ValidationResult.Success());
        _validator.ValidateSlowRuleIntervals(Arg.Any<SlowRule>(), Arg.Any<List<SlowRule>>()).Returns(ValidationResult.Success());
        _controller = new QueueRulesController(logger, _dataContext, _validator);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Stall Rules

    [Fact]
    public async Task GetStallRules_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _controller.GetStallRules();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var rules = ok.Value.ShouldBeOfType<List<StallRule>>();
        rules.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateStallRule_NewName_ReturnsCreatedWithRule()
    {
        // Arrange
        var dto = NewStallDto(name: "default");

        // Act
        var result = await _controller.CreateStallRule(dto);

        // Assert
        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        var rule = created.Value.ShouldBeOfType<StallRule>();
        rule.Name.ShouldBe("default");
        rule.Id.ShouldNotBe(Guid.Empty);
        (await _dataContext.StallRules.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateStallRule_DuplicateName_ReturnsBadRequest()
    {
        // Arrange — seed existing rule
        await _controller.CreateStallRule(NewStallDto(name: "rule-a"));

        // Act — try to create another with same name (case-insensitive)
        var result = await _controller.CreateStallRule(NewStallDto(name: "RULE-A"));

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        (await _dataContext.StallRules.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateStallRule_IntervalValidatorFails_ReturnsBadRequest()
    {
        // Arrange
        _validator.ValidateStallRuleIntervals(Arg.Any<StallRule>(), Arg.Any<List<StallRule>>())
            .Returns(ValidationResult.Failure("overlaps with existing rule"));

        // Act
        var result = await _controller.CreateStallRule(NewStallDto(name: "x"));

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        (await _dataContext.StallRules.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateStallRule_NonExistentId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.UpdateStallRule(Guid.NewGuid(), NewStallDto("x"));

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateStallRule_ExistingRule_PersistsChanges()
    {
        // Arrange
        var create = (CreatedAtActionResult)await _controller.CreateStallRule(NewStallDto(name: "orig"));
        var id = ((StallRule)create.Value!).Id;

        // Act
        var result = await _controller.UpdateStallRule(id, NewStallDto(name: "renamed"));

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var rule = ok.Value.ShouldBeOfType<StallRule>();
        rule.Name.ShouldBe("renamed");
        var saved = await _dataContext.StallRules.AsNoTracking().FirstAsync(r => r.Id == id);
        saved.Name.ShouldBe("renamed");
    }

    [Fact]
    public async Task UpdateStallRule_DuplicateNameOtherRule_ReturnsBadRequest()
    {
        // Arrange
        await _controller.CreateStallRule(NewStallDto(name: "alpha"));
        var betaCreate = (CreatedAtActionResult)await _controller.CreateStallRule(NewStallDto(name: "beta"));
        var betaId = ((StallRule)betaCreate.Value!).Id;

        // Act — try to rename beta → alpha
        var result = await _controller.UpdateStallRule(betaId, NewStallDto(name: "alpha"));

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteStallRule_ExistingRule_ReturnsNoContent()
    {
        // Arrange
        var create = (CreatedAtActionResult)await _controller.CreateStallRule(NewStallDto(name: "doomed"));
        var id = ((StallRule)create.Value!).Id;

        // Act
        var result = await _controller.DeleteStallRule(id);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        (await _dataContext.StallRules.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteStallRule_NonExistent_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteStallRule(Guid.NewGuid());

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Slow Rules

    [Fact]
    public async Task GetSlowRules_EmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var result = await _controller.GetSlowRules();

        // Assert
        var ok = result.ShouldBeOfType<OkObjectResult>();
        var rules = ok.Value.ShouldBeOfType<List<SlowRule>>();
        rules.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateSlowRule_NewName_ReturnsCreated()
    {
        // Arrange
        var dto = NewSlowDto(name: "slow-default");

        // Act
        var result = await _controller.CreateSlowRule(dto);

        // Assert
        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        var rule = created.Value.ShouldBeOfType<SlowRule>();
        rule.Name.ShouldBe("slow-default");
        (await _dataContext.SlowRules.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateSlowRule_DuplicateName_ReturnsBadRequest()
    {
        // Arrange
        await _controller.CreateSlowRule(NewSlowDto(name: "x"));

        // Act
        var result = await _controller.CreateSlowRule(NewSlowDto(name: "X"));

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateSlowRule_NonExistentId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.UpdateSlowRule(Guid.NewGuid(), NewSlowDto("x"));

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteSlowRule_NonExistent_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteSlowRule(Guid.NewGuid());

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

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
