using System.Security.Claims;
using Cleanuparr.Persistence.Models.Auth;

namespace Cleanuparr.Infrastructure.Features.Auth;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateLoginToken(Guid userId);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateAccessToken(string token);
    Guid? ValidateLoginToken(string token);
    byte[] GetOrCreateSigningKey();
}
