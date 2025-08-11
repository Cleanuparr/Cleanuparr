using Cleanuparr.Domain.Entities;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Services;

public class RuleEvaluator : IRuleEvaluator
{
    private readonly IRuleManager _ruleManager;
    private readonly IStriker _striker;
    private readonly ILogger<RuleEvaluator> _logger;

    public RuleEvaluator(IRuleManager ruleManager, IStriker striker, ILogger<RuleEvaluator> logger)
    {
        _ruleManager = ruleManager;
        _striker = striker;
        _logger = logger;
    }

    public async Task<DownloadCheckResult> EvaluateStallRulesAsync(ITorrentInfo torrent)
    {
        _logger.LogDebug("Evaluating stall rules for torrent '{TorrentName}' ({Hash})", torrent.Name, torrent.Hash);

        var result = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = torrent.IsPrivate
        };

        // Get matching stall rules in priority order
        var matchingRules = await _ruleManager.GetMatchingRulesAsync<StallRule>(torrent);

        if (matchingRules.Count == 0)
        {
            _logger.LogDebug("No stall rules match torrent '{TorrentName}'. No action will be taken.", torrent.Name);
            return result;
        }

        foreach (var rule in matchingRules)
        {
            _logger.LogDebug("Applying stall rule '{RuleName}' to torrent '{TorrentName}'", rule.Name, torrent.Name);

            try
            {
                // Apply strike and check if torrent should be removed
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrent.Hash, 
                    torrent.Name, 
                    (ushort)rule.MaxStrikes, 
                    StrikeType.Stalled);

                if (shouldRemove)
                {
                    result.ShouldRemove = true;
                    result.DeleteReason = DeleteReason.Stalled;
                    
                    _logger.LogInformation("Torrent '{TorrentName}' marked for removal by stall rule '{RuleName}' after reaching {MaxStrikes} strikes", 
                        torrent.Name, rule.Name, rule.MaxStrikes);
                }
                else
                {
                    _logger.LogDebug("Strike applied to torrent '{TorrentName}' by stall rule '{RuleName}', but removal threshold not reached", 
                        torrent.Name, rule.Name);
                }

                // First-match logic: stop after applying the first matching rule
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying stall rule '{RuleName}' to torrent '{TorrentName}'. Skipping rule.", 
                    rule.Name, torrent.Name);
            }
        }

        return result;
    }

    public async Task<DownloadCheckResult> EvaluateSlowRulesAsync(ITorrentInfo torrent)
    {
        _logger.LogDebug("Evaluating slow rules for torrent '{TorrentName}' ({Hash})", torrent.Name, torrent.Hash);

        var result = new DownloadCheckResult
        {
            Found = true,
            IsPrivate = torrent.IsPrivate
        };

        // Get matching slow rules in priority order
        var matchingRules = await _ruleManager.GetMatchingRulesAsync<SlowRule>(torrent);

        if (matchingRules.Count == 0)
        {
            _logger.LogDebug("No slow rules match torrent '{TorrentName}'. No action will be taken.", torrent.Name);
            return result;
        }

        // Apply first-match logic - evaluate rules in priority order and apply the first matching rule
        foreach (var rule in matchingRules)
        {
            _logger.LogDebug("Applying slow rule '{RuleName}' to torrent '{TorrentName}'", rule.Name, torrent.Name);

            try
            {
                // For slow rules, we need additional torrent information to evaluate speed/time criteria
                // This would typically come from the download client, but for now we'll apply the strike
                // The actual speed/time evaluation would be done by the calling download service
                
                bool shouldRemove = await _striker.StrikeAndCheckLimit(
                    torrent.Hash, 
                    torrent.Name, 
                    (ushort)rule.MaxStrikes, 
                    StrikeType.SlowSpeed); // Default to SlowSpeed, could be enhanced to determine type

                if (shouldRemove)
                {
                    result.ShouldRemove = true;
                    result.DeleteReason = DeleteReason.SlowSpeed; // Could be SlowTime based on rule criteria
                    
                    _logger.LogInformation("Torrent '{TorrentName}' marked for removal by slow rule '{RuleName}' after reaching {MaxStrikes} strikes", 
                        torrent.Name, rule.Name, rule.MaxStrikes);
                }
                else
                {
                    _logger.LogDebug("Strike applied to torrent '{TorrentName}' by slow rule '{RuleName}', but removal threshold not reached", 
                        torrent.Name, rule.Name);
                }

                // First-match logic: stop after applying the first matching rule
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying slow rule '{RuleName}' to torrent '{TorrentName}'. Skipping rule.", 
                    rule.Name, torrent.Name);
                continue;
            }
        }

        return result;
    }
}