using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.Infrastructure;
using ApartmentManagement.API.V1.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApartmentManagement.API.V1.Controllers;

public sealed class UsersController : ApiControllerBase
{
    private readonly IUserService _service;
    private readonly IAuthService _authService;
    private readonly IValidator<CreateUserRequestDto> _createUserValidator;
    private readonly IValidator<UpdateMyProfileDto> _updateMeValidator;
    private readonly IValidator<CurrentUserDto> _updateUserValidator;
    private readonly QuotaRateLimiter _quota;

    public UsersController(
        IUserService service,
        IAuthService authService,
        IValidator<CreateUserRequestDto> createUserValidator,
        IValidator<UpdateMyProfileDto> updateMeValidator,
        IValidator<CurrentUserDto> updateUserValidator,
        QuotaRateLimiter quota)
    {
        _service = service;
        _authService = authService;
        _createUserValidator = createUserValidator;
        _updateMeValidator = updateMeValidator;
        _updateUserValidator = updateUserValidator;
        _quota = quota;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequestDto dto, CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        if (!_quota.TryConsume($"users:create:{adminId:N}", 60, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "User create rate limit exceeded." });

        var validation = await _createUserValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ApiValidationError("Validation failed.", validation.ToDictionary());

        try
        {
            var created = await _service.CreateAsync(dto, cancellationToken);
            return ApiCreated(created, "User created.");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetPagedAsync(query, cancellationToken), "Users retrieved.");

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => ApiOk(await _service.GetByIdAsync(id, cancellationToken), "User retrieved.");

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CurrentUserDto dto, CancellationToken cancellationToken)
    {
        if (dto.UserId != id)
            return ApiValidationError("UserId in the request body must match the id in the URL.");
        var vctx = new ValidationContext<CurrentUserDto>(dto);
        vctx.RootContextData[ValidationContextKeys.RouteUserId] = id;
        var validation = await _updateUserValidator.ValidateAsync(vctx, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiOk(await _service.UpdateAsync(id, dto, cancellationToken), "User updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return ApiDeleted("User deleted.");
    }

    [HttpGet("me")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => ApiOk(await _authService.MeAsync(GetUserId(), cancellationToken), "Current user retrieved.");

    [HttpPut("me")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMyProfileDto dto, CancellationToken cancellationToken)
    {
        // Rate limit: update own profile 10 times / 7 days / account
        var userId = GetUserId();
        if (!_quota.TryConsume($"users:me:update:{userId:N}", 10, TimeSpan.FromDays(7), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Profile update rate limit exceeded." });

        // Change password (when provided): rate limit separately
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (!_quota.TryConsume($"users:me:change-password:{userId:N}", 5, TimeSpan.FromHours(1), out _, out _))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Change password rate limit exceeded." });
        }

        var validation = await _updateMeValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiOk(await _service.UpdateMeAsync(userId, dto, cancellationToken), "Profile updated.");
    }

    [HttpPut("{id:guid}/password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] ResetUserPasswordDto dto, CancellationToken cancellationToken)
        => ApiOk(await _service.AdminResetPasswordAsync(id, dto, cancellationToken), "Password updated.");

    /// <summary>Permanently removes the account and related refresh tokens / authored feedbacks / resident links. Use for test cleanup or GDPR-style removal.</summary>
    [HttpDelete("{id:guid}/purge")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken)
    {
        var adminId = GetUserId();
        if (!_quota.TryConsume($"users:purge:{adminId:N}", 40, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "User purge rate limit exceeded." });

        try
        {
            await _service.PurgeUserAsync(id, cancellationToken);
            return ApiOk(new { purged = true }, "User permanently removed.");
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "User not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
