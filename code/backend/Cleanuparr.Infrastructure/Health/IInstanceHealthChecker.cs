using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Health;

public interface IInstanceHealthChecker
{
    /// <summary>
    /// Probes an instance, whichever kind it is. Throws when it is unreachable or refuses the key.
    /// The type is passed in because callers do not always load the config navigation.
    /// </summary>
    Task CheckAsync(InstanceType type, ArrInstance instance);
}
