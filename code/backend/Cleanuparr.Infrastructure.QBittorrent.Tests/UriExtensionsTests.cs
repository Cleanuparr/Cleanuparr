using QBittorrent.Client.Extensions;
using Shouldly;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests;

public class UriExtensionsTests
{
    [Theory]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("http://example.com/", "http://example.com/")]
    [InlineData("http://example.com/a", "http://example.com:80/a/")]
    [InlineData("http://example.com/a/", "http://example.com/a/")]
    public void EnsureTrailingSlash(string original, string expected)
    {
        Uri originalUri = new(original);
        Uri actualUri = originalUri.EnsureTrailingSlash();

        actualUri.OriginalString.ShouldBe(expected);
        if (original == expected)
        {
            actualUri.ShouldBeSameAs(originalUri);
        }
    }

    [Fact]
    public void WithQueryParametersNull()
    {
        Uri uri = new("http://example.com/a/b/c/");
        uri.WithQueryParameters(null).ShouldBeSameAs(uri);
    }

    [Fact]
    public void WithQueryParametersEmpty()
    {
        Uri uri = new("http://example.com/a/b/c/");
        uri.WithQueryParameters().ShouldBeSameAs(uri);
    }

    [Fact]
    public void WithQueryParametersSingle()
    {
        Uri uri = new("http://example.com/a/b/c/");
        uri.WithQueryParameters(("testKey", "testValue")).AbsoluteUri
            .ShouldBe("http://example.com/a/b/c/?testKey=testValue");
    }

    [Fact]
    public void WithQueryParametersTwo()
    {
        Uri uri = new("http://example.com/a/b/c/");
        uri.WithQueryParameters(("testKey", "testValue"), ("test key", "test value")).AbsoluteUri
            .ShouldBe("http://example.com/a/b/c/?testKey=testValue&test%20key=test%20value");
    }
}
