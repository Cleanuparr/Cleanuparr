namespace Cleanuparr.Api.Common;

/// <summary>
/// Page size limits for paginated endpoints.
/// </summary>
public static class Pagination
{
    /// <summary>
    /// Page size used when the request asks for less than 1.
    /// </summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Largest page size a request can get.
    /// </summary>
    public const int MaxPageSize = 500;

    /// <summary>
    /// Clamps a requested page size.
    /// Below 1 returns <see cref="DefaultPageSize"/>; above <see cref="MaxPageSize"/> returns <see cref="MaxPageSize"/>.
    /// </summary>
    /// <param name="pageSize">Page size from the request.</param>
    /// <returns>The page size to query with.</returns>
    public static int NormalizePageSize(int pageSize)
    {
        if (pageSize < 1)
        {
            return DefaultPageSize;
        }

        if (pageSize > MaxPageSize)
        {
            return MaxPageSize;
        }

        return pageSize;
    }
}
