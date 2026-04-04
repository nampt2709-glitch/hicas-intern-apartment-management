using ApartmentManagement.API.V1.Validators;
using ApartmentManagement.Infrastructure;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Feedbacks;
using ApartmentManagement.API.V1.Interfaces.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.API.V1.Controllers;

public sealed class FeedbacksController : ApiControllerBase
{
    private readonly IFeedbackService _service;
    private readonly IValidator<FeedbackCreateDto> _createValidator;
    private readonly QuotaRateLimiter _quota;

    public FeedbacksController(IFeedbackService service, IValidator<FeedbackCreateDto> createValidator, QuotaRateLimiter quota)
    {
        _service = service;
        _createValidator = createValidator;
        _quota = quota;
    }

    [HttpGet]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetPagedAsync(query, GetUserId(), User.IsInRole("Admin"), cancellationToken), "Feedbacks retrieved.");

    [HttpGet("me")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetMine([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetMyPostsPagedAsync(query, GetUserId(), cancellationToken), "Your feedbacks retrieved.");

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => ApiOk(await _service.GetByIdAsync(id, cancellationToken: cancellationToken), "Feedback retrieved.");

    [HttpGet("tree")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Tree([FromQuery] Guid? rootFeedbackId, CancellationToken cancellationToken)
    {
        // Tree / flattened: 20 / minute / user
        var userId = GetUserId();
        if (!User.IsInRole("Admin"))
        {
            if (!_quota.TryConsume($"feedbacks:tree:{userId:N}", 20, TimeSpan.FromMinutes(1), out _, out _))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Feedback tree rate limit exceeded." });
        }
        return ApiOk(await _service.GetTreeAsync(rootFeedbackId, userId, User.IsInRole("Admin"), cancellationToken), "Feedback tree retrieved.");
    }

    [HttpGet("flattened")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Flattened([FromQuery] Guid? rootFeedbackId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!User.IsInRole("Admin"))
        {
            if (!_quota.TryConsume($"feedbacks:flattened:{userId:N}", 20, TimeSpan.FromMinutes(1), out _, out _))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Feedback flattened rate limit exceeded." });
        }

        return ApiOk(await _service.GetFlattenedAsync(rootFeedbackId, userId, User.IsInRole("Admin"), cancellationToken), "Feedback tree flattened.");
    }

    [HttpPost]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Create([FromBody] FeedbackCreateDto dto, CancellationToken cancellationToken)
    {
        // Create feedback/comment: 30 / minute / user (admins follow global admin limit; still allow)
        var userId = GetUserId();
        if (!User.IsInRole("Admin"))
        {
            if (!_quota.TryConsume($"feedbacks:create:{userId:N}", 30, TimeSpan.FromMinutes(1), out _, out _))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Feedback create rate limit exceeded." });
        }

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiCreated(await _service.CreateAsync(dto, userId, User.IsInRole("Admin"), cancellationToken), "Feedback created.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Delete feedback/comment: 10 / hour / user
        var userId = GetUserId();
        if (!User.IsInRole("Admin"))
        {
            if (!_quota.TryConsume($"feedbacks:delete:{userId:N}", 10, TimeSpan.FromHours(1), out _, out _))
                return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Feedback delete rate limit exceeded." });
        }

        await _service.DeleteAsync(id, userId, User.IsInRole("Admin"), cancellationToken);
        return ApiDeleted("Feedback deleted.");
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        // Admin restore feedback: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"feedbacks:restore:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Feedback restore rate limit exceeded." });

        await _service.RestoreAsync(id, cancellationToken);
        return ApiOk(new { restored = true }, "Feedback restored.");
    }
}
