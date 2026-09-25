using Cleanuparr.Api.Common;
using Shouldly;

namespace Cleanuparr.Api.Tests.Common;

public class PaginationTests
{
    [Theory]
    [InlineData(0, 50)]
    [InlineData(-5, 50)]
    [InlineData(501, 500)]
    [InlineData(int.MaxValue, 500)]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(500, 500)]
    public void NormalizePageSize_ClampsToTheAllowedRange(int pageSize, int expected)
    {
        Pagination.NormalizePageSize(pageSize).ShouldBe(expected);
    }
}
