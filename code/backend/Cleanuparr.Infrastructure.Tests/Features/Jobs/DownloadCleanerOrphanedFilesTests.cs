using Cleanuparr.Domain.Entities;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Cleanuparr.Persistence.Models.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs;

[Collection(JobHandlerCollection.Name)]
public sealed class DownloadCleanerOrphanedFilesTests : IDisposable
{
    private readonly JobHandlerFixture _fixture;
    private readonly ILogger<DownloadCleaner> _logger;
    private readonly string _tempRoot;

    public DownloadCleanerOrphanedFilesTests(JobHandlerFixture fixture)
    {
        _fixture = fixture;
        _fixture.RecreateDataContext();
        _fixture.ResetMocks();
        _logger = _fixture.CreateLogger<DownloadCleaner>();
        _tempRoot = Path.Combine(Path.GetTempPath(), "cleanuparr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _fixture.DryRunInterceptor.When(x => x.Intercept(Arg.Any<Action>(), Arg.Any<string?>()))
            .Do(ci => ((Action)ci.Args()[0]).Invoke());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private DownloadCleaner CreateSut() => new(
        _logger,
        _fixture.DataContext,
        _fixture.Cache,
        _fixture.MessageBus,
        _fixture.ArrClientFactory,
        _fixture.ArrQueueIterator,
        _fixture.DownloadServiceFactory,
        _fixture.EventPublisher,
        _fixture.TimeProvider,
        _fixture.SeedingRulesService,
        _fixture.UnlinkedService,
        _fixture.OrphanedFilesService);

    private async Task ExecuteWithTimeAdvance(DownloadCleaner sut)
    {
        var task = sut.ExecuteAsync();
        _fixture.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await task;
    }

    private static ITorrentItemWrapper MakeTorrent(string name, string savePath)
    {
        var t = Substitute.For<ITorrentItemWrapper>();
        t.Name.Returns(name);
        t.SavePath.Returns(savePath);
        return t;
    }

    // A torrent whose save path lies outside the test's scan dir, so it
    // contributes to "client has torrents" without claiming any test files.
    private ITorrentItemWrapper DecoyTorrent() => MakeTorrent("decoy", _tempRoot);

    private IDownloadService SetupDownloadService(DownloadClientConfig clientConfig, List<ITorrentItemWrapper> torrents)
    {
        var svc = Substitute.For<IDownloadService>();
        svc.ClientConfig.Returns(clientConfig);
        svc.LoginAsync().Returns(Task.CompletedTask);
        svc.GetSeedingDownloads().Returns([]);
        svc.GetAllTorrentsLite().Returns(torrents);
        _fixture.DownloadServiceFactory.GetDownloadService(clientConfig).Returns(svc);
        return svc;
    }

    [Fact]
    public async Task OrphanedFiles_NoEnabledClientConfigs_SkipsScan()
    {
        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        var dbClient = _fixture.DataContext.DownloadClients.First();
        SetupDownloadService(dbClient, []);

        var sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        _fixture.OrphanedFilesLogger.ReceivedLogContaining(LogLevel.Debug, "No orphaned files settings have been configured");
    }

    [Fact]
    public async Task OrphanedFiles_OrphanedEntry_IsMovedWhenOrphanedDirectorySet()
    {
        var scanDir = Path.Combine(_tempRoot, "downloads");
        var orphanedDir = Path.Combine(_tempRoot, "orphaned");
        Directory.CreateDirectory(scanDir);
        var orphanedFile = Path.Combine(scanDir, "orphan.mkv");
        File.WriteAllText(orphanedFile, "x");

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        var dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: orphanedDir);

        SetupDownloadService(dbClient, [DecoyTorrent()]);

        var sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        File.Exists(orphanedFile).ShouldBeFalse();
        Directory.GetFiles(orphanedDir).ShouldContain(f => Path.GetFileName(f) == "orphan.mkv");
    }

    [Fact]
    public async Task OrphanedFiles_TorrentClaimedEntry_IsNotMoved()
    {
        var scanDir = Path.Combine(_tempRoot, "downloads");
        Directory.CreateDirectory(scanDir);
        var claimedDir = Path.Combine(scanDir, "claimed-show");
        Directory.CreateDirectory(claimedDir);

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        var dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: Path.Combine(_tempRoot, "orphaned"));

        var torrent = MakeTorrent("claimed-show", scanDir);
        SetupDownloadService(dbClient, [torrent]);

        var sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        Directory.Exists(claimedDir).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanedFiles_EntryMatchingExcludePattern_IsNotMoved()
    {
        var scanDir = Path.Combine(_tempRoot, "downloads");
        Directory.CreateDirectory(scanDir);
        var skipped = Path.Combine(scanDir, "stuff.nfo");
        File.WriteAllText(skipped, "metadata");

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        var dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: Path.Combine(_tempRoot, "orphaned"),
            excludePatterns: ["*.nfo"]);

        SetupDownloadService(dbClient, [DecoyTorrent()]);

        var sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        File.Exists(skipped).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanedFiles_EntryYoungerThanMinFileAgeHours_IsNotMoved()
    {
        var scanDir = Path.Combine(_tempRoot, "downloads");
        Directory.CreateDirectory(scanDir);
        var fresh = Path.Combine(scanDir, "fresh.mkv");
        File.WriteAllText(fresh, "x");

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        var dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: Path.Combine(_tempRoot, "orphaned"),
            minFileAgeHours: 1);

        SetupDownloadService(dbClient, [DecoyTorrent()]);

        var sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        File.Exists(fresh).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanedFiles_NameCollisionInOrphanedDirectory_AppendsTimestampSuffix()
    {
        var scanDir = Path.Combine(_tempRoot, "downloads");
        var orphanedDir = Path.Combine(_tempRoot, "orphaned");
        Directory.CreateDirectory(scanDir);
        Directory.CreateDirectory(orphanedDir);
        File.WriteAllText(Path.Combine(orphanedDir, "dupe.mkv"), "existing");
        File.WriteAllText(Path.Combine(scanDir, "dupe.mkv"), "new");

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        var dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: orphanedDir);

        SetupDownloadService(dbClient, [DecoyTorrent()]);

        var sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        var files = Directory.GetFiles(orphanedDir).Select(Path.GetFileName).ToList();
        files.ShouldContain("dupe.mkv");
        files.Count(f => f!.StartsWith("dupe.mkv_")).ShouldBe(1);
    }

