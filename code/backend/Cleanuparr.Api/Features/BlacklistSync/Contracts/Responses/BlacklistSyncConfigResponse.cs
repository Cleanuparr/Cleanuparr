using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;

namespace Cleanuparr.Api.Features.BlacklistSync.Contracts.Responses;

public sealed record BlacklistSyncConfigResponse
{
    public bool Enabled { get; init; }

    public required string CronExpression { get; init; }

    public string? BlacklistPath { get; init; }

    public static BlacklistSyncConfigResponse From(BlacklistSyncConfig config) => new()
    {
        Enabled = config.Enabled,
        CronExpression = config.CronExpression,
        BlacklistPath = config.BlacklistPath,
    };
}
