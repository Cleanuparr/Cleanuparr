using System.Security.Cryptography;
using Cleanuparr.Persistence.Models.Auth;
using OtpNet;

namespace Cleanuparr.Infrastructure.Features.Auth;

public sealed class TotpService : ITotpService
{
    private const string Issuer = "Cleanuparr";

    /// <inheritdoc/>
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    /// <inheritdoc/>
    public string GetQrCodeUri(string secret, string username)
    {
        return $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(username)}?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";
    }

    /// <inheritdoc/>
    public bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string trimmedCode = code.Trim();

        if (trimmedCode.Length != 6)
        {
            return false;
        }

        try
        {
            var keyBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(keyBytes);
            return totp.VerifyTotp(trimmedCode, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public List<string> GenerateRecoveryCodes(int count = 10)
    {
        var codes = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            // Generate 8-character alphanumeric codes in format XXXX-XXXX
            var bytes = new byte[5];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            var code = Convert.ToHexString(bytes)[..8].ToUpperInvariant();
            codes.Add($"{code[..4]}-{code[4..]}");
        }

        return codes;
    }

    /// <inheritdoc/>
    public string HashRecoveryCode(string code)
    {
        // Normalize: remove dashes and uppercase
        var normalized = code.Replace("-", "").ToUpperInvariant();
        return BCrypt.Net.BCrypt.HashPassword(normalized, 10);
    }

    /// <inheritdoc/>
    public bool VerifyRecoveryCode(string code, string hash)
    {
        try
        {
            string normalized = code.Trim().Replace("-", "").ToUpperInvariant();
            return BCrypt.Net.BCrypt.Verify(normalized, hash);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool VerifySecondFactor(User user, string code)
    {
        if (ValidateCode(user.TotpSecret, code))
        {
            return true;
        }

        return user.RecoveryCodes
            .Any(recoveryCode => !recoveryCode.IsUsed && VerifyRecoveryCode(code, recoveryCode.CodeHash));
    }
}
