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

            var rules = await GetSeedingRulesForClient(client);

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

            var existingRules = await GetSeedingRulesForClient(client);
            var duplicate = existingRules.FirstOrDefault(r =>
                r.Name.Equals(ruleDto.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                r.PrivacyType == ruleDto.PrivacyType);

            if (duplicate is not null)
            {
                return BadRequest(new { Message = "A seeding rule with this name and privacy type already exists for this client" });
            }

            var overlapError = GetPrivacyTypeOverlapError(ruleDto.Name.Trim(), ruleDto.PrivacyType, existingRules, excludeId: null);
            if (overlapError is not null)
            {
                return BadRequest(new { Message = overlapError });
            }

            var rule = CreateRule(client.TypeName, client.Id, ruleDto);
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
            var (existingRule, _) = await FindRuleById(id);

            if (existingRule is null)
            {
                return NotFound(new { Message = $"Seeding rule with ID {id} not found" });
            }

            // Check for duplicate name+privacyType on the same client, excluding this rule
            var clientRules = await GetSeedingRulesForClientId(existingRule.DownloadClientConfigId);
            var duplicate = clientRules.FirstOrDefault(r =>
                r.Id != id &&
                r.Name.Equals(ruleDto.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                r.PrivacyType == ruleDto.PrivacyType);

            if (duplicate is not null)
            {
                return BadRequest(new { Message = "A seeding rule with this name and privacy type already exists for this client" });
            }

            var overlapError = GetPrivacyTypeOverlapError(ruleDto.Name.Trim(), ruleDto.PrivacyType, clientRules, excludeId: id);
            if (overlapError is not null)
            {
                return BadRequest(new { Message = overlapError });
            }

            existingRule.Name = ruleDto.Name.Trim();
            existingRule.PrivacyType = ruleDto.PrivacyType;
            existingRule.MaxRatio = ruleDto.MaxRatio;
            existingRule.MinSeedTime = ruleDto.MinSeedTime;
            existingRule.MaxSeedTime = ruleDto.MaxSeedTime;
            existingRule.DeleteSourceFiles = ruleDto.DeleteSourceFiles;

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

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSeedingRule(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var (existingRule, _) = await FindRuleById(id);

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

    private static string? GetPrivacyTypeOverlapError(
        string name,
        TorrentPrivacyType privacyType,
        IEnumerable<ISeedingRule> existingRules,
        Guid? excludeId)
    {
        if (privacyType == TorrentPrivacyType.Both)
        {
            var hasConflict = existingRules.Any(r =>
                r.Id != excludeId &&
                r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                r.PrivacyType != TorrentPrivacyType.Both);

            return hasConflict
                ? "A 'Both' rule cannot coexist with a Public or Private rule for the same category"
                : null;
        }
        else
        {
            var hasConflict = existingRules.Any(r =>
                r.Id != excludeId &&
                r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                r.PrivacyType == TorrentPrivacyType.Both);

            return hasConflict
                ? "A Public or Private rule cannot coexist with a 'Both' rule for the same category"
                : null;
        }
    }

    private ISeedingRule CreateRule(DownloadClientTypeName typeName, Guid clientId, SeedingRuleRequest dto)
    {
        return typeName switch
        {
            DownloadClientTypeName.qBittorrent => new QBitSeedingRule
            {
                DownloadClientConfigId = clientId,
                Name = dto.Name.Trim(),
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

    private async Task<(ISeedingRule? rule, object? dbSet)> FindRuleById(Guid id)
    {
        var qbit = await _dataContext.QBitSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (qbit is not null) return (qbit, _dataContext.QBitSeedingRules);

        var deluge = await _dataContext.DelugeSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (deluge is not null) return (deluge, _dataContext.DelugeSeedingRules);

        var transmission = await _dataContext.TransmissionSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (transmission is not null) return (transmission, _dataContext.TransmissionSeedingRules);

        var utorrent = await _dataContext.UTorrentSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (utorrent is not null) return (utorrent, _dataContext.UTorrentSeedingRules);

        var rtorrent = await _dataContext.RTorrentSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rtorrent is not null) return (rtorrent, _dataContext.RTorrentSeedingRules);

        return (null, null);
    }

    private async Task<List<ISeedingRule>> GetSeedingRulesForClient(DownloadClientConfig client)
    {
        return client.TypeName switch
        {
            DownloadClientTypeName.qBittorrent => (await _dataContext.QBitSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Deluge => (await _dataContext.DelugeSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Transmission => (await _dataContext.TransmissionSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.uTorrent => (await _dataContext.UTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.rTorrent => (await _dataContext.RTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id).AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            _ => []
        };
    }

    private async Task<List<ISeedingRule>> GetSeedingRulesForClientId(Guid clientId)
    {
        var client = await _dataContext.DownloadClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId);

        if (client is null)
        {
            return [];
        }

        return await GetSeedingRulesForClient(client);
    }
}
