using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Providers;
using Cleanuparr.Persistence.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Persistence;

[Collection("SeedParity")]
public sealed class DownloadClientUserInfoMigratorTests
{
    [Fact]
    public async Task Mixed_batch_migrates_on_sqlite()
    {
        using SqliteTestDatabase sqlite = SqliteTestDatabase.Create("userinfo-migration");

        await Should.NotThrowAsync(() => RunAsync(sqlite.CreateContext<DataContext>));
    }

    [SkippableFact]
    public async Task Mixed_batch_migrates_on_postgres()
    {
        PostgreSqlContainer postgresContainer;

        try
        {
            postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:17")
                .Build();

            await postgresContainer.StartAsync();
        }
        catch (Exception exception)
        {
            throw new SkipException($"Docker is unavailable, skipping userinfo migration test: {exception.Message}");
        }

        try
        {
            PostgresDatabaseProvider provider = new();
            string connectionString = postgresContainer.GetConnectionString();

            await RunAsync(() => CreatePostgresContext(connectionString, provider));
        }
        finally
        {
            await postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Bad_row_rolls_back_the_whole_transaction_and_logs_an_error()
    {
        using SqliteTestDatabase sqlite = SqliteTestDatabase.Create("userinfo-migration-bad-row");

        await using (DataContext migrate = sqlite.CreateContext<DataContext>())
        {
            await migrate.Database.MigrateAsync();
        }

        Guid goodId = Guid.NewGuid();
        Guid badId = Guid.NewGuid();

        await using (DataContext seed = sqlite.CreateContext<DataContext>())
        {
            seed.DownloadClients.Add(new DownloadClientConfig
            {
                Id = goodId,
                Name = "good-client",
                TypeName = DownloadClientTypeName.qBittorrent,
                Type = DownloadClientType.Torrent,
                Host = new Uri("http://user:pass@good-host/"),
            });
            await seed.SaveChangesAsync();

            // Uri rejects a raw '@' in the password, so raw SQL plants this row.
            // Reading it throws, which must roll back the whole run.
            await seed.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO download_clients (id, enabled, name, type_name, type, host, http_auth_type)
                VALUES ({0}, 0, 'bad-client', 'qbittorrent', 'torrent', 'http://u:p@ss@bad-host/', 'basic')
                """,
                badId);
        }

        ILogger<DownloadClientUserInfoMigrator> logger = Substitute.For<ILogger<DownloadClientUserInfoMigrator>>();
        DownloadClientUserInfoMigrator migrator = new(logger);

        await using (DataContext run = sqlite.CreateContext<DataContext>())
        {
            await Should.NotThrowAsync(() => migrator.MigrateAsync(run, CancellationToken.None));
        }

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        // The failed SELECT never reached the good row.
        await using DataContext assert = sqlite.CreateContext<DataContext>();
        List<string> rawHosts = await assert.Database
            .SqlQueryRaw<string>("SELECT host AS \"Value\" FROM download_clients ORDER BY name")
            .ToListAsync();
        rawHosts.ShouldBe(["http://u:p@ss@bad-host/", "http://user:pass@good-host/"]);
    }

    [Fact]
    public async Task Relative_host_row_is_skipped_and_the_rest_still_migrate()
    {
        using SqliteTestDatabase sqlite = SqliteTestDatabase.Create("userinfo-migration-relative-host");

        await using (DataContext migrate = sqlite.CreateContext<DataContext>())
        {
            await migrate.Database.MigrateAsync();
        }

        Guid relativeId = Guid.NewGuid();
        Guid credentialsId = Guid.NewGuid();

        await using (DataContext seed = sqlite.CreateContext<DataContext>())
        {
            seed.DownloadClients.Add(new DownloadClientConfig
            {
                Id = relativeId,
                Name = "relative-client",
                TypeName = DownloadClientTypeName.qBittorrent,
                Type = DownloadClientType.Torrent,
                Host = new Uri("192.168.1.5", UriKind.RelativeOrAbsolute),
            });
            seed.DownloadClients.Add(new DownloadClientConfig
            {
                Id = credentialsId,
                Name = "credentials-client",
                TypeName = DownloadClientTypeName.qBittorrent,
                Type = DownloadClientType.Torrent,
                Host = new Uri("http://user:pass@good-host/"),
            });
            await seed.SaveChangesAsync();
        }

        DownloadClientUserInfoMigrator migrator = new(Substitute.For<ILogger<DownloadClientUserInfoMigrator>>());

        await using (DataContext run = sqlite.CreateContext<DataContext>())
        {
            await migrator.MigrateAsync(run, CancellationToken.None);
        }

        await using DataContext assert = sqlite.CreateContext<DataContext>();
        DownloadClientConfig relative = await assert.DownloadClients.SingleAsync(c => c.Id == relativeId);
        DownloadClientConfig migrated = await assert.DownloadClients.SingleAsync(c => c.Id == credentialsId);

        relative.Host!.OriginalString.ShouldBe("192.168.1.5");
        migrated.Host!.UserInfo.ShouldBeEmpty();
        migrated.HttpAuthUsername.ShouldBe("user");
        migrated.HttpAuthPassword.ShouldBe("pass");
    }

    private static DataContext CreatePostgresContext(string connectionString, PostgresDatabaseProvider provider)
    {
        DbContextOptionsBuilder<DataContext> builder = new();
        builder
            .UseNpgsql(connectionString, options => options.MigrationsAssembly(PostgresDatabaseProvider.MigrationsAssembly))
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention();

        return new DataContext(builder.Options, provider);
    }

    private static async Task RunAsync(Func<DataContext> contextFactory)
    {
        Guid qbitId = Guid.NewGuid();
        Guid delugeId = Guid.NewGuid();
        Guid sabnzbdId = Guid.NewGuid();
        Guid rTorrentId = Guid.NewGuid();
        Guid transmissionId = Guid.NewGuid();
        Guid uTorrentNoCredsId = Guid.NewGuid();
        Guid uTorrentExistingCredsId = Guid.NewGuid();
        Guid noUserInfoId = Guid.NewGuid();
        Guid encodedId = Guid.NewGuid();
        Guid colonPasswordId = Guid.NewGuid();
        Guid ipv6Id = Guid.NewGuid();
        Guid uppercaseSchemeId = Guid.NewGuid();
        Guid usernameOnlyId = Guid.NewGuid();
        Guid passwordOnlyId = Guid.NewGuid();
        Guid emptyUserInfoId = Guid.NewGuid();
        Guid apiKeyConflictId = Guid.NewGuid();

        await using (DataContext migrate = contextFactory())
        {
            await migrate.Database.MigrateAsync();
        }

        await using (DataContext seed = contextFactory())
        {
            seed.DownloadClients.AddRange(
                NewClient(qbitId, "qbit", DownloadClientTypeName.qBittorrent, "http://quser:qpass@qbit-host/"),
                NewClient(delugeId, "deluge", DownloadClientTypeName.Deluge, "http://duser:dpass@deluge-host/"),
                NewClient(sabnzbdId, "sabnzbd", DownloadClientTypeName.Sabnzbd, "http://suser:spass@sab-host/"),
                NewClient(rTorrentId, "rtorrent", DownloadClientTypeName.rTorrent, "http://ruser:rpass@rtorrent-host/"),
                NewClient(transmissionId, "transmission", DownloadClientTypeName.Transmission, "http://tuser:tpass@transmission-host/"),
                NewClient(uTorrentNoCredsId, "utorrent-no-creds", DownloadClientTypeName.uTorrent, "http://uuser:upass@utorrent-host/"),
                NewClient(uTorrentExistingCredsId, "utorrent-existing-creds", DownloadClientTypeName.uTorrent, "http://xuser:xpass@utorrent-existing-host/", username: "existing", password: "existingpass"),
                NewClient(noUserInfoId, "no-userinfo", DownloadClientTypeName.qBittorrent, "http://plain-host/unchanged", username: "keepme"),
                NewClient(encodedId, "encoded", DownloadClientTypeName.qBittorrent, "http://u%3Ap:p%25q@encoded-host/a%20b?x=%2F"),
                NewClient(colonPasswordId, "colon-password", DownloadClientTypeName.qBittorrent, "http://u:p:q@colon-host/"),
                NewClient(ipv6Id, "ipv6", DownloadClientTypeName.qBittorrent, "http://user:pass@[::1]:8080/path/"),
                NewClient(uppercaseSchemeId, "uppercase-scheme", DownloadClientTypeName.qBittorrent, "HTTP://user:pass@upper-host:80/"),
                NewClient(usernameOnlyId, "username-only", DownloadClientTypeName.qBittorrent, "http://justuser@username-only-host/"),
                NewClient(passwordOnlyId, "password-only", DownloadClientTypeName.qBittorrent, "http://:justpass@password-only-host/"),
                NewClient(emptyUserInfoId, "empty-userinfo", DownloadClientTypeName.qBittorrent, "http://@empty-userinfo-host/"),
                NewClient(apiKeyConflictId, "api-key-conflict", DownloadClientTypeName.qBittorrent, "http://auser:apass@apikey-host/", apiKey: "secret-key"));

            await seed.SaveChangesAsync();
        }

        ILogger<DownloadClientUserInfoMigrator> logger = Substitute.For<ILogger<DownloadClientUserInfoMigrator>>();
        DownloadClientUserInfoMigrator migrator = new(logger);

        await using (DataContext run = contextFactory())
        {
            await migrator.MigrateAsync(run, CancellationToken.None);
        }

        await using (DataContext assert = contextFactory())
        {
            Dictionary<Guid, DownloadClientConfig> clients = await assert.DownloadClients
                .AsNoTracking()
                .ToDictionaryAsync(c => c.Id);

            DownloadClientConfig qbit = clients[qbitId];
            qbit.HttpAuthUsername.ShouldBe("quser");
            qbit.HttpAuthPassword.ShouldBe("qpass");
            qbit.HttpAuthType.ShouldBe(HttpAuthType.Basic);
            qbit.Username.ShouldBeNull();
            qbit.Password.ShouldBeNull();
            qbit.Host!.OriginalString.ShouldBe("http://qbit-host/");

            DownloadClientConfig deluge = clients[delugeId];
            deluge.HttpAuthUsername.ShouldBe("duser");
            deluge.HttpAuthPassword.ShouldBe("dpass");
            deluge.Host!.OriginalString.ShouldBe("http://deluge-host/");

            DownloadClientConfig sabnzbd = clients[sabnzbdId];
            sabnzbd.HttpAuthUsername.ShouldBe("suser");
            sabnzbd.HttpAuthPassword.ShouldBe("spass");
            sabnzbd.Host!.OriginalString.ShouldBe("http://sab-host/");

            DownloadClientConfig rTorrent = clients[rTorrentId];
            rTorrent.Username.ShouldBe("ruser");
            rTorrent.Password.ShouldBe("rpass");
            rTorrent.HttpAuthUsername.ShouldBeNull();
            rTorrent.Host!.OriginalString.ShouldBe("http://rtorrent-host/");

            DownloadClientConfig transmission = clients[transmissionId];
            transmission.Username.ShouldBe("tuser");
            transmission.Password.ShouldBe("tpass");
            transmission.Host!.OriginalString.ShouldBe("http://transmission-host/");

            DownloadClientConfig uTorrentNoCreds = clients[uTorrentNoCredsId];
            uTorrentNoCreds.Username.ShouldBe("uuser");
            uTorrentNoCreds.Password.ShouldBe("upass");
            uTorrentNoCreds.Host!.OriginalString.ShouldBe("http://utorrent-host/");

            // Existing creds win; only the userinfo goes.
            DownloadClientConfig uTorrentExistingCreds = clients[uTorrentExistingCredsId];
            uTorrentExistingCreds.Username.ShouldBe("existing");
            uTorrentExistingCreds.Password.ShouldBe("existingpass");
            uTorrentExistingCreds.Host!.OriginalString.ShouldBe("http://utorrent-existing-host/");

            DownloadClientConfig noUserInfo = clients[noUserInfoId];
            noUserInfo.Username.ShouldBe("keepme");
            noUserInfo.Host!.OriginalString.ShouldBe("http://plain-host/unchanged");

            DownloadClientConfig encoded = clients[encodedId];
            encoded.HttpAuthUsername.ShouldBe("u:p");
            encoded.HttpAuthPassword.ShouldBe("p%q");
            encoded.Host!.OriginalString.ShouldBe("http://encoded-host/a%20b?x=%2F");

            DownloadClientConfig colonPassword = clients[colonPasswordId];
            colonPassword.HttpAuthUsername.ShouldBe("u");
            colonPassword.HttpAuthPassword.ShouldBe("p:q");

            DownloadClientConfig ipv6 = clients[ipv6Id];
            ipv6.HttpAuthUsername.ShouldBe("user");
            ipv6.HttpAuthPassword.ShouldBe("pass");
            ipv6.Host!.OriginalString.ShouldBe("http://[::1]:8080/path/");

            DownloadClientConfig uppercaseScheme = clients[uppercaseSchemeId];
            uppercaseScheme.HttpAuthUsername.ShouldBe("user");
            uppercaseScheme.Host!.OriginalString.ShouldBe("HTTP://upper-host:80/");

            DownloadClientConfig usernameOnly = clients[usernameOnlyId];
            usernameOnly.HttpAuthUsername.ShouldBe("justuser");
            usernameOnly.HttpAuthPassword.ShouldBeNull();
            usernameOnly.Host!.OriginalString.ShouldBe("http://username-only-host/");

            DownloadClientConfig passwordOnly = clients[passwordOnlyId];
            passwordOnly.HttpAuthUsername.ShouldBeNull();
            passwordOnly.HttpAuthPassword.ShouldBe("justpass");
            passwordOnly.Host!.OriginalString.ShouldBe("http://password-only-host/");

            // Empty userinfo carries no credential, so the row stays as is.
            DownloadClientConfig emptyUserInfo = clients[emptyUserInfoId];
            emptyUserInfo.HttpAuthUsername.ShouldBeNull();
            emptyUserInfo.HttpAuthPassword.ShouldBeNull();
            emptyUserInfo.Host!.OriginalString.ShouldBe("http://@empty-userinfo-host/");

            // An API key conflicts with HTTP auth for qBittorrent, so only the userinfo goes.
            DownloadClientConfig apiKeyConflict = clients[apiKeyConflictId];
            apiKeyConflict.ApiKey.ShouldBe("secret-key");
            apiKeyConflict.HttpAuthUsername.ShouldBeNull();
            apiKeyConflict.HttpAuthPassword.ShouldBeNull();
            apiKeyConflict.Host!.OriginalString.ShouldBe("http://apikey-host/");
            Should.NotThrow(() => apiKeyConflict.Validate());
        }

        logger.Received(2).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        await using (DataContext secondRun = contextFactory())
        {
            await migrator.MigrateAsync(secondRun, CancellationToken.None);
        }

        await using (DataContext assertAgain = contextFactory())
        {
            DownloadClientConfig qbitAgain = await assertAgain.DownloadClients.AsNoTracking().SingleAsync(c => c.Id == qbitId);
            qbitAgain.HttpAuthUsername.ShouldBe("quser");
            qbitAgain.HttpAuthPassword.ShouldBe("qpass");
            qbitAgain.Host!.OriginalString.ShouldBe("http://qbit-host/");

            DownloadClientConfig uTorrentExistingCredsAgain = await assertAgain.DownloadClients
                .AsNoTracking()
                .SingleAsync(c => c.Id == uTorrentExistingCredsId);
            uTorrentExistingCredsAgain.Username.ShouldBe("existing");
            uTorrentExistingCredsAgain.Password.ShouldBe("existingpass");

            DownloadClientConfig apiKeyConflictAgain = await assertAgain.DownloadClients
                .AsNoTracking()
                .SingleAsync(c => c.Id == apiKeyConflictId);
            apiKeyConflictAgain.ApiKey.ShouldBe("secret-key");
            apiKeyConflictAgain.HttpAuthUsername.ShouldBeNull();
        }

        logger.Received(2).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private static DownloadClientConfig NewClient(
        Guid id,
        string name,
        DownloadClientTypeName typeName,
        string host,
        string? username = null,
        string? password = null,
        string? apiKey = null) => new()
    {
        Id = id,
        Name = name,
        TypeName = typeName,
        Type = typeName.ClientType(),
        Host = new Uri(host, UriKind.RelativeOrAbsolute),
        Username = username,
        Password = password,
        ApiKey = apiKey,
    };
}
