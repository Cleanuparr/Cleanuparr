using System;
using System.ComponentModel.DataAnnotations;

using Cleanuparr.Persistence.Models.Configuration.Arr;

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

    public ArrInstance ToEntity(Guid configId) => new()
    {
        Enabled = Enabled,
        Name = Name,
        Url = new Uri(Url),
        ApiKey = ApiKey,
        ArrConfigId = configId,
    };

    public void ApplyTo(ArrInstance instance)
    {
        instance.Enabled = Enabled;
        instance.Name = Name;
        instance.Url = new Uri(Url);
        instance.ApiKey = ApiKey;
    }
}
