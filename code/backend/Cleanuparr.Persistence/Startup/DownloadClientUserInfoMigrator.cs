using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Persistence.Startup;

/// <summary>
/// Moves URL userinfo (<c>user:pass@host</c>) out of download client hosts at startup.
/// Safe to rerun: a fixed row has no userinfo left.
/// </summary>
public sealed class DownloadClientUserInfoMigrator
{
    private readonly ILogger<DownloadClientUserInfoMigrator> _logger;

    public DownloadClientUserInfoMigrator(ILogger<DownloadClientUserInfoMigrator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Migrates all rows in one transaction.
    /// On failure it rolls back and logs, so startup continues.
    /// </summary>
    public async Task MigrateAsync(DataContext context, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            List<DownloadClientConfig> clients = await context.DownloadClients.ToListAsync(cancellationToken);

            foreach (DownloadClientConfig client in clients)
            {
                if (client.Host is null || !client.Host.IsAbsoluteUri || client.Host.UserInfo.Length == 0)
                {
                    continue;
                }

                MigrateClient(client);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(exception, "Failed to migrate download client URL credentials; no changes were applied");
        }
    }

    private void MigrateClient(DownloadClientConfig client)
    {
        UserInfoCredential credential = UserInfoMigration.ParseCredential(client.Host!.UserInfo);

        if (client.TypeName.ApiKeyConflictsWithHttpAuth() && !string.IsNullOrEmpty(client.ApiKey))
        {
            _logger.LogWarning(
                "Download client {Name} ({Id}) has an API key that conflicts with HTTP authentication; dropped the URL credentials and kept the API key",
                client.Name,
                client.Id);
        }
        else if (client.TypeName.UsesDedicatedHttpAuthFields())
        {
            client.HttpAuthUsername = credential.Username;
            client.HttpAuthPassword = credential.Password;
        }
        else if (string.IsNullOrEmpty(client.Username) && string.IsNullOrEmpty(client.Password))
        {
            client.Username = credential.Username;
            client.Password = credential.Password;
        }
        else
        {
            _logger.LogWarning(
                "Download client {Name} ({Id}) already has a username or password; kept them and dropped the URL credentials",
                client.Name,
                client.Id);
        }

        client.HttpAuthType = HttpAuthType.Basic;
        client.Host = UserInfoMigration.StripUserInfo(client.Host);
    }
}
