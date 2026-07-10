namespace Cleanuparr.Infrastructure.Features.Seeker;

public interface ISeekerStateCleanup
{
    Task DeleteForInstanceAsync(Guid arrInstanceId, CancellationToken cancellationToken = default);
}
