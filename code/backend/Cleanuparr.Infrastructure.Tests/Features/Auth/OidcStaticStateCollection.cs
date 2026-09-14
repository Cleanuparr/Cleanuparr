using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Auth;

/// <summary>
/// OidcAuthService keeps pending flows and one-time codes in static dictionaries.
/// Specs that touch them run sequentially.
/// </summary>
[CollectionDefinition(Name)]
public class OidcStaticStateCollection
{
    public const string Name = "OidcStaticState";
}
