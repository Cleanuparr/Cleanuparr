using System.Security.Claims;
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
[Route("api/account")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly UsersContext _usersContext;
    private readonly IPasswordService _passwordService;
    private readonly ITotpService _totpService;
    private readonly IPlexAuthService _plexAuthService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UsersContext usersContext,
        IPasswordService passwordService,
        ITotpService totpService,
        IPlexAuthService plexAuthService,
        IJwtService jwtService,
        ILogger<AccountController> logger)
    {
        _usersContext = usersContext;
        _passwordService = passwordService;
        _totpService = totpService;
        _plexAuthService = plexAuthService;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAccountInfo()
    {
        var user = await GetCurrentUser();
        if (user is null) return Unauthorized();

        return Ok(new AccountInfoResponse
        {
            Username = user.Username,
            PlexLinked = user.PlexAccountId is not null,
            PlexUsername = user.PlexUsername,
            TwoFactorEnabled = user.TotpEnabled,
            ApiKeyPreview = user.ApiKey[..8] + "..."
        });
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser();
            if (user is null) return Unauthorized();

            if (!_passwordService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { error = "Current password is incorrect" });
            }

            user.PasswordHash = _passwordService.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("Password changed for user {Username}", user.Username);

            return Ok(new { message = "Password changed" });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("2fa/regenerate")]
    public async Task<IActionResult> Regenerate2fa([FromBody] Regenerate2faRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser(includeRecoveryCodes: true);
            if (user is null) return Unauthorized();

            // Verify current credentials
            if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            {
                return BadRequest(new { error = "Incorrect password" });
            }

            if (!_totpService.ValidateCode(user.TotpSecret, request.TotpCode))
            {
                return BadRequest(new { error = "Invalid 2FA code" });
            }

            // Generate new TOTP
            var secret = _totpService.GenerateSecret();
            var qrUri = _totpService.GetQrCodeUri(secret, user.Username);
            var recoveryCodes = _totpService.GenerateRecoveryCodes();

            user.TotpSecret = secret;
            user.UpdatedAt = DateTime.UtcNow;

            // Replace recovery codes
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

            _logger.LogInformation("2FA regenerated for user {Username}", user.Username);

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

    [HttpGet("api-key")]
    public async Task<IActionResult> GetApiKey()
    {
        var user = await GetCurrentUser();
        if (user is null) return Unauthorized();

        return Ok(new { apiKey = user.ApiKey });
    }

    [HttpPost("api-key/regenerate")]
    public async Task<IActionResult> RegenerateApiKey()
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser();
            if (user is null) return Unauthorized();

            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            user.ApiKey = Convert.ToHexString(bytes).ToLowerInvariant();
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("API key regenerated for user {Username}", user.Username);

            return Ok(new { apiKey = user.ApiKey });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("plex/link")]
    public async Task<IActionResult> StartPlexLink()
    {
        var pin = await _plexAuthService.RequestPin();

        return Ok(new { pinId = pin.PinId, authUrl = pin.AuthUrl });
    }

    [HttpPost("plex/link/verify")]
    public async Task<IActionResult> VerifyPlexLink([FromBody] PlexPinRequest request)
    {
        var pinResult = await _plexAuthService.CheckPin(request.PinId);

        if (!pinResult.Completed || pinResult.AuthToken is null)
        {
            return Ok(new { completed = false });
        }

        var plexAccount = await _plexAuthService.GetAccount(pinResult.AuthToken);

        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser();
            if (user is null) return Unauthorized();

            user.PlexAccountId = plexAccount.AccountId;
            user.PlexUsername = plexAccount.Username;
            user.PlexEmail = plexAccount.Email;
            user.PlexAuthToken = pinResult.AuthToken;
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("Plex account linked for user {Username}: {PlexUsername}",
                user.Username, plexAccount.Username);

            return Ok(new { completed = true, plexUsername = plexAccount.Username });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpDelete("plex/link")]
    public async Task<IActionResult> UnlinkPlex()
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser();
            if (user is null) return Unauthorized();

            user.PlexAccountId = null;
            user.PlexUsername = null;
            user.PlexEmail = null;
            user.PlexAuthToken = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("Plex account unlinked for user {Username}", user.Username);

            return Ok(new { message = "Plex account unlinked" });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    private async Task<User?> GetCurrentUser(bool includeRecoveryCodes = false)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        var query = _usersContext.Users.AsQueryable();

        if (includeRecoveryCodes)
        {
            query = query.Include(u => u.RecoveryCodes);
        }

        return await query.FirstOrDefaultAsync(u => u.Id == userId);
    }
}
