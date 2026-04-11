using Cleanuparr.Persistence.Converters;
using Shouldly;
using Xunit;

namespace Cleanuparr.Persistence.Tests.Converters;

public class JsonStringListConverterTests
{
    private readonly JsonStringListConverter _converter = new();

    [Fact]
    public void ConvertToProvider_WithValues_ReturnsJsonArray()
    {
        var list = new List<string> { "movies", "tv", "music" };

        var result = _converter.ConvertToProviderExpression.Compile()(list);

        result.ShouldBe("[\"movies\",\"tv\",\"music\"]");
    }

    [Fact]
    public void ConvertToProvider_EmptyList_ReturnsEmptyJsonArray()
    {
        var list = new List<string>();

        var result = _converter.ConvertToProviderExpression.Compile()(list);

        result.ShouldBe("[]");
    }

    [Fact]
    public void ConvertFromProvider_ValidJson_ReturnsList()
    {
        var json = "[\"movies\",\"tv\"]";

        var result = _converter.ConvertFromProviderExpression.Compile()(json);

        result.ShouldBe(new List<string> { "movies", "tv" });
    }

    [Fact]
    public void ConvertFromProvider_EmptyArray_ReturnsEmptyList()
    {
        var json = "[]";

        var result = _converter.ConvertFromProviderExpression.Compile()(json);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var original = new List<string> { "tracker.example.com", "private.org" };

        var json = _converter.ConvertToProviderExpression.Compile()(original);
        var restored = _converter.ConvertFromProviderExpression.Compile()(json);

        restored.ShouldBe(original);
    }
}
