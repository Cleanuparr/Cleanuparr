namespace Cleanuparr.Infrastructure.Features.Auth;

public interface ITotpService
{
    string GenerateSecret();
    string GetQrCodeUri(string secret, string username);
    bool ValidateCode(string secret, string code);
    List<string> GenerateRecoveryCodes(int count = 10);
    string HashRecoveryCode(string code);
    bool VerifyRecoveryCode(string code, string hash);
}
