using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class TrackerMessageClassifierTests
{
    [Theory]
    [InlineData("Unregistered torrent")]
    [InlineData("Torrent not found")]
    [InlineData("torrent nicht gefunden")]
    [InlineData("não registrado")]
    [InlineData("nem található")]
    [InlineData("Trumped: Internal")]
    public void Classify_UnregisteredMessages_ReturnsUnregistered(string message)
    {
        TrackerMessageClassifier.Classify(message).ShouldBe(TrackerHealth.Unregistered);
    }

    [Fact]
    public void Classify_UnregisteredReasonFollowedByUrl_MatchesReasonAndIgnoresUrlSlug()
    {
        TrackerMessageClassifier.Classify("Dupe: https://tracker.example/the-dead-zone-1983")
            .ShouldBe(TrackerHealth.Unregistered);

        TrackerMessageClassifier.Classify("Announce ok: https://tracker.example/the-dead-zone-1983")
            .ShouldBe(TrackerHealth.Inconclusive);
    }

    [Theory]
    [InlineData("Stream truncated")]
    [InlineData("truncated")]
    [InlineData("520 (Unknown HTTP Error)")]
    [InlineData("Tracker is down")]
    [InlineData("Connection timed out")]
    [InlineData("maintenance")]
    public void Classify_TrackerDownMessages_ReturnsInconclusive(string message)
    {
        TrackerMessageClassifier.Classify(message).ShouldBe(TrackerHealth.Inconclusive);
    }

    [Fact]
    public void Classify_PasskeyProblemPhrasedAsNotRegistered_ReturnsInconclusive()
    {
        TrackerMessageClassifier.Classify("Torrent not registered, your passkey is unauthorized")
            .ShouldBe(TrackerHealth.Inconclusive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<none>")]
    public void Classify_NullEmptyOrUnknownMessage_ReturnsInconclusive(string? message)
    {
        TrackerMessageClassifier.Classify(message).ShouldBe(TrackerHealth.Inconclusive);
    }

    [Theory]
    [InlineData("Announce failed for showdown-in-tokyo")]
    [InlineData("download failed")]
    [InlineData("deadpool announce error")]
    [InlineData("another tracker")]
    public void Classify_PatternInsideLongerWord_ReturnsInconclusive(string message)
    {
        TrackerMessageClassifier.Classify(message).ShouldBe(TrackerHealth.Inconclusive);
    }
}
