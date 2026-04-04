using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApartmentManagement.API.V1.Controllers;

public sealed class UploadsController : ApiControllerBase
{
    private readonly IUploadValidator _uploadValidator;
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly QuotaRateLimiter _quota;

    public UploadsController(IUploadValidator uploadValidator, IAuthService authService, IUserService userService, QuotaRateLimiter quota)
    {
        _uploadValidator = uploadValidator;
        _authService = authService;
        _userService = userService;
        _quota = quota;
    }

    [HttpPost("avatar")]
    [Authorize(Roles = "User,Admin")]
    [EnableRateLimiting("avatar-upload-20-per-min-ip")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        // Rate limit: change avatar 5/hour/account
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { success = false, message = "Invalid or missing user identity." });

        if (!_quota.TryConsume($"uploads:avatar:acct:{userId:N}", 5, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Avatar upload rate limit exceeded for this account." });

        await _uploadValidator.ValidateRequestAsync(Request.Form.Files, cancellationToken);
        var result = await _uploadValidator.SaveUserAvatarAsync(file, userId, cancellationToken);
        var current = await _authService.MeAsync(userId, cancellationToken);
        current.AvatarPath = result.FilePath;
        await _userService.UpdateAsync(userId, current, cancellationToken);
        return ApiOk(result, "Avatar uploaded.");
    }
}
