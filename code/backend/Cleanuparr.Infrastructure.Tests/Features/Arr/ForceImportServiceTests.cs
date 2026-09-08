using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Domain.Entities.Arr.ManualImport;
using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events.Interfaces;
using Cleanuparr.Infrastructure.Features.Arr.ForceImport;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

public class ForceImportServiceTests
{
    /// <summary>One of the reasons the service treats as safe to force past.</summary>
    private const string SafeReason = "Unable to determine if file is a sample";

    private readonly IArrClient _arrClient;
    private readonly IStriker _striker;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMemoryCache _cache;
    private readonly ForceImportService _sut;
    private readonly ArrInstance _instance;

    public ForceImportServiceTests()
    {
        _arrClient = Substitute.For<IArrClient>();
        _striker = Substitute.For<IStriker>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new ForceImportService(Substitute.For<ILogger<ForceImportService>>(), _cache, _striker, _eventPublisher);

        _instance = new ArrInstance
        {
            Name = "sonarr",
            Url = new Uri("http://localhost:8989/"),
            ApiKey = "api-key",
            ArrConfig = new ArrConfig { Type = InstanceType.Sonarr },
        };

        _arrClient.SupportsForceImport.Returns(true);
        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>()).Returns([]);
        _arrClient.MapCandidate(Arg.Any<QueueRecord>(), Arg.Any<ManualImportCandidate>())
            .Returns(new ManualImportFile { Path = "/downloads/show.mkv", SeriesId = 7, EpisodeIds = [9] });

        SetConfig();
    }

    [Fact]
    public async Task TryImportAsync_ImportsAndResetsTheStrikes()
    {
        // Arrange: importBlocked is a settled state, so one run is enough
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Imported);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Is<List<ManualImportFile>>(files => files.Count == 1));
        await _striker.Received(1).ResetStrikeAsync(record.DownloadId, record.Title, StrikeType.FailedImport);
        await _eventPublisher.Received(1).PublishForceImported(record.Title, record.DownloadId, 1);
    }

    [Fact]
    public async Task TryImportAsync_Disabled_DoesNothing()
    {
        // Arrange
        SetConfig(forceImport: false);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_UnsupportedArr_DoesNothing()
    {
        // Arrange
        _arrClient.SupportsForceImport.Returns(false);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Theory]
    [InlineData("downloading", "warning")]
    [InlineData("importBlocked", "ok")]
    public async Task TryImportAsync_NotABlockedImport_DoesNothing(string state, string status)
    {
        // Arrange
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: state, status: status));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_AnUnmatchedMessage_DoesNothing()
    {
        // Arrange: one unsafe reason is enough to leave it alone
        QueueRecord record = BuildRecord(state: "importBlocked", messages: [SafeReason, "Not an upgrade for existing episode file"]);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_NoStatusMessage_DoesNothing()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked", messages: []);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
    }

    [Fact]
    public async Task TryImportAsync_TransitionalState_WaitsForASecondSighting()
    {
        // Arrange: the arr's own importer may still pick up an importPending download
        QueueRecord record = BuildRecord(state: "importPending");
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome firstRun = await _sut.TryImportAsync(_arrClient, _instance, record);
        ForceImportOutcome secondRun = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        firstRun.ShouldBe(ForceImportOutcome.Deferred);
        secondRun.ShouldBe(ForceImportOutcome.Imported);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_ImportCommandInFlight_Defers()
    {
        // Arrange: a manual import call during a move reads the files mid-move
        _arrClient.GetCommandsAsync(_instance).Returns([
            new ArrCommandStatus(1, ArrCommandState.Started, null, "RefreshMonitoredDownloads"),
        ]);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_UnrelatedCommandInFlight_Imports()
    {
        // Arrange
        _arrClient.GetCommandsAsync(_instance).Returns([
            new ArrCommandStatus(1, ArrCommandState.Started, null, "EpisodeSearch"),
        ]);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Imported);
    }

    [Fact]
    public async Task TryImportAsync_CommandListUnavailable_Defers()
    {
        // Arrange: without the command list there is no proof the arr is idle
        _arrClient.GetCommandsAsync(_instance).Returns<List<ArrCommandStatus>>(_ => throw new HttpRequestException("down"));
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
    }

    [Fact]
    public async Task TryImportAsync_NoCandidate_DoesNothing()
    {
        // Arrange
        StubCandidates();

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_UnexpectedRejection_ImportsNothing()
    {
        // Arrange: a rejection outside the safe list is a human's call
        StubCandidates(BuildCandidate(SafeReason, "Episode 1x02 was not found in the grabbed release"));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_MappingMismatch_ImportsNothing()
    {
        // Arrange: the file belongs to other content than the queue item
        _arrClient.MapCandidate(Arg.Any<QueueRecord>(), Arg.Any<ManualImportCandidate>()).Returns((ManualImportFile?)null);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_OneFileOfManyMapsElsewhere_ImportsNothing()
    {
        // Arrange
        ManualImportCandidate mapped = BuildCandidate(SafeReason);
        ManualImportCandidate unmapped = BuildCandidate(SafeReason);
        _arrClient.MapCandidate(Arg.Any<QueueRecord>(), unmapped).Returns((ManualImportFile?)null);
        StubCandidates(mapped, unmapped);

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_TheImportCallFails_Defers()
    {
        // Arrange: a failed call is transient, so the download waits instead of collecting a strike
        _arrClient.ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>())
            .Returns(Task.FromException(new HttpRequestException("boom")));
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task TryImportAsync_AlreadyAttempted_DoesNotAskTwice()
    {
        // Arrange: the arr needs time to work through the import it was given
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome firstRun = await _sut.TryImportAsync(_arrClient, _instance, record);
        ForceImportOutcome secondRun = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        firstRun.ShouldBe(ForceImportOutcome.Imported);
        secondRun.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_CandidatesUnavailable_Defers()
    {
        // Arrange: the arr may answer on the next run
        _arrClient.GetManualImportCandidatesAsync(Arg.Any<ArrInstance>(), Arg.Any<string>())
            .Returns<List<ManualImportCandidate>>(_ => throw new HttpRequestException("down"));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    private static void SetConfig(bool forceImport = true)
    {
        ContextProvider.Set(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig { ForceImport = forceImport },
        });
    }

    private void StubCandidates(params ManualImportCandidate[] candidates)
    {
        _arrClient.GetManualImportCandidatesAsync(Arg.Any<ArrInstance>(), Arg.Any<string>())
            .Returns(candidates.ToList());
    }

    private static ManualImportCandidate BuildCandidate(params string[] rejections) => new()
    {
        Path = "/downloads/show.mkv",
        RelativePath = "show.mkv",
        DownloadId = "HASH",
        Rejections = rejections.Select(reason => new ManualImportRejection { Reason = reason, Type = "permanent" }).ToList(),
    };

    private static QueueRecord BuildRecord(
        string state,
        string status = "warning",
        List<string>? messages = null
    ) => new()
    {
        Id = 1,
        Title = "Show.S01E01",
        DownloadId = "HASH",
        SeriesId = 7,
        EpisodeId = 9,
        Protocol = "torrent",
        Status = "completed",
        TrackedDownloadStatus = status,
        TrackedDownloadState = state,
        StatusMessages =
        [
            new TrackedDownloadStatusMessage { Title = "show.mkv", Messages = messages ?? [SafeReason] },
        ],
    };
}
