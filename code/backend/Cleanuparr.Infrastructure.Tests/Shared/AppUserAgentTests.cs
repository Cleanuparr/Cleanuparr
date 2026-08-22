using Cleanuparr.Shared.Helpers;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Shared;

public sealed class AppUserAgentTests
{
    [Theory]
    [InlineData(1, 2, 3, 4)]
    [InlineData(1, 2, 3, 0)]
    public void Format_ShouldDropTheRevision(int major, int minor, int build, int revision)
    {
        AppUserAgent.Format(new Version(major, minor, build, revision)).ShouldBe("Cleanuparr/1.2.3");
    }

    [Fact]
    public void Format_ShouldEmitTheBareProductToken_WhenTheVersionIsUnknown()
    {
        AppUserAgent.Format(null).ShouldBe("Cleanuparr");
    }
}
