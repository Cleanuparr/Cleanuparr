namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed class PasswordService : IPasswordService
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Pre-computed BCrypt hash with a work factor of 12 used as a fallback when no user exists
    /// </summary>
    public string DummyHash => "$2a$12$tQw4MgGGq7WTFro3Me4mQOekctJ0mIOYmFMn.XEmEbyZhBq0i4qKy";

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
