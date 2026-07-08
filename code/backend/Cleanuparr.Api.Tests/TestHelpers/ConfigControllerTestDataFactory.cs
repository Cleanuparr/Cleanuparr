using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.BlacklistSync;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.MalwareBlocker;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Cleanuparr.Api.Tests.TestHelpers;

/// <summary>
/// Shared SQLite in-memory factory for controller tests that need a populated DataContext.
/// Seeds one row per config table so first-or-default reads succeed.
/// </summary>
public static class ConfigControllerTestDataFactory
{
    public static DataContext CreateDataContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connection)
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention()
            .Options;

        var context = new DataContext(options);
        context.Database.EnsureCreated();

        SeedDefaults(context);
        return context;
    }

    public static EventsContext CreateEventsContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<EventsContext>()
            .UseSqlite(connection)
            .UseLowerCaseNamingConvention()
            .UseSnakeCaseNamingConvention()
            .Options;

        var context = new EventsContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static void ConfigureProblemDetails(ControllerBase controller)
    {
        ProblemDetailsFactory factory = Substitute.For<ProblemDetailsFactory>();
        factory
            .CreateProblemDetails(
                Arg.Any<HttpContext>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>())
            .Returns(call => new ProblemDetails
            {
                Status = call.ArgAt<int?>(1),
                Title = call.ArgAt<string?>(2),
                Detail = call.ArgAt<string?>(4),
            });

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.ProblemDetailsFactory = factory;
    }

    private static void SeedDefaults(DataContext context)
    {
        context.GeneralConfigs.Add(new GeneralConfig
        {
            Id = Guid.NewGuid(),
            DryRun = false,
            IgnoredDownloads = [],
            Log = new LoggingConfig(),
        });

        context.ArrConfigs.AddRange(
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Sonarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Radarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Lidarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Readarr, Instances = [], FailedImportMaxStrikes = 3 },
            new ArrConfig { Id = Guid.NewGuid(), Type = InstanceType.Whisparr, Instances = [], FailedImportMaxStrikes = 3 }
        );

        context.QueueCleanerConfigs.Add(new QueueCleanerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
            FailedImport = new FailedImportConfig(),
        });

        context.ContentBlockerConfigs.Add(new ContentBlockerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
            DeletePrivate = false,
            Sonarr = new BlocklistSettings { Enabled = false },
            Radarr = new BlocklistSettings { Enabled = false },
            Lidarr = new BlocklistSettings { Enabled = false },
            Readarr = new BlocklistSettings { Enabled = false },
            Whisparr = new BlocklistSettings { Enabled = false },
        });

        context.DownloadCleanerConfigs.Add(new DownloadCleanerConfig
        {
            Id = Guid.NewGuid(),
            IgnoredDownloads = [],
        });

        context.BlacklistSyncConfigs.Add(new BlacklistSyncConfig
        {
            Id = Guid.NewGuid(),
            Enabled = false,
            CronExpression = "0 0 * * * ?",
        });

        context.SaveChanges();
    }
}
