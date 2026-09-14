using Cleanuparr.Infrastructure.Helpers;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Helpers;

public class FileReaderTests
{
    [Theory]
    [InlineData("http://example.test/blocklist.txt")]
    [InlineData("https://cleanuparr.pages.dev/blacklist")]
    public void IsRemote_WithAnHttpUrl_ReturnsTrue(string path)
    {
        FileReader.IsRemote(path).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/config/blocklist.txt")]
    [InlineData("blocklist.txt")]
    [InlineData("./blocklist.txt")]
    [InlineData(@"C:\config\blocklist.txt")]
    [InlineData("file:///config/blocklist.txt")]
    [InlineData("ftp://example.test/blocklist.txt")]
    [InlineData("")]
    [InlineData(null)]
    public void IsRemote_WithAnythingElse_ReturnsFalse(string? path)
    {
        FileReader.IsRemote(path).ShouldBeFalse();
    }
}
