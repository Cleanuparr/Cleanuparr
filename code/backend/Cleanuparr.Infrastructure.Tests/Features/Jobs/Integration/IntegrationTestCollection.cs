using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.Integration;

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "JobHandlerIntegration";
}
