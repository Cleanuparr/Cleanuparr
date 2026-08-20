using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.LazyLibrarian;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Health;

public sealed class InstanceHealthChecker : IInstanceHealthChecker
{
    private readonly IArrClientFactory _arrClientFactory;
    private readonly ILazyLibrarianService _lazyLibrarianService;

    public InstanceHealthChecker(IArrClientFactory arrClientFactory, ILazyLibrarianService lazyLibrarianService)
    {
        _arrClientFactory = arrClientFactory;
        _lazyLibrarianService = lazyLibrarianService;
    }

    public async Task CheckAsync(InstanceType type, ArrInstance instance)
    {
        if (type is InstanceType.LazyLibrarian)
        {
            await _lazyLibrarianService.HealthCheckAsync(instance);
            return;
        }

        IArrClient client = _arrClientFactory.GetClient(type, instance.Version);
        await client.HealthCheckAsync(instance);
    }
}
