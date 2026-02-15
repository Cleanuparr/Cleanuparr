using System;

using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;

public sealed record UpdateDownloadClientRequest
{
    public bool Enabled { get; init; }

    public string Name { get; init; } = string.Empty;

    public DownloadClientTypeName TypeName { get; init; }

    public DownloadClientType Type { get; init; }

    public string? Host { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? UrlBase { get; init; }

    public string? ExternalUrl { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ValidationException("Client name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ValidationException("Host cannot be empty");
        }
    }

    public DownloadClientConfig ApplyTo(DownloadClientConfig existing) => existing with
    {
        Enabled = Enabled,
        Name = Name,
        TypeName = TypeName,
        Type = Type,
        Host = new Uri(Host!, UriKind.RelativeOrAbsolute),
        Username = Username,
        Password = Password,
        UrlBase = UrlBase,
        ExternalUrl = !string.IsNullOrWhiteSpace(ExternalUrl) ? new Uri(ExternalUrl, UriKind.RelativeOrAbsolute) : null,
    };
}
