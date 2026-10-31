using System;

using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;

public sealed record UpdateDownloadClientRequest
{
    public bool Enabled { get; init; }

    public string Name { get; init; } = string.Empty;

    public DownloadClientTypeName TypeName { get; init; }

    public string? Host { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? ApiKey { get; init; }

    public HttpAuthType HttpAuthType { get; init; }

    public string? HttpAuthUsername { get; init; }

    public string? HttpAuthPassword { get; init; }

    public string? UrlBase { get; init; }

    public string? ExternalUrl { get; init; }

    public string? DownloadDirectorySource { get; init; }

    public string? DownloadDirectoryTarget { get; init; }

    public void Validate()
    {
        if (TypeName is DownloadClientTypeName.Unknown || !Enum.IsDefined(TypeName))
        {
            throw new CustomValidationException("Unsupported download client type");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new CustomValidationException("Client name cannot be empty");
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

        if (!string.IsNullOrWhiteSpace(ExternalUrl) && !Uri.TryCreate(ExternalUrl, UriKind.RelativeOrAbsolute, out _))
        {
            throw new CustomValidationException("External URL is not a valid URL");
        }

        if (TypeName.RequiresAuthField(DownloadClientAuthField.ApiKey) && string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.IsPlaceholder())
        {
            throw new CustomValidationException("API key cannot be empty");
        }
    }

    public DownloadClientConfig ApplyTo(DownloadClientConfig existing) => existing with
    {
        Enabled = Enabled,
        Name = Name,
        TypeName = TypeName,
        Type = TypeName.ClientType(),
        Host = new Uri(Host!, UriKind.RelativeOrAbsolute),
        Username = Username,
        Password = Password.IsPlaceholder() ? existing.Password : Password,
        ApiKey = ApiKey.IsPlaceholder() ? existing.ApiKey : ApiKey,
        HttpAuthType = HttpAuthType,
        HttpAuthUsername = HttpAuthUsername,
        HttpAuthPassword = HttpAuthPassword.IsPlaceholder() ? existing.HttpAuthPassword : HttpAuthPassword,
        UrlBase = UrlBase,
        ExternalUrl = !string.IsNullOrWhiteSpace(ExternalUrl) ? new Uri(ExternalUrl, UriKind.RelativeOrAbsolute) : null,
        DownloadDirectorySource = DownloadDirectorySource,
        DownloadDirectoryTarget = DownloadDirectoryTarget,
    };
}
