using System;
using System.ComponentModel.DataAnnotations;

using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Api.Features.Arr.Contracts.Requests;

public sealed record TestArrInstanceRequest
{
    [Required]
    public required string Url { get; init; }

    [Required]
    public required string ApiKey { get; init; }

    public ArrInstance ToTestInstance() => new()
    {
        Enabled = true,
        Name = "Test Instance",
        Url = new Uri(Url),
        ApiKey = ApiKey,
        ArrConfigId = Guid.Empty,
    };
}
