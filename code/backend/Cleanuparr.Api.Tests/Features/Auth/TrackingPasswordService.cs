using Cleanuparr.Infrastructure.Features.Auth;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Spy wrapper around <see cref="PasswordService"/> that tracks calls to
/// <see cref="VerifyPassword"/> for behavioral assertions in timing tests.
/// </summary>
public sealed class TrackingPasswordService : IPasswordService
{
    private readonly PasswordService _inner = new();
    private int _verifyPasswordCallCount;

    public int VerifyPasswordCallCount => _verifyPasswordCallCount;

    public string HashPassword(string password)
    {
        return _inner.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        Interlocked.Increment(ref _verifyPasswordCallCount);
        return _inner.VerifyPassword(password, hash);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _verifyPasswordCallCount, 0);
    }
}
