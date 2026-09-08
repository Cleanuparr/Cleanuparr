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

    [Theory]
    [InlineData("Dead")]
    [InlineData("Nuked: bad encode")]
    [InlineData("Uploaded")]
    [InlineData("Upgraded")]
    [InlineData("Trumped: Internal: https://tracker.example/x")]
    [InlineData("Dupe: https://tracker.example/the-dead-zone-1983")]
    [InlineData("Season pack: https://tracker.example/show-s01")]
    public void Classify_BareReasonCode_ReturnsUnregistered(string message)
    {
        TrackerMessageClassifier.Classify(message).ShouldBe(TrackerHealth.Unregistered);
    }

    [Theory]
    [InlineData("Please use the other tracker")]
    [InlineData("You have not uploaded enough")]
    [InlineData("tracker is dead")]
    [InlineData("Torrent uploaded by another user, see the other tracker")]
    [InlineData("Please seed: uploaded ratio too low")]
    public void Classify_ReasonWordOutsideLeadingSegment_ReturnsInconclusive(string message)
    {
        TrackerMessageClassifier.Classify(message).ShouldBe(TrackerHealth.Inconclusive);
    }

    [Fact]
    public void Classify_SpecificPhraseAfterLeadingSegment_ReturnsUnregistered()
    {
        TrackerMessageClassifier.Classify("Announce failed: torrent not found")
            .ShouldBe(TrackerHealth.Unregistered);
    }
}
