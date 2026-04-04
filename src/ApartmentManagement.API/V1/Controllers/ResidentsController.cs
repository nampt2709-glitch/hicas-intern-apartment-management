using ApartmentManagement.API.V1.Validators;
using ApartmentManagement.Infrastructure;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Residents;
using ApartmentManagement.API.V1.Interfaces.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApartmentManagement.API.V1.Controllers;

public sealed class ResidentsController : ApiControllerBase
{
    private readonly IResidentService _service;
    private readonly IValidator<ResidentCreateDto> _createValidator;
    private readonly IValidator<ResidentUpdateDto> _updateValidator;
    private readonly QuotaRateLimiter _quota;

    public ResidentsController(IResidentService service, IValidator<ResidentCreateDto> createValidator, IValidator<ResidentUpdateDto> updateValidator, QuotaRateLimiter quota)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _quota = quota;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetPagedAsync(query, cancellationToken), "Residents retrieved.");

    [HttpGet("me")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        => ApiOk(await _service.GetMineForUserAsync(GetUserId(), cancellationToken), "Resident retrieved.");

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => ApiOk(await _service.GetByIdAsync(id, cancellationToken: cancellationToken), "Resident retrieved.");

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Create([FromBody] ResidentCreateDto dto, CancellationToken cancellationToken)
    {
        // Admin create resident: 60/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"residents:create:{adminId:N}", 60, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Resident create rate limit exceeded." });

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiCreated(await _service.CreateAsync(dto, cancellationToken), "Resident created.");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ResidentUpdateDto dto, CancellationToken cancellationToken)
    {
        // Admin update resident: 120/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"residents:update:{adminId:N}", 120, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Resident update rate limit exceeded." });

        var vctx = new ValidationContext<ResidentUpdateDto>(dto);
        vctx.RootContextData[ValidationContextKeys.ResidentId] = id;
        var validation = await _updateValidator.ValidateAsync(vctx, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiOk(await _service.UpdateAsync(id, dto, cancellationToken), "Resident updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Admin delete resident: 20/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"residents:delete:{adminId:N}", 20, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Resident delete rate limit exceeded." });

        await _service.DeleteAsync(id, cancellationToken);
        return ApiDeleted("Resident deleted.");
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        // Admin restore resident: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"residents:restore:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Resident restore rate limit exceeded." });

        await _service.RestoreAsync(id, cancellationToken);
        return ApiOk(new { restored = true }, "Resident restored.");
    }
}
