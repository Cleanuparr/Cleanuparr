namespace Cleanuparr.Api.Tests;

/// <summary>
/// Auth integration tests share the file-system config directory (users.db via
/// SetupGuardMiddleware.CreateStaticInstance). Grouping them in one collection
/// forces sequential execution and prevents inter-factory interference.
/// </summary>
[CollectionDefinition("Auth Integration Tests")]
public class AuthIntegrationTestsCollection { }
