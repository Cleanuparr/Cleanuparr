namespace Cleanuparr.Infrastructure.Features.Auth;

public interface IPasswordService
{
    string DummyHash { get; }
    
    string HashPassword(string password);
    
    bool VerifyPassword(string password, string hash);
}
