using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Auth;

public sealed class LoginAttemptTracker
{
    private const int MaxLockoutSeconds = 300;

    private readonly UsersContext _usersContext;
    private readonly ILogger<LoginAttemptTracker> _logger;

    public LoginAttemptTracker(UsersContext usersContext, ILogger<LoginAttemptTracker> logger)
    {
        _usersContext = usersContext;
        _logger = logger;
    }

    public static int? GetLockoutSecondsRemaining(User user)
    {
        if (user.LockoutEnd is null || user.LockoutEnd.Value <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return (int)Math.Ceiling((user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalSeconds);
    }

    public async Task<int> IncrementFailedAttempts(Guid userId)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            User user = await _usersContext.Users.FirstAsync(u => u.Id == userId);
            user.FailedLoginAttempts++;

            int lockoutSeconds = Math.Min(user.FailedLoginAttempts * 2, MaxLockoutSeconds);
            user.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(lockoutSeconds);
            await _usersContext.SaveChangesAsync();

            _logger.LogWarning("Failed login attempt {Attempts} for user {Username}, locked for {Seconds}s",
                user.FailedLoginAttempts, user.Username, lockoutSeconds);

            return lockoutSeconds;
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    public async Task ResetFailedAttempts(Guid userId)
    {
        await UsersContext.Lock.WaitAsync();
        try
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
        finally
        {
            UsersContext.Lock.Release();
        }
    }
}
