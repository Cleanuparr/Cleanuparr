using System.Security.Cryptography;
using Cleanuparr.Api.Features.Auth.Contracts.Requests;
using Cleanuparr.Api.Features.Auth.Contracts.Responses;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cleanuparr.Api.Features.Auth.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly UsersContext _usersContext;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly ITotpService _totpService;
    private readonly IPlexAuthService _plexAuthService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UsersContext usersContext,
        IJwtService jwtService,
        IPasswordService passwordService,
        ITotpService totpService,
        IPlexAuthService plexAuthService,
        ILogger<AuthController> logger)
    {
        _usersContext = usersContext;
        _jwtService = jwtService;
        _passwordService = passwordService;
        _totpService = totpService;
        _plexAuthService = plexAuthService;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var user = await _usersContext.Users.AsNoTracking().FirstOrDefaultAsync();

        return Ok(new AuthStatusResponse
        {
            SetupCompleted = user is { SetupCompleted: true },
            PlexLinked = user?.PlexAccountId is not null
        });
    }

    [HttpPost("setup/account")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var existingUser = await _usersContext.Users.FirstOrDefaultAsync();
            if (existingUser is not null)
            {
                return Conflict(new { error = "Account already exists" });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                PasswordHash = _passwordService.HashPassword(request.Password),
                TotpSecret = string.Empty,
                TotpEnabled = false,
                ApiKey = GenerateApiKey(),
                SetupCompleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _usersContext.Users.Add(user);
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("Admin account created for user {Username}", request.Username);

            return Created("", new { userId = user.Id });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("setup/2fa/generate")]
    public async Task<IActionResult> GenerateTotpSetup()
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await _usersContext.Users
                .Include(u => u.RecoveryCodes)
                .FirstOrDefaultAsync();

            if (user is null)
            {
                return BadRequest(new { error = "Create an account first" });
            }

            if (user.SetupCompleted && user.TotpEnabled)
            {
                return Conflict(new { error = "2FA is already configured" });
            }

            // Generate new TOTP secret
            var secret = _totpService.GenerateSecret();
            var qrUri = _totpService.GetQrCodeUri(secret, user.Username);

            // Generate recovery codes
            var recoveryCodes = _totpService.GenerateRecoveryCodes();

            // Store secret (will be finalized on verify)
            user.TotpSecret = secret;
            user.UpdatedAt = DateTime.UtcNow;

            // Remove old recovery codes and add new ones
            _usersContext.RecoveryCodes.RemoveRange(user.RecoveryCodes);

            foreach (var code in recoveryCodes)
            {
                _usersContext.RecoveryCodes.Add(new RecoveryCode
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CodeHash = _totpService.HashRecoveryCode(code),
                    IsUsed = false
                });
            }

            await _usersContext.SaveChangesAsync();

            return Ok(new TotpSetupResponse
            {
                Secret = secret,
                QrCodeUri = qrUri,
                RecoveryCodes = recoveryCodes
            });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("setup/2fa/verify")]
    public async Task<IActionResult> VerifyTotpSetup([FromBody] VerifyTotpRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await _usersContext.Users.FirstOrDefaultAsync();
            if (user is null)
            {
                return BadRequest(new { error = "Create an account first" });
            }

            if (string.IsNullOrEmpty(user.TotpSecret))
            {
                return BadRequest(new { error = "Generate 2FA setup first" });
            }

            if (!_totpService.ValidateCode(user.TotpSecret, request.Code))
            {
                return Unauthorized(new { error = "Invalid verification code" });
            }

            user.TotpEnabled = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("2FA enabled for user {Username}", user.Username);

            return Ok(new { message = "2FA verified and enabled" });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("setup/complete")]
    public async Task<IActionResult> CompleteSetup()
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await _usersContext.Users.FirstOrDefaultAsync();
            if (user is null)
            {
                return BadRequest(new { error = "Create an account first" });
            }

            if (!user.TotpEnabled)
            {
                return BadRequest(new { error = "2FA must be configured before completing setup" });
            }

            user.SetupCompleted = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("Setup completed for user {Username}", user.Username);

            return Ok(new { message = "Setup complete" });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _usersContext.Users.AsNoTracking().FirstOrDefaultAsync();

        if (user is null || !user.SetupCompleted)
        {
            return Unauthorized(new { error = "Invalid credentials" });
        }

        // Check lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remaining = (int)(user.LockoutEnd.Value - DateTime.UtcNow).TotalSeconds;
            return StatusCode(429, new { error = "Account is locked", retryAfterSeconds = remaining });
        }

        if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash) ||
            !string.Equals(user.Username, request.Username, StringComparison.OrdinalIgnoreCase))
        {
            await IncrementFailedAttempts(user.Id);
            return Unauthorized(new { error = "Invalid credentials" });
        }

        // Reset failed attempts on successful password verification
        await ResetFailedAttempts(user.Id);

        // Password valid - require 2FA
        var loginToken = _jwtService.GenerateLoginToken(user.Id);

        return Ok(new LoginResponse
        {
            RequiresTwoFactor = true,
            LoginToken = loginToken
        });
    }

    [HttpPost("login/2fa")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorRequest request)
    {
        var userId = _jwtService.ValidateLoginToken(request.LoginToken);
        if (userId is null)
        {
            return Unauthorized(new { error = "Invalid or expired login token" });
        }

        var user = await _usersContext.Users
            .Include(u => u.RecoveryCodes)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user is null)
        {
            return Unauthorized(new { error = "Invalid login token" });
        }

        bool codeValid;

        if (request.IsRecoveryCode)
        {
            codeValid = await TryUseRecoveryCode(user, request.Code);
        }
        else
        {
            codeValid = _totpService.ValidateCode(user.TotpSecret, request.Code);
        }

        if (!codeValid)
        {
            return Unauthorized(new { error = "Invalid verification code" });
        }

        return Ok(await GenerateTokenResponse(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var tokenHash = HashRefreshToken(request.RefreshToken);

            var storedToken = await _usersContext.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAt == null);

            if (storedToken is null || storedToken.ExpiresAt < DateTime.UtcNow)
            {
                return Unauthorized(new { error = "Invalid or expired refresh token" });
            }

            // Revoke the old token (rotation)
            storedToken.RevokedAt = DateTime.UtcNow;

            // Generate new tokens
            var response = await GenerateTokenResponse(storedToken.User);
            await _usersContext.SaveChangesAsync();

            return Ok(response);
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var tokenHash = HashRefreshToken(request.RefreshToken);

            var storedToken = await _usersContext.RefreshTokens
                .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAt == null);

            if (storedToken is not null)
            {
                storedToken.RevokedAt = DateTime.UtcNow;
                await _usersContext.SaveChangesAsync();
            }

            return Ok(new { message = "Logged out" });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("setup/plex/pin")]
    public async Task<IActionResult> RequestSetupPlexPin()
    {
        var user = await _usersContext.Users.AsNoTracking().FirstOrDefaultAsync();
        if (user is null)
        {
            return BadRequest(new { error = "Create an account first" });
        }

        var pin = await _plexAuthService.RequestPin();

        return Ok(new PlexPinStatusResponse
        {
            PinId = pin.PinId,
            AuthUrl = pin.AuthUrl
        });
    }

    [HttpPost("setup/plex/verify")]
    public async Task<IActionResult> VerifySetupPlexLink([FromBody] PlexPinRequest request)
    {
        var pinResult = await _plexAuthService.CheckPin(request.PinId);

        if (!pinResult.Completed || pinResult.AuthToken is null)
        {
            return Ok(new PlexVerifyResponse { Completed = false });
        }

        var plexAccount = await _plexAuthService.GetAccount(pinResult.AuthToken);

        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await _usersContext.Users.FirstOrDefaultAsync();
            if (user is null)
            {
                return BadRequest(new { error = "Create an account first" });
            }

            user.PlexAccountId = plexAccount.AccountId;
            user.PlexUsername = plexAccount.Username;
            user.PlexEmail = plexAccount.Email;
            user.PlexAuthToken = pinResult.AuthToken;
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("Plex account linked during setup for user {Username}: {PlexUsername}",
                user.Username, plexAccount.Username);

            return Ok(new PlexVerifyResponse { Completed = true });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("login/plex/pin")]
    public async Task<IActionResult> RequestPlexPin()
    {
        var user = await _usersContext.Users.AsNoTracking().FirstOrDefaultAsync();
        if (user is null || !user.SetupCompleted || user.PlexAccountId is null)
        {
            return BadRequest(new { error = "Plex login is not available" });
        }

        var pin = await _plexAuthService.RequestPin();

        return Ok(new PlexPinStatusResponse
        {
            PinId = pin.PinId,
            AuthUrl = pin.AuthUrl
        });
    }

    [HttpPost("login/plex/verify")]
    public async Task<IActionResult> VerifyPlexLogin([FromBody] PlexPinRequest request)
    {
        var user = await _usersContext.Users.FirstOrDefaultAsync();
        if (user is null || !user.SetupCompleted || user.PlexAccountId is null)
        {
            return BadRequest(new { error = "Plex login is not available" });
        }

        var pinResult = await _plexAuthService.CheckPin(request.PinId);

        if (!pinResult.Completed || pinResult.AuthToken is null)
        {
            return Ok(new PlexVerifyResponse { Completed = false });
        }

        // Verify the Plex account matches the linked one
        var plexAccount = await _plexAuthService.GetAccount(pinResult.AuthToken);

        if (plexAccount.AccountId != user.PlexAccountId)
        {
            return Unauthorized(new { error = "Plex account does not match the linked account" });
        }

        // Plex login bypasses 2FA
        _logger.LogInformation("User {Username} logged in via Plex", user.Username);

        var tokenResponse = await GenerateTokenResponse(user);

        return Ok(new PlexVerifyResponse
        {
            Completed = true,
            Tokens = tokenResponse
        });
    }

    private async Task<TokenResponse> GenerateTokenResponse(User user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        _usersContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await _usersContext.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 900 // 15 minutes in seconds
        };
    }

    private async Task<bool> TryUseRecoveryCode(User user, string code)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            foreach (var recoveryCode in user.RecoveryCodes.Where(r => !r.IsUsed))
            {
                if (_totpService.VerifyRecoveryCode(code, recoveryCode.CodeHash))
                {
                    recoveryCode.IsUsed = true;
                    recoveryCode.UsedAt = DateTime.UtcNow;
                    await _usersContext.SaveChangesAsync();

                    _logger.LogWarning("Recovery code used for user {Username}", user.Username);
                    return true;
                }
            }

            return false;
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    private async Task IncrementFailedAttempts(Guid userId)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await _usersContext.Users.FirstAsync(u => u.Id == userId);
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                _logger.LogWarning("Account locked for user {Username} after {Attempts} failed attempts",
                    user.Username, user.FailedLoginAttempts);
            }

            await _usersContext.SaveChangesAsync();
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    private async Task ResetFailedAttempts(Guid userId)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await _usersContext.Users.FirstAsync(u => u.Id == userId);
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _usersContext.SaveChangesAsync();
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string HashRefreshToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