    [Fact]
    public async Task OrphanedFiles_OrphanedDirectorySelfReference_IsNeverFlagged()
    {
        var scanDir = Path.Combine(_tempRoot, "downloads");
        var orphanedDir = Path.Combine(scanDir, "orphaned");
        Directory.CreateDirectory(orphanedDir);

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        var dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: orphanedDir);

        SetupDownloadService(dbClient, [DecoyTorrent()]);

        var sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        Directory.Exists(orphanedDir).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanedFiles_DownloadClientThrowsOnGetAllTorrentsLite_ScanIsSkipped()
    {
        string scanDir = Path.Combine(_tempRoot, "downloads");
        string orphanedDir = Path.Combine(_tempRoot, "orphaned");
        Directory.CreateDirectory(scanDir);
        string fileThatWouldBeMoved = Path.Combine(scanDir, "would-be-orphan.mkv");
        File.WriteAllText(fileThatWouldBeMoved, "x");

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        DownloadClientConfig dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: orphanedDir);

        IDownloadService svc = Substitute.For<IDownloadService>();
        svc.ClientConfig.Returns(dbClient);
        svc.LoginAsync().Returns(Task.CompletedTask);
        svc.GetSeedingDownloads().Returns([]);
        svc.GetAllTorrentsLite().ThrowsAsync(new HttpRequestException("connection refused"));
        _fixture.DownloadServiceFactory.GetDownloadService(dbClient).Returns(svc);

