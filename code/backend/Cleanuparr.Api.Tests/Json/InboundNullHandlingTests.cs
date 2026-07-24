using System.Text.Json;
using Cleanuparr.Api.Json;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Json;

public class InboundNullHandlingTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new();
        CleanuparrJsonConfiguration.ConfigureApiInbound(options);
        return options;
    }

    private sealed class Model
    {
        public List<string> Items { get; set; } = ["default"];

        public string Name { get; set; } = "default-name";

        public string? Optional { get; set; } = "opt";
    }

    [Fact]
    public void ExplicitNull_OnNonNullableProperty_KeepsDefault()
    {
        Model result = JsonSerializer.Deserialize<Model>("""{"items": null, "name": null}""", Options)!;

        result.Items.ShouldBe(["default"]);
        result.Name.ShouldBe("default-name");
    }

    [Fact]
    public void ExplicitNull_MatchesAbsent()
    {
        Model withNull = JsonSerializer.Deserialize<Model>("""{"items": null, "name": null}""", Options)!;
        Model absent = JsonSerializer.Deserialize<Model>("{}", Options)!;

        withNull.Items.ShouldBe(absent.Items);
        withNull.Name.ShouldBe(absent.Name);
    }

    [Fact]
    public void ExplicitNull_OnNullableProperty_SetsNull()
    {
        Model result = JsonSerializer.Deserialize<Model>("""{"optional": null}""", Options)!;

        result.Optional.ShouldBeNull();
    }

    [Fact]
    public void NonNullValue_StillDeserializes()
    {
        Model result = JsonSerializer.Deserialize<Model>("""{"items": ["a","b"], "name": "x"}""", Options)!;

        result.Items.ShouldBe(["a", "b"]);
        result.Name.ShouldBe("x");
    }
}
