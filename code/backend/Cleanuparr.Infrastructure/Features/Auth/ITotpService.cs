using Cleanuparr.Persistence.Models.Auth;

namespace Cleanuparr.Infrastructure.Features.Auth;

public interface ITotpService
{
    /// <summary>
    /// Creates a base32 TOTP secret.
    /// </summary>
    string GenerateSecret();

    /// <summary>
    /// Builds the otpauth URI for an authenticator app.
    /// </summary>
    string GetQrCodeUri(string secret, string username);

    /// <summary>
    /// Checks a 6-digit authenticator code against the secret.
    /// </summary>
    bool ValidateCode(string secret, string code);

    /// <summary>
    /// Creates single-use recovery codes in XXXX-XXXX format.
    /// </summary>
    List<string> GenerateRecoveryCodes(int count = 10);

    /// <summary>
    /// Hashes a recovery code for storage.
    /// </summary>
    string HashRecoveryCode(string code);

    /// <summary>
    /// Checks a recovery code against a stored hash.
    /// </summary>
    bool VerifyRecoveryCode(string code, string hash);

    /// <summary>
    /// Accepts an authenticator code or an unused recovery code.
    /// Does not mark the recovery code as used.
    /// </summary>
    bool VerifySecondFactor(User user, string code);
}
