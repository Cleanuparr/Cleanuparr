using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Api.Features.DownloadCleaner.Controllers;

[ApiController]
[Route("api/seeding-rules")]
[Authorize]
public class SeedingRulesController : ControllerBase
{
    private readonly ILogger<SeedingRulesController> _logger;
    private readonly DataContext _dataContext;

    public SeedingRulesController(
        ILogger<SeedingRulesController> logger,
        DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    [HttpGet("{downloadClientId}")]
    public async Task<IActionResult> GetSeedingRules(Guid downloadClientId)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var client = await _dataContext.DownloadClients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == downloadClientId);

            if (client is null)
            {
                return NotFound(new { Message = $"Download client with ID {downloadClientId} not found" });
            }

            var rules = await SeedingRuleHelper.GetForClientAsync(_dataContext, client);

            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve seeding rules for client {ClientId}", downloadClientId);
            return StatusCode(500, new { Message = "Failed to retrieve seeding rules", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("{downloadClientId}")]
    public async Task<IActionResult> CreateSeedingRule(Guid downloadClientId, [FromBody] SeedingRuleRequest ruleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var client = await _dataContext.DownloadClients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == downloadClientId);

            if (client is null)
            {
                return NotFound(new { Message = $"Download client with ID {downloadClientId} not found" });
            }

            var existingRules = await SeedingRuleHelper.GetForClientAsync(_dataContext, client);

            if (ruleDto.Priority.HasValue && existingRules.Any(r => r.Priority == ruleDto.Priority.Value))
            {
                return BadRequest(new { Message = $"A seeding rule with priority {ruleDto.Priority.Value} already exists for this client" });
            }

            int priority = ruleDto.Priority ?? (existingRules.Count == 0 ? 1 : existingRules.Max(r => r.Priority) + 1);

            var rule = CreateRule(client.TypeName, client.Id, ruleDto, priority);
            rule.Validate();

            AddRuleToDbSet(rule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Created seeding rule: {RuleName} with ID: {RuleId} for client {ClientId}",
                rule.Name, rule.Id, downloadClientId);

            return CreatedAtAction(nameof(GetSeedingRules), new { downloadClientId }, rule);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for seeding rule creation: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create seeding rule: {RuleName} for client {ClientId}",
                ruleDto.Name, downloadClientId);
            return StatusCode(500, new { Message = "Failed to create seeding rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSeedingRule(Guid id, [FromBody] SeedingRuleRequest ruleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var (existingRule, _) = await SeedingRuleHelper.FindByIdAsync(_dataContext, id);

            if (existingRule is null)
            {
                return NotFound(new { Message = $"Seeding rule with ID {id} not found" });
            }

            existingRule.Name = ruleDto.Name.Trim();
            existingRule.Categories = SanitizeStringList(ruleDto.Categories);
            existingRule.TrackerPatterns = SanitizeStringList(ruleDto.TrackerPatterns);
            existingRule.PrivacyType = ruleDto.PrivacyType;
            existingRule.MaxRatio = ruleDto.MaxRatio;
            existingRule.MinSeedTime = ruleDto.MinSeedTime;
            existingRule.MaxSeedTime = ruleDto.MaxSeedTime;
            existingRule.DeleteSourceFiles = ruleDto.DeleteSourceFiles;
            // Priority is intentionally NOT updated here — use the reorder endpoint

            if (existingRule is ITagFilterable tagFilterable)
            {
                tagFilterable.TagsAny = SanitizeStringList(ruleDto.TagsAny);
                tagFilterable.TagsAll = SanitizeStringList(ruleDto.TagsAll);
            }

            existingRule.Validate();

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Updated seeding rule: {RuleName} with ID: {RuleId}", existingRule.Name, id);

            return Ok(existingRule);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for seeding rule update: {Message}", ex.Message);
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update seeding rule with ID: {RuleId}", id);
            return StatusCode(500, new { Message = "Failed to update seeding rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("{downloadClientId}/reorder")]
    public async Task<IActionResult> ReorderSeedingRules(Guid downloadClientId, [FromBody] ReorderSeedingRulesRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await DataContext.Lock.WaitAsync();
        try
        {
            var client = await _dataContext.DownloadClients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == downloadClientId);

            if (client is null)
            {
                return NotFound(new { Message = $"Download client with ID {downloadClientId} not found" });
            }

            List<ISeedingRule> rules = await SeedingRuleHelper.GetForClientTrackedAsync(_dataContext, client);

            if (request.OrderedIds.Distinct().Count() != request.OrderedIds.Count)
            {
                return BadRequest(new { Message = "Duplicate rule IDs are not allowed" });
            }

            if (request.OrderedIds.Count != rules.Count)
            {
                return BadRequest(new { Message = $"Expected {rules.Count} rule IDs but received {request.OrderedIds.Count}. All rules must be included." });
            }

            foreach (Guid id in request.OrderedIds.Where(id => rules.All(r => r.Id != id)))
            {
                return BadRequest(new { Message = $"Rule with ID {id} not found for client {downloadClientId}" });
            }

            int priority = 1;
            var lookup = rules.ToDictionary(r => r.Id);

            foreach (var id in request.OrderedIds)
            {
                lookup[id].Priority = priority++;
            }

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Reordered {Count} seeding rules for client {ClientId}", rules.Count, downloadClientId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder seeding rules for client {ClientId}", downloadClientId);
            return StatusCode(500, new { Message = "Failed to reorder seeding rules", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSeedingRule(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var (existingRule, _) = await SeedingRuleHelper.FindByIdAsync(_dataContext, id);

            if (existingRule is null)
            {
                return NotFound(new { Message = $"Seeding rule with ID {id} not found" });
            }

            RemoveRuleFromDbSet(existingRule);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Deleted seeding rule: {RuleName} with ID: {RuleId}", existingRule.Name, id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete seeding rule with ID: {RuleId}", id);
            return StatusCode(500, new { Message = "Failed to delete seeding rule", Error = ex.Message });
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    private static List<string> SanitizeStringList(List<string> list)
        => list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();

    private static ISeedingRule CreateRule(DownloadClientTypeName typeName, Guid clientId, SeedingRuleRequest dto, int priority)
    {
        var categories = SanitizeStringList(dto.Categories);
        var trackerPatterns = SanitizeStringList(dto.TrackerPatterns);
        var tagsAny = SanitizeStringList(dto.TagsAny);
        var tagsAll = SanitizeStringList(dto.TagsAll);

        return typeName switch
        {
            DownloadClientTypeName.qBittorrent => new QBitSeedingRule
            {
                DownloadClientConfigId = clientId,
                Name = dto.Name.Trim(),
                Categories = categories,
                TrackerPatterns = trackerPatterns,
                TagsAny = tagsAny,
                TagsAll = tagsAll,
                Priority = priority,
                PrivacyType = dto.PrivacyType,
                MaxRatio = dto.MaxRatio,
                MinSeedTime = dto.MinSeedTime,
                MaxSeedTime = dto.MaxSeedTime,
                DeleteSourceFiles = dto.DeleteSourceFiles,
            },
            DownloadClientTypeName.Deluge => new DelugeSeedingRule
            {
                DownloadClientConfigId = clientId,
                Name = dto.Name.Trim(),
                Categories = categories,
                TrackerPatterns = trackerPatterns,
                Priority = priority,
                PrivacyType = dto.PrivacyType,
                MaxRatio = dto.MaxRatio,
                MinSeedTime = dto.MinSeedTime,
                MaxSeedTime = dto.MaxSeedTime,
                DeleteSourceFiles = dto.DeleteSourceFiles,
            },
            DownloadClientTypeName.Transmission => new TransmissionSeedingRule
            {
                DownloadClientConfigId = clientId,
                Name = dto.Name.Trim(),
                Categories = categories,
                TrackerPatterns = trackerPatterns,
                TagsAny = tagsAny,
                TagsAll = tagsAll,
                Priority = priority,
                PrivacyType = dto.PrivacyType,
                MaxRatio = dto.MaxRatio,
                MinSeedTime = dto.MinSeedTime,
                MaxSeedTime = dto.MaxSeedTime,
                DeleteSourceFiles = dto.DeleteSourceFiles,
            },
            DownloadClientTypeName.uTorrent => new UTorrentSeedingRule
            {
                DownloadClientConfigId = clientId,
                Name = dto.Name.Trim(),
                Categories = categories,
                TrackerPatterns = trackerPatterns,
                Priority = priority,
                PrivacyType = dto.PrivacyType,
                MaxRatio = dto.MaxRatio,
                MinSeedTime = dto.MinSeedTime,
                MaxSeedTime = dto.MaxSeedTime,
                DeleteSourceFiles = dto.DeleteSourceFiles,
            },
            DownloadClientTypeName.rTorrent => new RTorrentSeedingRule
            {
                DownloadClientConfigId = clientId,
                Name = dto.Name.Trim(),
                Categories = categories,
                TrackerPatterns = trackerPatterns,
                Priority = priority,
                PrivacyType = dto.PrivacyType,
                MaxRatio = dto.MaxRatio,
                MinSeedTime = dto.MinSeedTime,
                MaxSeedTime = dto.MaxSeedTime,
                DeleteSourceFiles = dto.DeleteSourceFiles,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "Unsupported download client type")
        };
    }

    private void AddRuleToDbSet(ISeedingRule rule)
    {
        switch (rule)
        {
            case QBitSeedingRule qbit:
                _dataContext.QBitSeedingRules.Add(qbit);
                break;
            case DelugeSeedingRule deluge:
                _dataContext.DelugeSeedingRules.Add(deluge);
                break;
            case TransmissionSeedingRule transmission:
                _dataContext.TransmissionSeedingRules.Add(transmission);
                break;
            case UTorrentSeedingRule utorrent:
                _dataContext.UTorrentSeedingRules.Add(utorrent);
                break;
            case RTorrentSeedingRule rtorrent:
                _dataContext.RTorrentSeedingRules.Add(rtorrent);
                break;
        }
    }

    private void RemoveRuleFromDbSet(ISeedingRule rule)
    {
        switch (rule)
        {
            case QBitSeedingRule qbit:
                _dataContext.QBitSeedingRules.Remove(qbit);
                break;
            case DelugeSeedingRule deluge:
                _dataContext.DelugeSeedingRules.Remove(deluge);
                break;
            case TransmissionSeedingRule transmission:
                _dataContext.TransmissionSeedingRules.Remove(transmission);
                break;
            case UTorrentSeedingRule utorrent:
                _dataContext.UTorrentSeedingRules.Remove(utorrent);
                break;
            case RTorrentSeedingRule rtorrent:
                _dataContext.RTorrentSeedingRules.Remove(rtorrent);
                break;
        }
    }
}
