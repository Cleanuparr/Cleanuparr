using System.Net;
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
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ClearExtensions;
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
    private readonly FakeTimeProvider _timeProvider;
    private readonly ForceImportService _sut;
    private readonly ArrInstance _instance;

    /// <summary>How many imports the arr has recorded, which is what its history reports.</summary>
    private int _importedByTheArr;

    public ForceImportServiceTests()
    {
        _arrClient = Substitute.For<IArrClient>();
        _striker = Substitute.For<IStriker>();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _timeProvider = new FakeTimeProvider();
        _sut = new ForceImportService(
            Substitute.For<ILogger<ForceImportService>>(), _cache, _striker, _eventPublisher, _timeProvider);

        _instance = new ArrInstance
        {
            Name = "sonarr",
            Url = new Uri("http://localhost:8989/"),
            ApiKey = "api-key",
            ArrConfig = new ArrConfig { Type = InstanceType.Sonarr },
        };

        _arrClient.ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>())
            .Returns(_ =>
            {
                _importedByTheArr++;
                return Task.CompletedTask;
            });

        _arrClient.SupportsForceImport.Returns(true);
        _arrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(true);
        _arrClient.GetCommandsAsync(Arg.Any<ArrInstance>()).Returns([]);
        // The arr records one import per asked file, unless a test says otherwise.
        _arrClient.GetImportedCountAsync(Arg.Any<ArrInstance>(), Arg.Any<string>())
            .Returns(_ => _importedByTheArr);
        _arrClient.MapCandidate(Arg.Any<QueueRecord>(), Arg.Any<ManualImportCandidate>())
            .Returns(new ManualImportFile { Path = "/downloads/show.mkv", SeriesId = 7, EpisodeIds = [9] });

        SetConfig();
    }

    [Fact]
    public async Task TryImportAsync_AsksTheArrAndSaysNothingYet()
    {
        // Arrange: importBlocked is a settled state, so one run is enough
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert: an accepted request is not an import, so nothing is announced yet
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Is<List<ManualImportFile>>(files => files.Count == 1));
        await _striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
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
    public async Task TryImportAsync_NoContentId_DoesNothing()
    {
        // Arrange: a zero content id would match a candidate carrying a zero id
        _arrClient.HasContentId(Arg.Any<QueueRecord>()).Returns(false);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().GetManualImportCandidatesAsync(Arg.Any<ArrInstance>(), Arg.Any<string>());
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_NoStatusMessage_DoesNothing()
    {
        // Arrange: the arr reports a warning record with nothing under it
        QueueRecord record = BuildRecord(state: "importBlocked", withStatusMessage: false);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_AStatusMessageWithoutReasons_DoesNothing()
    {
        // Arrange: the arr states a block on the whole download as a bare title
        QueueRecord record = BuildRecord(state: "importBlocked");
        record.StatusMessages!.Add(new TrackedDownloadStatusMessage
        {
            Title = "One or more episodes expected in this release were not imported or missing",
            Messages = [],
        });
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Theory]
    [InlineData("importPending")]
    [InlineData("importFailed")]
    public async Task TryImportAsync_TransitionalState_WaitsForASecondSighting(string state)
    {
        // Arrange: the arr's own importer may still pick up a transitional download
        QueueRecord record = BuildRecord(state: state);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome firstRun = await _sut.TryImportAsync(_arrClient, _instance, record);
        ForceImportOutcome secondRun = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        firstRun.ShouldBe(ForceImportOutcome.Deferred);
        secondRun.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Theory]
    [InlineData("RefreshMonitoredDownloads")]
    [InlineData("ManualImport")]
    [InlineData("RescanSeries")]
    [InlineData("RenameFiles")]
    [InlineData("RenameSeries")]
    [InlineData("RenameMovie")]
    public async Task TryImportAsync_AFileMovingCommandInFlight_Defers(string command)
    {
        // Arrange: a manual import call during a move reads the files mid-move
        _arrClient.GetCommandsAsync(_instance).Returns([
            new ArrCommandStatus(1, ArrCommandState.Started, null, command),
        ]);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_UnrelatedCommandInFlight_AsksTheArr()
    {
        // Arrange
        _arrClient.GetCommandsAsync(_instance).Returns([
            new ArrCommandStatus(1, ArrCommandState.Started, null, "EpisodeSearch"),
        ]);
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
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
    public async Task TryImportAsync_TheArrWasNotReached_Defers()
    {
        // Arrange: an unreachable arr is transient, so the download waits instead of collecting a strike
        _arrClient.ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>())
            .Returns(Task.FromException(new HttpRequestException("boom")));
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, BuildRecord(state: "importBlocked"));

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryImportAsync_TheArrWasNotReached_CostsNoTry()
    {
        // Arrange
        SetConfig(maxTries: 1);
        _arrClient.ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>())
            .Returns(Task.FromException(new HttpRequestException("down")));
        StubCandidates(BuildCandidate(SafeReason));
        QueueRecord record = BuildRecord(state: "importBlocked");

        // Act
        await _sut.TryImportAsync(_arrClient, _instance, record);
        await _sut.TryImportAsync(_arrClient, _instance, record);
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert: a request that never landed keeps the tries intact
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.Received(3).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_TheArrRefusedTheImport_SpendsTheTry()
    {
        // Arrange: an arr that keeps refusing must not hold the download out of the strike path forever
        SetConfig(maxTries: 1);
        _arrClient.ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>())
            .Returns(Task.FromException(new HttpRequestException("bad request", null, HttpStatusCode.BadRequest)));
        StubCandidates(BuildCandidate(SafeReason));
        QueueRecord record = BuildRecord(state: "importBlocked");

        // Act
        ForceImportOutcome first = await _sut.TryImportAsync(_arrClient, _instance, record);
        ForceImportOutcome second = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        first.ShouldBe(ForceImportOutcome.Deferred);
        second.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_TheArrRefusedALaterTry_StillReportsTheAcceptedOne()
    {
        // Arrange: try 1 lands, try 2 is refused
        SetConfig(maxTries: 2);
        StubCandidates(BuildCandidate(SafeReason));
        QueueRecord record = BuildRecord(state: "importBlocked");

        await _sut.TryImportAsync(_arrClient, _instance, record);

        _arrClient.ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>())
            .Returns(Task.FromException(new HttpRequestException("bad request", null, HttpStatusCode.BadRequest)));

        await _sut.TryImportAsync(_arrClient, _instance, record);

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert: the refused try leaves try 1's file count untouched
        await _eventPublisher.Received(1).PublishForceImported(record.Title, record.DownloadId);
    }

    [Fact]
    public async Task TryImportAsync_TheDownloadStaysBlocked_AsksAgainUpToTheLimit()
    {
        // Arrange
        SetConfig(maxTries: 2);
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        // Act
        ForceImportOutcome first = await _sut.TryImportAsync(_arrClient, _instance, record);
        ForceImportOutcome second = await _sut.TryImportAsync(_arrClient, _instance, record);
        ForceImportOutcome third = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert: the strike path takes over once the tries run out
        first.ShouldBe(ForceImportOutcome.Deferred);
        second.ShouldBe(ForceImportOutcome.Deferred);
        third.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.Received(2).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_OutOfTries_StaysOutOfTheWay()
    {
        // Arrange
        SetConfig(maxTries: 1);
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        await _sut.TryImportAsync(_arrClient, _instance, record);
        await _sut.TryImportAsync(_arrClient, _instance, record);

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.NotApplicable);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_ADeferredRunCostsNoTry()
    {
        // Arrange: the arr is busy, so nothing is asked of it
        SetConfig(maxTries: 1);
        _arrClient.GetCommandsAsync(_instance).Returns([
            new ArrCommandStatus(1, ArrCommandState.Started, null, "ManualImport"),
        ]);
        StubCandidates(BuildCandidate(SafeReason));
        QueueRecord record = BuildRecord(state: "importBlocked");

        await _sut.TryImportAsync(_arrClient, _instance, record);
        await _sut.TryImportAsync(_arrClient, _instance, record);

        _arrClient.GetCommandsAsync(_instance).Returns([]);

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.Received(1).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_TheGaveUpWindowPassed_TriesAgain()
    {
        // Arrange: whatever stopped the arr may be fixed by now
        SetConfig(maxTries: 1);
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        await _sut.TryImportAsync(_arrClient, _instance, record);
        (await _sut.TryImportAsync(_arrClient, _instance, record)).ShouldBe(ForceImportOutcome.NotApplicable);

        _timeProvider.Advance(TimeSpan.FromHours(6));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.Received(2).ForceImportAsync(_instance, Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task TryImportAsync_TheSightingWindowPassed_WaitsForASecondSightingAgain()
    {
        // Arrange: a sighting that old says nothing about the block in front of us
        QueueRecord record = BuildRecord(state: "importPending");
        StubCandidates(BuildCandidate(SafeReason));

        await _sut.TryImportAsync(_arrClient, _instance, record);

        _timeProvider.Advance(TimeSpan.FromHours(6));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public async Task ReconcileAsync_TheDownloadLeftTheQueue_ReportsTheImport()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _striker.Received(1).ResetStrikeAsync(record.DownloadId, record.Title, StrikeType.FailedImport);
        await _eventPublisher.Received(1).PublishForceImported(record.Title, record.DownloadId);
    }

    [Fact]
    public async Task ReconcileAsync_TheDownloadIsStillQueued_ReportsNothing()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string> { record.DownloadId });

        // Assert
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReconcileAsync_ReportsAnImportOnlyOnce()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _eventPublisher.Received(1).PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReconcileAsync_ThePublishFailed_ReportsItOnTheNextRun()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);

        _eventPublisher
            .PublishForceImported(record.Title, record.DownloadId)
            .Returns(Task.FromException(new Exception("the event went nowhere")));

        // Act
        await Should.ThrowAsync<Exception>(() => _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>()));

        _eventPublisher.ClearSubstitute(ClearOptions.ReturnValues);
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _eventPublisher.Received(2).PublishForceImported(record.Title, record.DownloadId);
    }

    [Fact]
    public async Task ReconcileAsync_TheLastTryLandedAfterGivingUp_ReportsTheImport()
    {
        // Arrange: the tries run out while the arr is still importing what it was last asked for
        SetConfig(maxTries: 1);
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        await _sut.TryImportAsync(_arrClient, _instance, record);
        _importedByTheArr = 0;

        ForceImportOutcome gaveUp = await _sut.TryImportAsync(_arrClient, _instance, record);
        _importedByTheArr = 1;

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        gaveUp.ShouldBe(ForceImportOutcome.NotApplicable);
        await _striker.Received(1).ResetStrikeAsync(record.DownloadId, record.Title, StrikeType.FailedImport);
        await _eventPublisher.Received(1).PublishForceImported(record.Title, record.DownloadId);
    }

    [Fact]
    public async Task TryImportAsync_AsksAgain_KeepsTheFirstBaseline()
    {
        // Arrange: the first request lands while the arr still reports the download as blocked
        SetConfig(maxTries: 3);
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));

        await _sut.TryImportAsync(_arrClient, _instance, record);

        // The arr imports nothing more, because the first request already covered the files.
        _arrClient.ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>())
            .Returns(Task.CompletedTask);

        await _sut.TryImportAsync(_arrClient, _instance, record);

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert: a baseline taken on the retry would have swallowed the first import
        await _eventPublisher.Received(1).PublishForceImported(record.Title, record.DownloadId);
    }

    [Fact]
    public async Task ReconcileAsync_TheArrRecordedNoImport_ReportsNothing()
    {
        // Arrange: the download left the queue without the arr importing it
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);
        _importedByTheArr = 0;

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReconcileAsync_AnOlderImportOfTheSameDownload_ReportsNothing()
    {
        // Arrange: the arr imported this download once before, and its history keeps that row
        _importedByTheArr = 1;
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);
        _importedByTheArr = 1;

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReconcileAsync_TheHistoryIsUnavailable_ReportsItOnALaterRun()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);

        _arrClient.GetImportedCountAsync(Arg.Any<ArrInstance>(), Arg.Any<string>())
            .Returns<int>(_ => throw new HttpRequestException("the arr is down"));

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        _arrClient.GetImportedCountAsync(Arg.Any<ArrInstance>(), Arg.Any<string>()).Returns(_ => _importedByTheArr);
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _eventPublisher.Received(1).PublishForceImported(record.Title, record.DownloadId);
    }

    [Fact]
    public async Task ReconcileAsync_TheImportNeverLanded_GivesUpOnIt()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);
        _importedByTheArr = 0;

        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());
        _timeProvider.Advance(TimeSpan.FromHours(6));

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert: the entry is gone, so an import recorded later is not read as this one
        _importedByTheArr = 1;
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryImportAsync_TheHistoryIsUnavailable_AsksForNothing()
    {
        // Arrange
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        _arrClient.GetImportedCountAsync(Arg.Any<ArrInstance>(), Arg.Any<string>())
            .Returns<int>(_ => throw new HttpRequestException("the arr is down"));

        // Act
        ForceImportOutcome outcome = await _sut.TryImportAsync(_arrClient, _instance, record);

        // Assert: a try is worth spending only on a request the arr received
        outcome.ShouldBe(ForceImportOutcome.Deferred);
        await _arrClient.DidNotReceive().ForceImportAsync(Arg.Any<ArrInstance>(), Arg.Any<List<ManualImportFile>>());
    }

    [Fact]
    public void Forget_NothingIsPending_DoesNothing()
    {
        Should.NotThrow(() => _sut.Forget(_instance, "HASH"));
    }

    [Fact]
    public async Task ReconcileAsync_CleanuparrRemovedTheDownloadMidRun_ReportsNothing()
    {
        // Arrange: another job takes the download out while the history call is in flight
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);

        _arrClient.GetImportedCountAsync(Arg.Any<ArrInstance>(), Arg.Any<string>())
            .Returns(_ =>
            {
                _sut.Forget(_instance, record.DownloadId);
                return _importedByTheArr;
            });

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _striker.DidNotReceive().ResetStrikeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StrikeType>());
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReconcileAsync_CleanuparrRemovedTheDownload_ReportsNothing()
    {
        // Arrange: the download left the queue because Cleanuparr took it out
        QueueRecord record = BuildRecord(state: "importBlocked");
        StubCandidates(BuildCandidate(SafeReason));
        await _sut.TryImportAsync(_arrClient, _instance, record);
        _sut.Forget(_instance, record.DownloadId);

        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReconcileAsync_NothingPending_ReportsNothing()
    {
        // Act
        await _sut.ReconcileAsync(_arrClient, _instance, new HashSet<string>());

        // Assert
        await _eventPublisher.DidNotReceive().PublishForceImported(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryImportAsync_CandidatesUnreachable_Defers()
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

    private static void SetConfig(bool forceImport = true, ushort maxTries = 3)
    {
        ContextProvider.Set(new QueueCleanerConfig
        {
            FailedImport = new FailedImportConfig { ForceImport = forceImport, ForceImportMaxTries = maxTries },
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
        List<string>? messages = null,
        bool withStatusMessage = true
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
        StatusMessages = withStatusMessage
            ?
            [
                new TrackedDownloadStatusMessage { Title = "show.mkv", Messages = messages ?? [SafeReason] },
            ]
            : [],
    };
}
