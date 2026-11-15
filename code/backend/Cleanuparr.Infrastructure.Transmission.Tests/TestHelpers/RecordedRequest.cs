namespace Cleanuparr.Infrastructure.Transmission.Tests.TestHelpers;

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body, string? SessionId, string? Authorization);
