namespace Cleanuparr.Infrastructure.Features.Jobs;

public interface IHandler
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}