using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.DownloadCleaner;

internal static class SeedingRuleHelper
{
    /// <summary>
    /// Queries the appropriate per-type seeding rules table for a single client.
    /// </summary>
    public static async Task<List<ISeedingRule>> GetForClientAsync(DataContext ctx, DownloadClientConfig client)
    {
        return client.TypeName switch
        {
            DownloadClientTypeName.qBittorrent => (await ctx.QBitSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Deluge => (await ctx.DelugeSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Transmission => (await ctx.TransmissionSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.uTorrent => (await ctx.UTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.rTorrent => (await ctx.RTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .AsNoTracking().ToListAsync()).Cast<ISeedingRule>().ToList(),
            _ => [],
        };
    }

    /// <summary>
    /// Queries the appropriate per-type seeding rules table for a single client with change tracking enabled.
    /// Use this when you need to modify and save the returned entities.
    /// </summary>
    public static async Task<List<ISeedingRule>> GetForClientTrackedAsync(DataContext ctx, DownloadClientConfig client)
    {
        return client.TypeName switch
        {
            DownloadClientTypeName.qBittorrent => (await ctx.QBitSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Deluge => (await ctx.DelugeSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Transmission => (await ctx.TransmissionSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.uTorrent => (await ctx.UTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .ToListAsync()).Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.rTorrent => (await ctx.RTorrentSeedingRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .ToListAsync()).Cast<ISeedingRule>().ToList(),
            _ => [],
        };
    }

    /// <summary>
    /// Loads the client by ID then queries its seeding rules.
    /// </summary>
    public static async Task<List<ISeedingRule>> GetForClientIdAsync(DataContext ctx, Guid clientId)
    {
        var client = await ctx.DownloadClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId);

        return client is null ? [] : await GetForClientAsync(ctx, client);
    }

    /// <summary>
    /// Filters seeding rules for a client from pre-loaded in-memory lists.
    /// Use this in bulk-load scenarios to avoid N+1 queries.
    /// </summary>
    public static List<ISeedingRule> FilterForClient(
        DownloadClientConfig client,
        List<QBitSeedingRule> qbitRules,
        List<DelugeSeedingRule> delugeRules,
        List<TransmissionSeedingRule> transmissionRules,
        List<UTorrentSeedingRule> utorrentRules,
        List<RTorrentSeedingRule> rtorrentRules)
    {
        return client.TypeName switch
        {
            DownloadClientTypeName.qBittorrent => qbitRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Deluge => delugeRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.Transmission => transmissionRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.uTorrent => utorrentRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .Cast<ISeedingRule>().ToList(),
            DownloadClientTypeName.rTorrent => rtorrentRules
                .Where(r => r.DownloadClientConfigId == client.Id)
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .Cast<ISeedingRule>().ToList(),
            _ => [],
        };
    }

    /// <summary>
    /// Searches all five per-type seeding rule tables for a rule with the given ID.
    /// Returns the rule and a sentinel string identifying its type, or (null, null) if not found.
    /// </summary>
    public static async Task<(ISeedingRule? rule, object? dbSet)> FindByIdAsync(DataContext ctx, Guid id)
    {
        var qbit = await ctx.QBitSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (qbit is not null) return (qbit, ctx.QBitSeedingRules);

        var deluge = await ctx.DelugeSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (deluge is not null) return (deluge, ctx.DelugeSeedingRules);

        var transmission = await ctx.TransmissionSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (transmission is not null) return (transmission, ctx.TransmissionSeedingRules);

        var utorrent = await ctx.UTorrentSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (utorrent is not null) return (utorrent, ctx.UTorrentSeedingRules);

        var rtorrent = await ctx.RTorrentSeedingRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rtorrent is not null) return (rtorrent, ctx.RTorrentSeedingRules);

        return (null, null);
    }
}
