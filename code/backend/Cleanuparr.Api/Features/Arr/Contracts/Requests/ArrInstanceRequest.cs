using System;
using System.ComponentModel.DataAnnotations;

using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Api.Features.Arr.Contracts.Requests;

public sealed record ArrInstanceRequest
{
    public bool Enabled { get; init; } = true;

    [Required]
    public required string Name { get; init; }

    [Required]
    public required string Url { get; init; }

    [Required]
    public required string ApiKey { get; init; }

    [Required]
    public required float Version { get; init; }

    public string? ExternalUrl { get; init; }

    public ArrInstance ToEntity(Guid configId)
    {
        if (ApiKey.IsPlaceholder())
        {
            throw new ValidationException("API key is required when creating a new instance");
        }

        return new()
        {
            Enabled = Enabled,
            Name = Name,
            Url = new Uri(Url),
            ExternalUrl = ExternalUrl is not null ? new Uri(ExternalUrl) : null,
            ApiKey = ApiKey,
            ArrConfigId = configId,
            Version = Version,
        };
    }

    public void ApplyTo(ArrInstance instance)
    {
        instance.Enabled = Enabled;
        instance.Name = Name;
        instance.Url = new Uri(Url);
        instance.ExternalUrl = ExternalUrl is not null ? new Uri(ExternalUrl) : null;
        instance.ApiKey = ApiKey.IsPlaceholder() ? instance.ApiKey : ApiKey;
        instance.Version = Version;
    }
}
