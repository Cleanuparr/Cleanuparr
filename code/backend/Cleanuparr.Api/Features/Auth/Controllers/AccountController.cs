using System.Security.Claims;
using System.Security.Cryptography;
using Cleanuparr.Api.Extensions;
using Cleanuparr.Api.Features.Auth.Contracts.Requests;
using Cleanuparr.Api.Features.Auth.Contracts.Responses;
using Cleanuparr.Api.Filters;
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
[NoCache]
public sealed class AccountController : ControllerBase
{
    private readonly UsersContext _usersContext;
    private readonly DataContext _dataContext;
    private readonly IPasswordService _passwordService;
    private readonly ITotpService _totpService;
    private readonly IPlexAuthService _plexAuthService;
    private readonly IOidcAuthService _oidcAuthService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UsersContext usersContext,
        DataContext dataContext,
        IPasswordService passwordService,
        ITotpService totpService,
        IPlexAuthService plexAuthService,
        IOidcAuthService oidcAuthService,
        ILogger<AccountController> logger)
    {
        _usersContext = usersContext;
        _dataContext = dataContext;
        _passwordService = passwordService;
        _totpService = totpService;
        _plexAuthService = plexAuthService;
        _oidcAuthService = oidcAuthService;
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

            DateTime now = DateTime.UtcNow;

            user.PasswordHash = _passwordService.HashPassword(request.NewPassword);
            user.UpdatedAt = now;

            // Revoke all existing refresh tokens so old sessions can't be reused
            var activeTokens = await _usersContext.RefreshTokens
                .Where(r => r.UserId == user.Id && r.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
            }

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

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> Enable2fa([FromBody] Enable2faRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser(includeRecoveryCodes: true);
            if (user is null) return Unauthorized();

            if (user.TotpEnabled)
            {
                return Conflict(new { error = "2FA is already enabled" });
            }

            if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            {
                return BadRequest(new { error = "Incorrect password" });
            }

            // Generate new TOTP
            var secret = _totpService.GenerateSecret();
            var qrUri = _totpService.GetQrCodeUri(secret, user.Username);
            var recoveryCodes = _totpService.GenerateRecoveryCodes();

            user.TotpSecret = secret;
            user.UpdatedAt = DateTime.UtcNow;

            // Replace any existing recovery codes
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

            _logger.LogInformation("2FA setup generated for user {Username}", user.Username);

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

    [HttpPost("2fa/enable/verify")]
    public async Task<IActionResult> VerifyEnable2fa([FromBody] VerifyTotpRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser();
            if (user is null) return Unauthorized();

            if (user.TotpEnabled)
            {
                return Conflict(new { error = "2FA is already enabled" });
            }

            if (string.IsNullOrEmpty(user.TotpSecret))
            {
                return BadRequest(new { error = "Generate 2FA setup first" });
            }

            if (!_totpService.ValidateCode(user.TotpSecret, request.Code))
            {
                return BadRequest(new { error = "Invalid verification code" });
            }

            user.TotpEnabled = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("2FA enabled for user {Username}", user.Username);

            return Ok(new { message = "2FA enabled" });
        }
        finally
        {
            UsersContext.Lock.Release();
        }
    }

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> Disable2fa([FromBody] Disable2faRequest request)
    {
        await UsersContext.Lock.WaitAsync();
        try
        {
            var user = await GetCurrentUser(includeRecoveryCodes: true);
            if (user is null) return Unauthorized();

            if (!user.TotpEnabled)
            {
                return BadRequest(new { error = "2FA is not enabled" });
            }

            if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            {
                return BadRequest(new { error = "Incorrect password" });
            }

            if (!_totpService.ValidateCode(user.TotpSecret, request.TotpCode))
            {
                return BadRequest(new { error = "Invalid 2FA code" });
            }

            user.TotpEnabled = false;
            user.TotpSecret = string.Empty;
            user.UpdatedAt = DateTime.UtcNow;

            // Remove all recovery codes
            _usersContext.RecoveryCodes.RemoveRange(user.RecoveryCodes);

            await _usersContext.SaveChangesAsync();

            _logger.LogInformation("2FA disabled for user {Username}", user.Username);

            return Ok(new { message = "2FA disabled" });
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

    [HttpPost("oidc/link")]
    public async Task<IActionResult> StartOidcLink()
    {
        var generalConfig = await _dataContext.GeneralConfigs.AsNoTracking().FirstOrDefaultAsync();
        var oidcConfig = generalConfig?.Auth.Oidc;

        if (oidcConfig is not { Enabled: true })
        {
            return BadRequest(new { error = "OIDC is not enabled" });
        }

        var redirectUri = GetOidcLinkCallbackUrl();
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            var result = await _oidcAuthService.StartAuthorization(redirectUri, userIdClaim);
            return Ok(new OidcStartResponse { AuthorizationUrl = result.AuthorizationUrl });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to start OIDC link authorization");
            return StatusCode(429, new { error = ex.Message });
        }
    }

    /// <remarks>
    /// This endpoint must be [AllowAnonymous] because the IdP redirects the user's browser here
    /// without a Bearer token. Security is ensured by validating that the OIDC flow was initiated
    /// by an authenticated user (InitiatorUserId stored in the flow state during StartOidcLink).
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("oidc/link/callback")]
    public async Task<IActionResult> OidcLinkCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        var basePath = HttpContext.Request.GetSafeBasePath();

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect($"{basePath}/settings/general?oidc_link_error=failed");
        }

        var redirectUri = GetOidcLinkCallbackUrl();
        var result = await _oidcAuthService.HandleCallback(code, state, redirectUri);

        if (!result.Success || string.IsNullOrEmpty(result.Subject))
        {
            _logger.LogWarning("OIDC link callback failed: {Error}", result.Error);
            return Redirect($"{basePath}/settings/general?oidc_link_error=failed");
        }

        // Verify the flow was initiated by an authenticated user
        if (string.IsNullOrEmpty(result.InitiatorUserId) ||
            !Guid.TryParse(result.InitiatorUserId, out var initiatorId))
        {
            _logger.LogWarning("OIDC link callback missing initiator user ID");
            return Redirect($"{basePath}/settings/general?oidc_link_error=failed");
        }

        // Verify the initiator user exists
        var user = await _usersContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == initiatorId);

        if (user is null)
        {
            _logger.LogWarning("OIDC link callback initiator user not found: {UserId}", result.InitiatorUserId);
            return Redirect($"{basePath}/settings/general?oidc_link_error=failed");
        }

        // Save the authorized subject to config
        var generalConfig = await _dataContext.GeneralConfigs.FirstOrDefaultAsync();
        
        if (generalConfig is null)
        {
            return Redirect($"{basePath}/settings/general?oidc_link_error=no_config");
        }

        generalConfig.Auth.Oidc.AuthorizedSubject = result.Subject;
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("OIDC account linked with subject: {Subject} by user: {Username}",
            result.Subject, user.Username);

        return Redirect($"{basePath}/settings/general?oidc_link=success");
    }

    private string GetOidcLinkCallbackUrl()
    {
        var request = HttpContext.Request;
        var scheme = request.Scheme;
        var host = request.Host.ToString();
        var basePath = request.PathBase.Value?.TrimEnd('/') ?? "";
        return $"{scheme}://{host}{basePath}/api/account/oidc/link/callback";
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
