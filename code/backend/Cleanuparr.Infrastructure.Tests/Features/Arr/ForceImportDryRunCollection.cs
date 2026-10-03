using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Arr;

/// <summary>
/// ForceImportService keeps its dry-run eviction token in a static field.
/// Specs that cancel it run sequentially.
/// </summary>
[CollectionDefinition(Name)]
public class ForceImportDryRunCollection
{
    public const string Name = "ForceImportDryRun";
}
