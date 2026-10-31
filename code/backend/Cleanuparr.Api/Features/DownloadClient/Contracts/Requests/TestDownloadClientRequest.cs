using System;

using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;

public sealed record TestDownloadClientRequest
{
    public DownloadClientTypeName TypeName { get; init; }

    public string? Host { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? ApiKey { get; init; }

    public HttpAuthType HttpAuthType { get; init; }

    public string? HttpAuthUsername { get; init; }

    public string? HttpAuthPassword { get; init; }

    public string? UrlBase { get; init; }

    public Guid? ClientId { get; init; }

    public void Validate()
    {
        if (TypeName is DownloadClientTypeName.Unknown || !Enum.IsDefined(TypeName))
        {
            throw new CustomValidationException("Unsupported download client type");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new CustomValidationException("Host cannot be empty");
        }

        if (!Uri.TryCreate(Host, UriKind.RelativeOrAbsolute, out Uri? parsedHost))
        {
            throw new CustomValidationException("Host is not a valid URL");
        }

        if (parsedHost.IsAbsoluteUri && parsedHost.UserInfo.Length > 0)
        {
            throw new CustomValidationException(
                "Credentials in the URL are not supported. Use the HTTP authentication fields instead.");
        }

        if (TypeName.RequiresAuthField(DownloadClientAuthField.ApiKey) && string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.IsPlaceholder())
        {
            throw new CustomValidationException("API key cannot be empty");
        }

        if (TypeName.SupportsApiKeyAndCredentials() && !string.IsNullOrWhiteSpace(ApiKey) &&
            (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password)))
        {
            throw new CustomValidationException("Use either an API key or a username and password, not both");
        }

        if (TypeName.ApiKeyConflictsWithHttpAuth() && !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(HttpAuthUsername))
        {
            throw new CustomValidationException(
                "API key can't be combined with HTTP authentication. Use qBittorrent username/password instead.");
        }

        string? digestUsername = TypeName.UsesDedicatedHttpAuthFields() ? HttpAuthUsername : Username;

        if (HttpAuthType == HttpAuthType.Digest && string.IsNullOrWhiteSpace(digestUsername))
        {
            throw new CustomValidationException("HTTP Digest authentication requires a username");
        }
    }

    public DownloadClientConfig ToTestConfig(string? resolvedPassword = null, string? resolvedApiKey = null, string? resolvedHttpAuthPassword = null)
    {
        var password = resolvedPassword ?? Password;
        var apiKey = resolvedApiKey ?? ApiKey;
        var httpAuthPassword = resolvedHttpAuthPassword ?? HttpAuthPassword;

        if (password.IsPlaceholder())
        {
            throw new CustomValidationException("Password cannot be a placeholder value");
        }

        if (apiKey.IsPlaceholder())
        {
            throw new CustomValidationException("API key cannot be a placeholder value");
        }

        if (httpAuthPassword.IsPlaceholder())
        {
            throw new CustomValidationException("HTTP auth password cannot be a placeholder value");
        }

        return new()
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            Name = "Test Client",
            TypeName = TypeName,
            Type = TypeName.ClientType(),
            Host = new Uri(Host!, UriKind.RelativeOrAbsolute),
            Username = Username,
            Password = password,
            ApiKey = apiKey,
            HttpAuthType = HttpAuthType,
            HttpAuthUsername = HttpAuthUsername,
            HttpAuthPassword = httpAuthPassword,
            UrlBase = UrlBase,
        };
    }
}
