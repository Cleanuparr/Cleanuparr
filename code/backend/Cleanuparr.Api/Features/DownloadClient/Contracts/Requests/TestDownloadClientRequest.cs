using System;

using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Persistence.Models.Configuration;

namespace Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;

public sealed record TestDownloadClientRequest
{
    public DownloadClientTypeName TypeName { get; init; }

    public DownloadClientType Type { get; init; }

    public Uri? Host { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? UrlBase { get; init; }

    public void Validate()
    {
        if (Host is null)
        {
            throw new ValidationException("Host cannot be empty");
        }
    }

    public DownloadClientConfig ToTestConfig() => new()
    {
        Id = Guid.NewGuid(),
        Enabled = true,
        Name = "Test Client",
        TypeName = TypeName,
        Type = Type,
        Host = Host,
        Username = Username,
        Password = Password,
        UrlBase = UrlBase,
    };
}
