using System;
using System.ComponentModel.DataAnnotations;

using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Shared.Helpers;

namespace Cleanuparr.Api.Features.Arr.Contracts.Requests;

public sealed record TestArrInstanceRequest
{
    [Required]
    public required string Url { get; init; }

    [Required]
    public required string ApiKey { get; init; }

    [Required]
    public required float Version { get; init; }

    public ArrInstance ToTestInstance()
    {
        if (ApiKey.IsPlaceholder())
        {
            throw new ValidationException("API key cannot be a placeholder value");
        }

        return new()
        {
            Enabled = true,
            Name = "Test Instance",
            Url = new Uri(Url),
            ApiKey = ApiKey,
            ArrConfigId = Guid.Empty,
            Version = Version,
        };
    }
}
