using System.Globalization;
using QBittorrent.Client.Extensions;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class MultipartFormDataContentExtensionsTests
{
    [Fact]
    public async Task AddValue_Double_UsesInvariantCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            using MultipartFormDataContent content = new();
            content.AddValue("ratio", 1.5);
            content.AddNotNullValue("limit", (double?)2.5);

            string body = await content.ReadAsStringAsync();

            body.ShouldContain("1.5");
            body.ShouldContain("2.5");
            body.ShouldNotContain("1,5");
            body.ShouldNotContain("2,5");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void AddFile_WhenAddFails_ReleasesFileHandle()
    {
        string path = Path.GetTempFileName();
        try
        {
            using MultipartFormDataContent content = new();

            Should.Throw<ArgumentException>(() => content.AddFile("", path));

            using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
