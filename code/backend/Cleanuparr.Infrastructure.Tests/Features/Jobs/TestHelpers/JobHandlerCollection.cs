using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;

/// <summary>
/// Collection definition for job handler tests that share <see cref="JobHandlerFixture"/>.
/// Tests in this collection run sequentially to avoid FakeTimeProvider interference.
/// </summary>
[CollectionDefinition(Name)]
public class JobHandlerCollection : ICollectionFixture<JobHandlerFixture>
{
    public const string Name = "JobHandler";
}
