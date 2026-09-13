using Cleanuparr.Api.Features.Auth;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features.Auth;

public sealed class LoginAttemptTrackerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly UsersContext _usersContext;
    private readonly LoginAttemptTracker _sut;
    private readonly Guid _userId = Guid.NewGuid();

    // Frozen at the real now: the lockout windows are relative, and rounding must not drift while the test runs.
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    public LoginAttemptTrackerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptions<UsersContext> options = new DbContextOptionsBuilder<UsersContext>()
            .UseSqlite(_connection)
            .Options;

        _usersContext = new UsersContext(options);
        _usersContext.Database.EnsureCreated();

        _usersContext.Users.Add(new User
        {
            Id = _userId,
            Username = "admin",
            PasswordHash = "hash",
            TotpSecret = string.Empty,
            ApiKey = "key",
            SetupCompleted = true
        });
        _usersContext.SaveChanges();

        _sut = new LoginAttemptTracker(_usersContext, NullLogger<LoginAttemptTracker>.Instance, _clock);
    }

    [Fact]
    public async Task IncrementFailedAttempts_GrowsTheLockoutWindowWithEachAttempt()
    {
        (await _sut.IncrementFailedAttempts(_userId)).ShouldBe(2);
        (await _sut.IncrementFailedAttempts(_userId)).ShouldBe(4);
        (await _sut.IncrementFailedAttempts(_userId)).ShouldBe(6);

        User user = await _usersContext.Users.FirstAsync(u => u.Id == _userId);
        user.FailedLoginAttempts.ShouldBe(3);
        user.LockoutEnd.ShouldNotBeNull();
        _sut.GetLockoutSecondsRemaining(user).ShouldNotBeNull();
    }

    [Fact]
    public async Task IncrementFailedAttempts_StopsGrowingAtTheMaximumWindow()
    {
        User seeded = await _usersContext.Users.FirstAsync(u => u.Id == _userId);
        seeded.FailedLoginAttempts = 148;
        await _usersContext.SaveChangesAsync();

        (await _sut.IncrementFailedAttempts(_userId)).ShouldBe(298);
        (await _sut.IncrementFailedAttempts(_userId)).ShouldBe(300);
        (await _sut.IncrementFailedAttempts(_userId)).ShouldBe(300);

        User user = await _usersContext.Users.FirstAsync(u => u.Id == _userId);
        user.FailedLoginAttempts.ShouldBe(151);
        _sut.GetLockoutSecondsRemaining(user)!.Value.ShouldBeLessThanOrEqualTo(300);
    }

    [Fact]
    public async Task ResetFailedAttempts_ClearsTheCounterAndLockout()
    {
        await _sut.IncrementFailedAttempts(_userId);
        await _sut.IncrementFailedAttempts(_userId);

        await _sut.ResetFailedAttempts(_userId);

        User user = await _usersContext.Users.FirstAsync(u => u.Id == _userId);
        user.FailedLoginAttempts.ShouldBe(0);
        user.LockoutEnd.ShouldBeNull();
        _sut.GetLockoutSecondsRemaining(user).ShouldBeNull();
    }

    [Fact]
    public void GetLockoutSecondsRemaining_WithoutLockout_ReturnsNull()
    {
        User user = CreateUser(lockoutEnd: null);

        _sut.GetLockoutSecondsRemaining(user).ShouldBeNull();
    }

    [Fact]
    public void GetLockoutSecondsRemaining_WithExpiredLockout_ReturnsNull()
    {
        User user = CreateUser(_clock.GetUtcNow().AddSeconds(-1));

        _sut.GetLockoutSecondsRemaining(user).ShouldBeNull();
    }

    [Fact]
    public void GetLockoutSecondsRemaining_WithActiveLockout_RoundsUpToWholeSeconds()
    {
        User user = CreateUser(_clock.GetUtcNow().AddSeconds(9.9));

        _sut.GetLockoutSecondsRemaining(user).ShouldBe(10);
    }

    private static User CreateUser(DateTimeOffset? lockoutEnd)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = "hash",
            TotpSecret = string.Empty,
            ApiKey = "key",
            LockoutEnd = lockoutEnd
        };
    }

    public void Dispose()
    {
        _usersContext.Dispose();
        _connection.Dispose();
    }
}
