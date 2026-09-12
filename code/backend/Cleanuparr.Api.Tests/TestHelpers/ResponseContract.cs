using System.Text.Json;
using Cleanuparr.Api.Json;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Tests.TestHelpers;

/// <summary>
/// Serializes a controller result through the API's own JSON options.
/// </summary>
public static class ResponseContract
{
    private static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        CleanuparrJsonConfiguration.ConfigureApiInbound(options);
        return options;
    }

    /// <summary>
    /// Serializes the value carried by an <see cref="ObjectResult"/> and parses it back.
    /// </summary>
    public static JsonElement Body(IActionResult result)
    {
        if (result is not ObjectResult objectResult)
        {
            throw new InvalidOperationException($"Expected an ObjectResult, got {result.GetType().Name}.");
        }

        if (objectResult.Value is null)
        {
            throw new InvalidOperationException("The result carries no value.");
        }

        string json = JsonSerializer.Serialize(objectResult.Value, objectResult.Value.GetType(), Options);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Serializes a payload that never travels through a controller, such as a hub message.
    /// </summary>
    public static JsonElement Payload(object value)
    {
        string json = JsonSerializer.Serialize(value, value.GetType(), Options);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Top-level property names, sorted so assertions read in a stable order.
    /// </summary>
    public static IReadOnlyList<string> Keys(IActionResult result) => Keys(Body(result));

    /// <inheritdoc cref="Keys(IActionResult)" />
    public static IReadOnlyList<string> Keys(JsonElement element) =>
        element.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Property names of the first array element, empty when the array is empty.
    /// </summary>
    public static IReadOnlyList<string> FirstItemKeys(IActionResult result)
    {
        JsonElement body = Body(result);
        JsonElement? first = body.EnumerateArray().Cast<JsonElement?>().FirstOrDefault();
        return first is null ? [] : Keys(first.Value);
    }
}
