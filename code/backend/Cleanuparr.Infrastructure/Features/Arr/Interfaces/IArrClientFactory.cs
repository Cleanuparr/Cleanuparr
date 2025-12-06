using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Arr.Interfaces;

public interface IArrClientFactory
{
    IArrClient GetClient(InstanceType type);
}