        DownloadCleaner sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        File.Exists(fileThatWouldBeMoved).ShouldBeTrue();
        (Directory.Exists(orphanedDir) && Directory.GetFiles(orphanedDir).Length > 0).ShouldBeFalse();
        _fixture.OrphanedFilesLogger.ReceivedLogContainingAtLeastOnce(LogLevel.Error, "Failed to get torrents");
        _fixture.OrphanedFilesLogger.ReceivedLogContainingAtLeastOnce(LogLevel.Warning, "torrents are unavailable or empty");
    }

    [Fact]
    public async Task OrphanedFiles_DownloadClientReturnsZeroTorrents_ScanIsSkipped()
    {
        string scanDir = Path.Combine(_tempRoot, "downloads");
        string orphanedDir = Path.Combine(_tempRoot, "orphaned");
        Directory.CreateDirectory(scanDir);
        string fileThatWouldBeMoved = Path.Combine(scanDir, "would-be-orphan.mkv");
        File.WriteAllText(fileThatWouldBeMoved, "x");

        TestDataContextFactory.AddDownloadClient(_fixture.DataContext);
        DownloadClientConfig dbClient = _fixture.DataContext.DownloadClients.First();
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, dbClient,
            scanDirectories: [scanDir],
            orphanedDirectory: orphanedDir);

        SetupDownloadService(dbClient, []);

        DownloadCleaner sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        File.Exists(fileThatWouldBeMoved).ShouldBeTrue();
        (Directory.Exists(orphanedDir) && Directory.GetFiles(orphanedDir).Length > 0).ShouldBeFalse();
        _fixture.OrphanedFilesLogger.ReceivedLogContainingAtLeastOnce(LogLevel.Debug, "No torrents found");
        _fixture.OrphanedFilesLogger.ReceivedLogContainingAtLeastOnce(LogLevel.Warning, "torrents are unavailable or empty");
    }

    [Fact]
    public async Task OrphanedFiles_OneClientFails_OtherClientStillProcessed()
    {
        string scanDirA = Path.Combine(_tempRoot, "downloads-a");
        string orphanedDirA = Path.Combine(_tempRoot, "orphaned-a");
        string scanDirB = Path.Combine(_tempRoot, "downloads-b");
        string orphanedDirB = Path.Combine(_tempRoot, "orphaned-b");
        Directory.CreateDirectory(scanDirA);
        Directory.CreateDirectory(scanDirB);
        string fileInA = Path.Combine(scanDirA, "a-orphan.mkv");
        string fileInB = Path.Combine(scanDirB, "b-orphan.mkv");
        File.WriteAllText(fileInA, "x");
        File.WriteAllText(fileInB, "x");

        DownloadClientConfig clientA = TestDataContextFactory.AddDownloadClient(_fixture.DataContext, name: "Client A");
        DownloadClientConfig clientB = TestDataContextFactory.AddDownloadClient(_fixture.DataContext, name: "Client B");
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, clientA,
            scanDirectories: [scanDirA],
            orphanedDirectory: orphanedDirA);
        TestDataContextFactory.AddOrphanedFilesConfig(
            _fixture.DataContext, clientB,
            scanDirectories: [scanDirB],
            orphanedDirectory: orphanedDirB);

        IDownloadService svcA = Substitute.For<IDownloadService>();
        svcA.ClientConfig.Returns(clientA);
        svcA.LoginAsync().Returns(Task.CompletedTask);
        svcA.GetSeedingDownloads().Returns([]);
        svcA.GetAllTorrentsLite().ThrowsAsync(new HttpRequestException("connection refused"));
        _fixture.DownloadServiceFactory.GetDownloadService(clientA).Returns(svcA);

        SetupDownloadService(clientB, [DecoyTorrent()]);

        DownloadCleaner sut = CreateSut();
        await ExecuteWithTimeAdvance(sut);

        File.Exists(fileInA).ShouldBeTrue();
        File.Exists(fileInB).ShouldBeFalse();
        Directory.GetFiles(orphanedDirB).ShouldContain(f => Path.GetFileName(f) == "b-orphan.mkv");
    }
}
