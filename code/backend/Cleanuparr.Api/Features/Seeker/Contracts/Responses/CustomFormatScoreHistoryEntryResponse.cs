namespace Cleanuparr.Api.Features.Seeker.Contracts.Responses;

public sealed record CustomFormatScoreHistoryEntryResponse
{
    public int Score { get; init; }
    public int CutoffScore { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
