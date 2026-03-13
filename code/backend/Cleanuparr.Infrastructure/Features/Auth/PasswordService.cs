namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed class PasswordService : IPasswordService
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Pre-computed BCrypt hash used as a fallback when no user exists
    /// </summary>
    public static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("timing-safe-dummy", WorkFactor);

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
