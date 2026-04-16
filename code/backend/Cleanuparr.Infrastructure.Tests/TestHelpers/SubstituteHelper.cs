using NSubstitute.Core;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Helper to clear leaked NSubstitute argument specifications from the thread-local context.
/// This prevents AmbiguousArgumentsException when xUnit constructs multiple fixtures on the same thread.
/// </summary>
/// <remarks>
/// Uses <see cref="IThreadLocalContext.DequeueAllArgumentSpecifications"/> which is part of the
/// NSubstitute.Core public interface surface. Verified compatible with NSubstitute 5.3.0.
/// If a future NSubstitute upgrade removes or renames this method, a compilation error will surface
/// immediately — update this helper accordingly.
/// </remarks>
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
