using ApartmentManagement.API.V1.Validators;
using ApartmentManagement.Infrastructure;
using ApartmentManagement.API.V1.DTOs.Apartments;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.Interfaces.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ApartmentManagement.API.V1.Controllers;

public sealed class ApartmentsController : ApiControllerBase
{
    private readonly IApartmentService _service;
    private readonly IUploadValidator _uploadValidator;
    private readonly IValidator<ApartmentCreateDto> _createValidator;
    private readonly IValidator<ApartmentUpdateDto> _updateValidator;
    private readonly QuotaRateLimiter _quota;

    public ApartmentsController(
        IApartmentService service,
        IUploadValidator uploadValidator,
        IValidator<ApartmentCreateDto> createValidator,
        IValidator<ApartmentUpdateDto> updateValidator,
        QuotaRateLimiter quota)
    {
        _service = service;
        _uploadValidator = uploadValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _quota = quota;
    }

    [HttpGet]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetPagedAsync(query, cancellationToken), "Apartments retrieved.");

    [HttpGet("me")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        => ApiOk(await _service.GetMineForResidentAsync(GetUserId(), cancellationToken), "Apartment retrieved.");

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => ApiOk(await _service.GetByIdAsync(id, cancellationToken: cancellationToken), "Apartment retrieved.");

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Create([FromBody] ApartmentCreateDto dto, CancellationToken cancellationToken)
    {
        // Admin create apartment: 30/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"apartments:create:{adminId:N}", 30, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Apartment create rate limit exceeded." });

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiCreated(await _service.CreateAsync(dto, cancellationToken), "Apartment created.");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ApartmentUpdateDto dto, CancellationToken cancellationToken)
    {
        // Admin update apartment: 60/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"apartments:update:{adminId:N}", 60, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Apartment update rate limit exceeded." });

        var vctx = new ValidationContext<ApartmentUpdateDto>(dto);
        vctx.RootContextData[ValidationContextKeys.ApartmentId] = id;
        var validation = await _updateValidator.ValidateAsync(vctx, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiOk(await _service.UpdateAsync(id, dto, cancellationToken), "Apartment updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Admin delete apartment: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"apartments:delete:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Apartment delete rate limit exceeded." });

        await _service.DeleteAsync(id, cancellationToken);
        return ApiDeleted("Apartment deleted.");
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        // Admin restore apartment: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"apartments:restore:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Apartment restore rate limit exceeded." });

        await _service.RestoreAsync(id, cancellationToken);
        return ApiOk(new { restored = true }, "Apartment restored.");
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("apartment-images-20-per-min-ip")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await _uploadValidator.ValidateRequestAsync(Request.Form.Files, cancellationToken);
        var result = await _uploadValidator.SaveApartmentImageAsync(file, id, cancellationToken);
        await _service.AttachUploadedImageAsync(id, result.FilePath, file.FileName, file.ContentType ?? "application/octet-stream", cancellationToken);
        return ApiOk(result, "Image uploaded.");
    }
}
