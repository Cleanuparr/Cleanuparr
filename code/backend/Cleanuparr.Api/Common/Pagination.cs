namespace Cleanuparr.Api.Common;

public static class Pagination
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 500;

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
