using System;

using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;

namespace Cleanuparr.Api.Features.BlacklistSync.Contracts.Requests;

public sealed record UpdateBlacklistSyncConfigRequest
{
    public bool Enabled { get; init; }

    public string? BlacklistPath { get; init; }

    /// <summary>
    /// Applies the request to the provided configuration instance.
    /// </summary>
    public BlacklistSyncConfig ApplyTo(BlacklistSyncConfig config)
    {
        config.Enabled = Enabled;
        config.BlacklistPath = BlacklistPath;

        return config;
    }

    public bool HasPathChanged(string? currentPath)
        => !string.Equals(currentPath, BlacklistPath, StringComparison.InvariantCultureIgnoreCase);
}
