using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Api.Features.QueueCleaner.Contracts.Responses;

public abstract record QueueRuleResponse
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public bool Enabled { get; init; }

    public int MaxStrikes { get; init; }

    public TorrentPrivacyType PrivacyType { get; init; }

    public ushort MinCompletionPercentage { get; init; }

    public ushort MaxCompletionPercentage { get; init; }

    public bool DeletePrivateTorrentsFromClient { get; init; }

    public bool ChangeCategory { get; init; }
}
