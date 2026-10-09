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

    public string? UrlBase { get; init; }

    public Guid? ClientId { get; init; }

    public void Validate()
    {
        if (TypeName is DownloadClientTypeName.Unknown || !Enum.IsDefined(TypeName))
        {
            throw new ValidationException("Unsupported download client type");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ValidationException("Host cannot be empty");
        }

        if (!Uri.TryCreate(Host, UriKind.RelativeOrAbsolute, out _))
        {
            throw new ValidationException("Host is not a valid URL");
        }

        if (TypeName.RequiresAuthField(DownloadClientAuthField.ApiKey) && string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.IsPlaceholder())
        {
            throw new ValidationException("API key cannot be empty");
        }
    }

    public DownloadClientConfig ToTestConfig(string? resolvedPassword = null, string? resolvedApiKey = null)
    {
        var password = resolvedPassword ?? Password;
        var apiKey = resolvedApiKey ?? ApiKey;

        if (password.IsPlaceholder())
        {
            throw new ValidationException("Password cannot be a placeholder value");
        }

        if (apiKey.IsPlaceholder())
        {
            throw new ValidationException("API key cannot be a placeholder value");
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
            UrlBase = UrlBase,
        };
    }
}
