using Cleanuparr.Infrastructure.Helpers;
using NSubstitute;
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

    [Fact]
    public void ToLocalPath_WithAFileUri_ReturnsThePathItPointsAt()
    {
        FileReader.ToLocalPath("file:///config/blocklist.txt").ShouldBe("/config/blocklist.txt");
    }

    [Fact]
    public void ToLocalPath_WithAnEncodedFileUri_Decodes()
    {
        FileReader.ToLocalPath("file:///config/my%20blocklist.txt").ShouldBe("/config/my blocklist.txt");
    }

    [Theory]
    [InlineData("/config/blocklist.txt")]
    [InlineData("blocklist.txt")]
    [InlineData("./blocklist.txt")]
    public void ToLocalPath_WithAPlainPath_LeavesItAlone(string path)
    {
        FileReader.ToLocalPath(path).ShouldBe(path);
    }

    [Fact]
    public async Task ReadContentAsync_WithAFileUri_ReadsTheFile()
    {
        string file = Path.Combine(Path.GetTempPath(), $"cleanuparr-filereader-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(file, "first-pattern");

        try
        {
            FileReader reader = new(Substitute.For<IHttpClientFactory>());

            string[] lines = await reader.ReadContentAsync(new Uri(file).AbsoluteUri);

            lines.ShouldBe(["first-pattern"]);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
