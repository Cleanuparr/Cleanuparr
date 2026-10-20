using System.Collections.Concurrent;

namespace Cleanuparr.Infrastructure.Features.Seeker;

/// <summary>
/// Serializes writes to an instance's Seeker cycle state between the Seeker job and the cycle reset endpoint.
/// </summary>
public sealed class SeekerCycleLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <summary>
    /// Waits until the instance's lock is free, then takes it.
    /// </summary>
    public Task WaitAsync(Guid arrInstanceId, CancellationToken cancellationToken = default)
    {
        return GetLock(arrInstanceId).WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Takes the instance's lock if it is free.
    /// </summary>
    /// <returns>Whether the lock was taken.</returns>
    public bool TryEnter(Guid arrInstanceId)
    {
        return GetLock(arrInstanceId).Wait(0);
    }

    /// <summary>
    /// Releases the instance's lock.
    /// </summary>
    public void Release(Guid arrInstanceId)
    {
        GetLock(arrInstanceId).Release();
    }

    private SemaphoreSlim GetLock(Guid arrInstanceId)
    {
        return _locks.GetOrAdd(arrInstanceId, _ => new SemaphoreSlim(1, 1));
    }
}
