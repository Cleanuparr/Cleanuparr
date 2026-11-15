namespace Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body);
