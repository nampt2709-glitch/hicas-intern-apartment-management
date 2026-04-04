using ApartmentManagement.API.V1.Validators;
using ApartmentManagement.Infrastructure;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Services;
using ApartmentManagement.API.V1.Interfaces.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApartmentManagement.API.V1.Controllers;

public sealed class UtilityServicesController : ApiControllerBase
{
    private readonly IUtilityServiceService _service;
    private readonly IValidator<UtilityServiceCreateDto> _createValidator;
    private readonly IValidator<UtilityServiceUpdateDto> _updateValidator;
    private readonly QuotaRateLimiter _quota;

    public UtilityServicesController(IUtilityServiceService service, IValidator<UtilityServiceCreateDto> createValidator, IValidator<UtilityServiceUpdateDto> updateValidator, QuotaRateLimiter quota)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _quota = quota;
    }

    [HttpGet]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetPagedAsync(query, cancellationToken), "Services retrieved.");

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => ApiOk(await _service.GetByIdAsync(id, cancellationToken: cancellationToken), "Service retrieved.");

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Create([FromBody] UtilityServiceCreateDto dto, CancellationToken cancellationToken)
    {
        // Admin create utility service: 30/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"utility:create:{adminId:N}", 30, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Utility service create rate limit exceeded." });

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiCreated(await _service.CreateAsync(dto, cancellationToken), "Service created.");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UtilityServiceUpdateDto dto, CancellationToken cancellationToken)
    {
        // Admin update utility service: 60/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"utility:update:{adminId:N}", 60, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Utility service update rate limit exceeded." });

        var vctx = new ValidationContext<UtilityServiceUpdateDto>(dto);
        vctx.RootContextData[ValidationContextKeys.UtilityServiceId] = id;
        var validation = await _updateValidator.ValidateAsync(vctx, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiOk(await _service.UpdateAsync(id, dto, cancellationToken), "Service updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Admin delete utility service: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"utility:delete:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Utility service delete rate limit exceeded." });

        await _service.DeleteAsync(id, cancellationToken);
        return ApiDeleted("Service deleted.");
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        // Admin restore utility service: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"utility:restore:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Utility service restore rate limit exceeded." });

        await _service.RestoreAsync(id, cancellationToken);
        return ApiOk(new { restored = true }, "Service restored.");
    }
}
