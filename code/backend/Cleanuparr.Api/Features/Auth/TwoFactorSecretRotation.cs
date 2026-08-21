using Cleanuparr.Api.Features.Auth.Contracts.Responses;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;

namespace Cleanuparr.Api.Features.Auth;

internal static class TwoFactorSecretRotation
{
    /// <summary>
    /// Replaces the user's TOTP secret and recovery codes.
    /// </summary>
    internal static TotpSetupResponse Rotate(ITotpService totpService, UsersContext usersContext, User user)
    {
        string secret = totpService.GenerateSecret();
        string qrUri = totpService.GetQrCodeUri(secret, user.Username);
        List<string> recoveryCodes = totpService.GenerateRecoveryCodes();

        user.TotpSecret = secret;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        usersContext.RecoveryCodes.RemoveRange(user.RecoveryCodes);

        foreach (string code in recoveryCodes)
        {
            usersContext.RecoveryCodes.Add(new RecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CodeHash = totpService.HashRecoveryCode(code),
                IsUsed = false
            });
        }

        return new TotpSetupResponse
        {
            Secret = secret,
            QrCodeUri = qrUri,
            RecoveryCodes = recoveryCodes
        };
    }
}
