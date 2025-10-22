using Cleanuparr.Api.Features.QueueCleaner.Contracts.Requests;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Api.Features.QueueCleaner.Controllers;

[ApiController]
[Route("api/queue-rules")]
public class QueueRulesController : ControllerBase
{
    private readonly ILogger<QueueRulesController> _logger;
    private readonly DataContext _dataContext;
    private readonly IRuleIntervalValidator _ruleIntervalValidator;

    public QueueRulesController(
        ILogger<QueueRulesController> logger,
        DataContext dataContext,
        IRuleIntervalValidator ruleIntervalValidator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _ruleIntervalValidator = ruleIntervalValidator;
    }

    [HttpGet("stall")]
    public async Task<IActionResult> GetStallRules()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var rules = await _dataContext.StallRules
                .OrderBy(r => r.MinCompletionPercentage)
                .ThenBy(r => r.Name)
                .AsNoTracking()
                .ToListAsync();

            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve stall rules");
            return StatusCode(500, new { Message = "Failed to retrieve stall rules", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("stall")]
    public async Task<IActionResult> CreateStallRule([FromBody] StallRuleDto ruleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var queueCleanerConfig = await _dataContext.QueueCleanerConfigs
                .FirstAsync();

            var existingRule = await _dataContext.StallRules
                .FirstOrDefaultAsync(r => r.Name.ToLower() == ruleDto.Name.ToLower());
            
            if (existingRule != null)
            {
                return BadRequest(new { Message = "A stall rule with this name already exists" });
            }

            var rule = new StallRule
            {
                Id = Guid.NewGuid(),
                QueueCleanerConfigId = queueCleanerConfig.Id,
                Name = ruleDto.Name.Trim(),
                Enabled = ruleDto.Enabled,
                MaxStrikes = ruleDto.MaxStrikes,
                PrivacyType = ruleDto.PrivacyType,
                MinCompletionPercentage = ruleDto.MinCompletionPercentage,
                MaxCompletionPercentage = ruleDto.MaxCompletionPercentage,
                ResetStrikesOnProgress = ruleDto.ResetStrikesOnProgress,
                DeletePrivateTorrentsFromClient = ruleDto.DeletePrivateTorrentsFromClient,
                MinimumProgress = ruleDto.MinimumProgress?.Trim(),
            };

            var existingRules = await _dataContext.StallRules.ToListAsync();
            
            var intervalValidationResult = _ruleIntervalValidator.ValidateStallRuleIntervals(rule, existingRules);
            if (!intervalValidationResult.IsValid)
            {
                return BadRequest(new { Message = intervalValidationResult.ErrorMessage });
            }

            rule.Validate();

            _dataContext.StallRules.Add(rule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Created stall rule: {RuleName} with ID: {RuleId}", rule.Name, rule.Id);

            return CreatedAtAction(nameof(GetStallRules), new { id = rule.Id }, rule);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for stall rule creation: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create stall rule: {RuleName}", ruleDto.Name);
            return StatusCode(500, new { Message = "Failed to create stall rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("stall/{id}")]
    public async Task<IActionResult> UpdateStallRule(Guid id, [FromBody] StallRuleDto ruleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var existingRule = await _dataContext.StallRules
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existingRule == null)
            {
                return NotFound(new { Message = $"Stall rule with ID {id} not found" });
            }

            var duplicateRule = await _dataContext.StallRules
                .FirstOrDefaultAsync(r => r.Id != id && r.Name.ToLower() == ruleDto.Name.ToLower());
            
            if (duplicateRule != null)
            {
                return BadRequest(new { Message = "A stall rule with this name already exists" });
            }

            var updatedRule = existingRule with
            {
                Name = ruleDto.Name.Trim(),
                Enabled = ruleDto.Enabled,
                MaxStrikes = ruleDto.MaxStrikes,
                PrivacyType = ruleDto.PrivacyType,
                MinCompletionPercentage = ruleDto.MinCompletionPercentage,
                MaxCompletionPercentage = ruleDto.MaxCompletionPercentage,
                ResetStrikesOnProgress = ruleDto.ResetStrikesOnProgress,
                DeletePrivateTorrentsFromClient = ruleDto.DeletePrivateTorrentsFromClient,
                MinimumProgress = ruleDto.MinimumProgress?.Trim(),
            };

            var existingRules = await _dataContext.StallRules
                .Where(r => r.Id != id)
                .ToListAsync();
            
            var intervalValidationResult = _ruleIntervalValidator.ValidateStallRuleIntervals(updatedRule, existingRules);
            if (!intervalValidationResult.IsValid)
            {
                return BadRequest(new { Message = intervalValidationResult.ErrorMessage });
            }

            updatedRule.Validate();

            _dataContext.Entry(existingRule).CurrentValues.SetValues(updatedRule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Updated stall rule: {RuleName} with ID: {RuleId}", updatedRule.Name, id);

            return Ok(updatedRule);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for stall rule update: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stall rule with ID: {RuleId}", id);
            return StatusCode(500, new { Message = "Failed to update stall rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("stall/{id}")]
    public async Task<IActionResult> DeleteStallRule(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingRule = await _dataContext.StallRules
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existingRule == null)
            {
                return NotFound(new { Message = $"Stall rule with ID {id} not found" });
            }

            _dataContext.StallRules.Remove(existingRule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Deleted stall rule: {RuleName} with ID: {RuleId}", existingRule.Name, id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete stall rule with ID: {RuleId}", id);
            return StatusCode(500, new { Message = "Failed to delete stall rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("slow")]
    public async Task<IActionResult> GetSlowRules()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var rules = await _dataContext.SlowRules
                .OrderBy(r => r.MinCompletionPercentage)
                .ThenBy(r => r.Name)
                .AsNoTracking()
                .ToListAsync();

            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve slow rules");
            return StatusCode(500, new { Message = "Failed to retrieve slow rules", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("slow")]
    public async Task<IActionResult> CreateSlowRule([FromBody] SlowRuleDto ruleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var queueCleanerConfig = await _dataContext.QueueCleanerConfigs
                .FirstAsync();

            var existingRule = await _dataContext.SlowRules
                .FirstOrDefaultAsync(r => r.Name.ToLower() == ruleDto.Name.ToLower());
            
            if (existingRule != null)
            {
                return BadRequest(new { Message = "A slow rule with this name already exists" });
            }

            var rule = new SlowRule
            {
                Id = Guid.NewGuid(),
                QueueCleanerConfigId = queueCleanerConfig.Id,
                Name = ruleDto.Name.Trim(),
                Enabled = ruleDto.Enabled,
                MaxStrikes = ruleDto.MaxStrikes,
                PrivacyType = ruleDto.PrivacyType,
                MinCompletionPercentage = ruleDto.MinCompletionPercentage,
                MaxCompletionPercentage = ruleDto.MaxCompletionPercentage,
                ResetStrikesOnProgress = ruleDto.ResetStrikesOnProgress,
                MinSpeed = ruleDto.MinSpeed?.Trim() ?? string.Empty,
                MaxTimeHours = ruleDto.MaxTimeHours,
                IgnoreAboveSize = ruleDto.IgnoreAboveSize,
                DeletePrivateTorrentsFromClient = ruleDto.DeletePrivateTorrentsFromClient,
            };

            var existingRules = await _dataContext.SlowRules.ToListAsync();
            
            var intervalValidationResult = _ruleIntervalValidator.ValidateSlowRuleIntervals(rule, existingRules);
            if (!intervalValidationResult.IsValid)
            {
                return BadRequest(new { Message = intervalValidationResult.ErrorMessage });
            }

            rule.Validate();

            _dataContext.SlowRules.Add(rule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Created slow rule: {RuleName} with ID: {RuleId}", rule.Name, rule.Id);

            return CreatedAtAction(nameof(GetSlowRules), new { id = rule.Id }, rule);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for slow rule creation: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create slow rule: {RuleName}", ruleDto.Name);
            return StatusCode(500, new { Message = "Failed to create slow rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("slow/{id}")]
    public async Task<IActionResult> UpdateSlowRule(Guid id, [FromBody] SlowRuleDto ruleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var existingRule = await _dataContext.SlowRules
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existingRule == null)
            {
                return NotFound(new { Message = $"Slow rule with ID {id} not found" });
            }

            var duplicateRule = await _dataContext.SlowRules
                .FirstOrDefaultAsync(r => r.Id != id && r.Name.ToLower() == ruleDto.Name.ToLower());
            
            if (duplicateRule != null)
            {
                return BadRequest(new { Message = "A slow rule with this name already exists" });
            }

            var updatedRule = existingRule with
            {
                Name = ruleDto.Name.Trim(),
                Enabled = ruleDto.Enabled,
                MaxStrikes = ruleDto.MaxStrikes,
                PrivacyType = ruleDto.PrivacyType,
                MinCompletionPercentage = ruleDto.MinCompletionPercentage,
                MaxCompletionPercentage = ruleDto.MaxCompletionPercentage,
                ResetStrikesOnProgress = ruleDto.ResetStrikesOnProgress,
                MinSpeed = ruleDto.MinSpeed?.Trim() ?? string.Empty,
                MaxTimeHours = ruleDto.MaxTimeHours,
                IgnoreAboveSize = ruleDto.IgnoreAboveSize,
                DeletePrivateTorrentsFromClient = ruleDto.DeletePrivateTorrentsFromClient,
            };

            var existingRules = await _dataContext.SlowRules
                .Where(r => r.Id != id)
                .ToListAsync();
            
            var intervalValidationResult = _ruleIntervalValidator.ValidateSlowRuleIntervals(updatedRule, existingRules);
            if (!intervalValidationResult.IsValid)
            {
                return BadRequest(new { Message = intervalValidationResult.ErrorMessage });
            }

            updatedRule.Validate();

            _dataContext.Entry(existingRule).CurrentValues.SetValues(updatedRule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Updated slow rule: {RuleName} with ID: {RuleId}", updatedRule.Name, id);

            return Ok(updatedRule);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for slow rule update: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update slow rule with ID: {RuleId}", id);
            return StatusCode(500, new { Message = "Failed to update slow rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("slow/{id}")]
    public async Task<IActionResult> DeleteSlowRule(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var existingRule = await _dataContext.SlowRules
                .FirstOrDefaultAsync(r => r.Id == id);

            if (existingRule == null)
            {
                return NotFound(new { Message = $"Slow rule with ID {id} not found" });
            }

            _dataContext.SlowRules.Remove(existingRule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Deleted slow rule: {RuleName} with ID: {RuleId}", existingRule.Name, id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete slow rule with ID: {RuleId}", id);
            return StatusCode(500, new { Message = "Failed to delete slow rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}
