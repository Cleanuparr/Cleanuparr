using System.Text;
using System.Text.Json;

namespace Cleanuparr.Infrastructure.Features.Notifications;

/// <summary>
/// Shared HTTP send mechanics for the JSON-based notification proxies: serialization, tracing,
/// dispatch and status-code-to-exception mapping.
/// </summary>
public static class NotificationHttpSender
{
    /// <summary>
    /// Serializes <paramref name="payload"/> onto <paramref name="request"/> and sends it, translating
    /// any <see cref="HttpRequestException"/> into a provider-specific exception via
    /// <paramref name="exceptionFactory"/> and <paramref name="statusCodeMessages"/>.
    /// </summary>
    /// <param name="httpClient">The client used to send the request.</param>
    /// <param name="request">The request to send; its <see cref="HttpRequestMessage.Content"/> is set by this method.</param>
    /// <param name="payload">The payload to serialize as the request body.</param>
    /// <param name="serializerOptions">The JSON options to serialize <paramref name="payload"/> with.</param>
    /// <param name="logTrace">Optional callback invoked with the serialized payload before sending, for trace logging.</param>
    /// <param name="exceptionFactory">Builds the provider-specific exception from a message and an optional inner exception.</param>
    /// <param name="statusCodeMessages">Maps an HTTP status code to the message and whether to include the inner exception.</param>
    /// <param name="defaultErrorMessage">The message used when the status code is unknown or absent.</param>
    public static async Task SendAsync<TPayload>(
        HttpClient httpClient,
        HttpRequestMessage request,
        TPayload payload,
        JsonSerializerOptions serializerOptions,
        Action<string>? logTrace,
        Func<string, Exception?, Exception> exceptionFactory,
        IReadOnlyDictionary<int, (string Message, bool IncludeException)> statusCodeMessages,
        string defaultErrorMessage = "unable to send notification")
    {
        try
        {
            string content = JsonSerializer.Serialize(payload, serializerOptions);
            logTrace?.Invoke(content);

            request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode is null)
            {
                throw exceptionFactory(defaultErrorMessage, exception);
            }

            if (statusCodeMessages.TryGetValue((int)exception.StatusCode, out (string Message, bool IncludeException) entry))
            {
                throw exceptionFactory(entry.Message, entry.IncludeException ? exception : null);
            }

            throw exceptionFactory(defaultErrorMessage, exception);
        }
    }
}
