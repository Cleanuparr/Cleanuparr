namespace Cleanuparr.Api.Features.Webhooks.Contracts;

/// <summary>
/// The *arr webhook event types Cleanuparr acts on. Unrecognized events parse to
/// <see cref="Unknown"/> and are ignored.
/// </summary>
public enum ArrWebhookEventType
{
    Unknown = 0,
    Test,
    Grab,
}
