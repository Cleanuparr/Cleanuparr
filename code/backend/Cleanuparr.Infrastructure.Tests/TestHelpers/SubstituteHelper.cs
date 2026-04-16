using NSubstitute.Core;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Helper to clear leaked NSubstitute argument specifications from the thread-local context.
/// This prevents AmbiguousArgumentsException when xUnit constructs multiple fixtures on the same thread.
/// </summary>
public static class SubstituteHelper
{
    /// <summary>
    /// Clears any pending argument specifications that may have leaked from other
    /// fixture constructors running on the same thread.
    /// Call this at the start of fixture constructors before any NSubstitute setup.
    /// </summary>
    public static void ClearPendingArgSpecs()
    {
        SubstitutionContext.Current.ThreadContext.DequeueAllArgumentSpecifications();
    }
}
