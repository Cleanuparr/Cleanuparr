using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence.Models.Auth;
using OtpNet;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Auth;

public sealed class TotpServiceTests
{
    private readonly TotpService _sut = new();

    [Fact]
    public void VerifySecondFactor_WithValidTotpCode_ReturnsTrue()
    {
        string secret = _sut.GenerateSecret();
        User user = CreateUser(secret);

        _sut.VerifySecondFactor(user, ComputeTotp(secret)).ShouldBeTrue();
    }

    [Fact]
    public void VerifySecondFactor_WithUnusedRecoveryCode_ReturnsTrue()
    {
        string secret = _sut.GenerateSecret();
        List<string> codes = _sut.GenerateRecoveryCodes();
        User user = CreateUser(secret, codes);

        _sut.VerifySecondFactor(user, codes[3]).ShouldBeTrue();
    }

    [Fact]
    public void VerifySecondFactor_WithUsedRecoveryCode_ReturnsFalse()
    {
        string secret = _sut.GenerateSecret();
        List<string> codes = _sut.GenerateRecoveryCodes();
        User user = CreateUser(secret, codes);
        user.RecoveryCodes[3].IsUsed = true;

        _sut.VerifySecondFactor(user, codes[3]).ShouldBeFalse();
    }

    [Fact]
    public void VerifySecondFactor_WithUnknownCode_ReturnsFalse()
    {
        string secret = _sut.GenerateSecret();
        User user = CreateUser(secret, _sut.GenerateRecoveryCodes());

        _sut.VerifySecondFactor(user, "ABCD-1234").ShouldBeFalse();
        _sut.VerifySecondFactor(user, "000000").ShouldBeFalse();
    }

    [Fact]
    public void VerifySecondFactor_WithRecoveryCodeAndEmptySecret_ReturnsTrue()
    {
        List<string> codes = _sut.GenerateRecoveryCodes();
        User user = CreateUser(string.Empty, codes);

        _sut.VerifySecondFactor(user, codes[0]).ShouldBeTrue();
    }

    [Fact]
    public void VerifySecondFactor_WithRecoveryCodeIgnoringDashesAndCase_ReturnsTrue()
    {
        List<string> codes = _sut.GenerateRecoveryCodes();
        User user = CreateUser(_sut.GenerateSecret(), codes);

        _sut.VerifySecondFactor(user, codes[0].Replace("-", string.Empty).ToLowerInvariant()).ShouldBeTrue();
    }

    private User CreateUser(string secret, List<string>? recoveryCodes = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = "hash",
            TotpSecret = secret,
            TotpEnabled = true,
            ApiKey = "key",
            RecoveryCodes = (recoveryCodes ?? [])
                .Select(code => new RecoveryCode
                {
                    Id = Guid.NewGuid(),
                    CodeHash = _sut.HashRecoveryCode(code),
                    IsUsed = false
                })
                .ToList()
        };
    }

    private static string ComputeTotp(string secret)
    {
        return new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
    }
}
