using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.Auth.Models;

internal sealed class OidcTokenResponse
{
    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
}
