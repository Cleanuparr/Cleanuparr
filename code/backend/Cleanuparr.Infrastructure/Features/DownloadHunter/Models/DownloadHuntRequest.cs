using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;

namespace Cleanuparr.Infrastructure.Features.DownloadHunter.Models;

public sealed record DownloadHuntRequest<T>
    where T : SearchItem
{
    public required InstanceType InstanceType { get; init; }
    
    public required ArrInstance Instance { get; init; }
    
    public required T SearchItem { get; init; }
    
    public required QueueRecord Record { get; init; }
}