using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Auth;

/// <summary>
/// Counts failed credential attempts and locks the account for a growing window.
/// </summary>
public sealed class LoginAttemptTracker
{
    private const int MaxLockoutSeconds = 300;

    private readonly UsersContext _usersContext;
    private readonly ILogger<LoginAttemptTracker> _logger;
    private readonly TimeProvider _timeProvider;

    public LoginAttemptTracker(UsersContext usersContext, ILogger<LoginAttemptTracker> logger, TimeProvider timeProvider)
    {
        _usersContext = usersContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the seconds left on the lockout, or null when the account is not locked.
    /// </summary>
    public int? GetLockoutSecondsRemaining(User user)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (user.LockoutEnd is null || user.LockoutEnd.Value <= now)
        {
            return null;
        }

        return (int)Math.Ceiling((user.LockoutEnd.Value - now).TotalSeconds);
    }

    /// <summary>
    /// Records one failed attempt and returns the new lockout length in seconds.
    /// The window grows by two seconds per attempt, up to five minutes.
    /// Call this while holding <see cref="UsersContext.Lock"/>.
    /// </summary>
    public async Task<int> IncrementFailedAttempts(Guid userId)
    {
        User user = await _usersContext.Users.FirstAsync(u => u.Id == userId);
        user.FailedLoginAttempts++;

        int lockoutSeconds = Math.Min(user.FailedLoginAttempts * 2, MaxLockoutSeconds);
        user.LockoutEnd = _timeProvider.GetUtcNow().AddSeconds(lockoutSeconds);
        await _usersContext.SaveChangesAsync();

        _logger.LogWarning("Failed login attempt {Attempts} for user {Username}, locked for {Seconds}s",
            user.FailedLoginAttempts, user.Username, lockoutSeconds);

        return lockoutSeconds;
    }

    /// <summary>
    /// Clears the counter and the lockout once every factor has been verified.
    /// Call this while holding <see cref="UsersContext.Lock"/>.
    /// </summary>
    public async Task ResetFailedAttempts(Guid userId)
    {
        User user = await _usersContext.Users.FirstAsync(u => u.Id == userId);

        if (user.FailedLoginAttempts is 0 && user.LockoutEnd is null)
        {
            return;
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _usersContext.SaveChangesAsync();
    }
}
