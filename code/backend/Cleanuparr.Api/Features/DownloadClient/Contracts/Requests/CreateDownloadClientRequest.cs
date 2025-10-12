using System;

using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;

public sealed record CreateDownloadClientRequest
{
    public bool Enabled { get; init; }

    public string Name { get; init; } = string.Empty;

    public DownloadClientTypeName TypeName { get; init; }

    public DownloadClientType Type { get; init; }

    public Uri? Host { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? UrlBase { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ValidationException("Client name cannot be empty");
        }

        if (Host is null)
        {
            throw new ValidationException("Host cannot be empty");
        }
    }

    public DownloadClientConfig ToEntity() => new()
    {
        Enabled = Enabled,
        Name = Name,
        TypeName = TypeName,
        Type = Type,
        Host = Host,
        Username = Username,
        Password = Password,
        UrlBase = UrlBase,
    };
}
