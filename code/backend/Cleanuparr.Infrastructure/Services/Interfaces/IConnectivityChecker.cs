using Cleanuparr.Persistence.Models.Configuration.General;

namespace Cleanuparr.Infrastructure.Services.Interfaces;

public interface IConnectivityChecker
{
    Task<bool> IsOnlineAsync(GeneralConfig config, CancellationToken cancellationToken = default);
}
