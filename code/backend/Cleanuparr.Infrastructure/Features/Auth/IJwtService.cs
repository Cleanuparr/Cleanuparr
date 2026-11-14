using Cleanuparr.Persistence.Models.Auth;

namespace Cleanuparr.Infrastructure.Features.Auth;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateLoginToken(Guid userId);
    string GenerateRefreshToken();
    Guid? ValidateLoginToken(string token);
    byte[] GetOrCreateSigningKey();
}
