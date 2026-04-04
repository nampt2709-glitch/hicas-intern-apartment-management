using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Validators;
using ApartmentManagement.Infrastructure;
using FluentValidation;
using Serilog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApartmentManagement.API.V1.Controllers;

[EnableRateLimiting("auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RefreshTokenRequestDto> _refreshValidator;
    private readonly IValidator<ForgotPasswordRequestDto> _forgotValidator;
    private readonly IValidator<ResetPasswordRequestDto> _resetValidator;
    private readonly QuotaRateLimiter _quota;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RefreshTokenRequestDto> refreshValidator,
        IValidator<ForgotPasswordRequestDto> forgotValidator,
        IValidator<ResetPasswordRequestDto> resetValidator,
        QuotaRateLimiter quota)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _quota = quota;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!_quota.TryConsume($"auth:register:ip:{ip}", 30, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Registration rate limit exceeded for this IP." });

        if (!_quota.TryConsume($"auth:register:acct:{dto.Email}".ToLowerInvariant(), 5, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Registration rate limit exceeded for this email." });

        var validation = await _registerValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ApiValidationError("Validation failed.",
                validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()));

        try
        {
            var result = await _authService.RegisterAsync(dto, cancellationToken);
            Log.ForContext("LogType", "Security").Information("REGISTER - email={Email} - success", dto.Email);
            return ApiCreated(result, "Registration successful.");
        }
        catch (InvalidOperationException ex)
        {
            Log.ForContext("LogType", "Security").Warning(ex, "REGISTER - email={Email} - failed", dto.Email);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // If IP already exceeded failed-login quota, block immediately.
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (_quota.IsExceeded($"auth:login:fail:ip:{ip}", 5, TimeSpan.FromMinutes(15), out _, out _))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Too many failed login attempts from this IP. Try again later." });

            // 2) Auth rules
            // - Login/refresh per account: 8 / 30 minutes / account (by email)
            if (!_quota.TryConsume($"auth:login:acct:{dto.Email}".ToLowerInvariant(), 8, TimeSpan.FromMinutes(30), out _, out _))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Rate limit exceeded for this account." });

            var validation = await _loginValidator.ValidateAsync(dto, cancellationToken);
            if (!validation.IsValid)
                return ApiValidationError("Validation failed.",
                    validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()));

            var result = await _authService.LoginAsync(dto, cancellationToken);
            Log.ForContext("LogType", "Security").Information("LOGIN - email={Email} - success roles={Roles}",
                dto.Email, string.Join(",", result.Roles));
            return ApiOk(result, "Login successful.");
        }
        catch (UnauthorizedAccessException ex)
        {
            // - Login sai: 5 lần / 15 phút / IP
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var ok = _quota.TryConsume($"auth:login:fail:ip:{ip}", 5, TimeSpan.FromMinutes(15), out _, out _);

            Log.ForContext("LogType", "Security").Error(ex, "LOGIN - email={Email} - failed - {Detail}", dto.Email, ex.Message);

            if (!ok)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Too many failed login attempts from this IP. Try again later." });

            return Unauthorized(new { success = false, message = "Invalid email or password." });
        }
        catch (Exception ex)
        {
            Log.ForContext("LogType", "Security").Error(ex, "LOGIN - email={Email} - failed - {Detail}", dto.Email, ex.Message);
            throw;
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // 2) Auth rules
            // - Login/refresh per account: 8 / 30 minutes / account
            // - Refresh token: 20 / hour / account (we approximate by refresh-token value for anonymous endpoint)
            var refreshKey = (dto.RefreshToken ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(refreshKey))
            {
                if (!_quota.TryConsume($"auth:refresh:token:{refreshKey}", 20, TimeSpan.FromHours(1), out _, out _))
                    return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Refresh token rate limit exceeded." });
            }

            var validation = await _refreshValidator.ValidateAsync(dto, cancellationToken);
            if (!validation.IsValid)
                return ApiValidationError("Validation failed.",
                    validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()));

            var result = await _authService.RefreshTokenAsync(dto, cancellationToken);
            Log.ForContext("LogType", "Security").Information("REFRESH - token - success");
            return ApiOk(result, "Token refreshed.");
        }
        catch (Exception ex)
        {
            Log.ForContext("LogType", "Security").Error(ex, "REFRESH - token - failed - {Detail}", ex.Message);
            throw;
        }
    }

    [HttpPost("logout")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = Request.Headers.Authorization.ToString();
            accessToken = accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? accessToken["Bearer ".Length..].Trim() : accessToken;
            var userId = GetUserId();
            var result = await _authService.LogoutAsync(userId, accessToken, dto.RefreshToken, cancellationToken);
            Log.ForContext("LogType", "Security").Information("LOGOUT - userId={UserId} - success", userId);
            return ApiOk(result, "Logout successful.");
        }
        catch (Exception ex)
        {
            Log.ForContext("LogType", "Security").Error(ex, "LOGOUT - failed - {Detail}", ex.Message);
            throw;
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        var validation = await _forgotValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ApiValidationError("Validation failed.", validation.ToDictionary());

        // Account-based: 8 / 30 minutes / account
        if (!_quota.TryConsume($"auth:forgot:acct:{dto.Email}".ToLowerInvariant(), 8, TimeSpan.FromMinutes(30), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Rate limit exceeded for this account." });

        // Generate token (dev-only return pattern)
        var token = await _authService.GeneratePasswordResetTokenAsync(dto.Email, cancellationToken);

        Log.ForContext("LogType", "Security").Information("FORGOT_PASSWORD - email={Email} - requested", dto.Email);

        var isDev = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        if (isDev)
            return ApiOk(new { sent = true, token }, "Reset token generated (development only).");

        return ApiOk(new { sent = true }, "If the email exists, a reset instruction has been sent.");
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        var validation = await _resetValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ApiValidationError("Validation failed.", validation.ToDictionary());

        // Account-based: 8 / 30 minutes / account (by email)
        if (!_quota.TryConsume($"auth:reset:acct:{dto.Email}".ToLowerInvariant(), 8, TimeSpan.FromMinutes(30), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Rate limit exceeded for this account." });

        // Token-based: 20 / hour / "account" (approx by token)
        if (!_quota.TryConsume($"auth:reset:token:{dto.Token}", 20, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Reset token rate limit exceeded." });

        await _authService.ResetPasswordAsync(dto, cancellationToken);
        Log.ForContext("LogType", "Security").Information("RESET_PASSWORD - email={Email} - success", dto.Email);
        return ApiOk(new { reset = true }, "Password reset successful.");
    }

    [HttpGet("me")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _authService.MeAsync(userId, cancellationToken);
        return ApiOk(result, "Profile retrieved.");
    }
}
