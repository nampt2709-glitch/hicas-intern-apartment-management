using ApartmentManagement.API.V1.Validators;
using ApartmentManagement.Infrastructure;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Invoices;
using ApartmentManagement.API.V1.Interfaces.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

// Mục đích file: REST API hóa đơn — Admin CRUD + khôi phục; cư dân xem danh sách hóa đơn của mình; quota và validation.

namespace ApartmentManagement.API.V1.Controllers;

// Controller hóa đơn — kế thừa ApiControllerBase.
public sealed class InvoicesController : ApiControllerBase
{
    private readonly IInvoiceService _service;
    private readonly IValidator<InvoiceCreateDto> _createValidator;
    private readonly IValidator<InvoiceUpdateDto> _updateValidator;
    private readonly QuotaRateLimiter _quota;

    // Phụ thuộc inject: IInvoiceService, validator tạo/cập nhật, QuotaRateLimiter cho thao tác Admin.
    public InvoicesController(IInvoiceService service, IValidator<InvoiceCreateDto> createValidator, IValidator<InvoiceUpdateDto> updateValidator, QuotaRateLimiter quota)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _quota = quota;
    }

    // GET danh sách hóa đơn phân trang (Admin).
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetPagedAsync(query, cancellationToken), "Invoices retrieved.");

    // GET hóa đơn của cư dân đang đăng nhập (phân trang theo user).
    [HttpGet("me")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GetMine([FromQuery] PaginationQueryDto query, CancellationToken cancellationToken)
        => ApiOk(await _service.GetMineForResidentAsync(query, GetUserId(), cancellationToken), "Invoices retrieved.");

    // GET chi tiết hóa đơn theo id (Admin).
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => ApiOk(await _service.GetByIdAsync(id, cancellationToken: cancellationToken), "Invoice retrieved.");

    // POST tạo hóa đơn (Admin) — quota, validate, 201.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Create([FromBody] InvoiceCreateDto dto, CancellationToken cancellationToken)
    {
        // Admin create invoice: 120/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"invoices:create:{adminId:N}", 120, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Invoice create rate limit exceeded." });

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiCreated(await _service.CreateAsync(dto, cancellationToken), "Invoice created.");
    }

    // PUT cập nhật hóa đơn (Admin) — quota, validation có InvoiceId trong context.
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Update(Guid id, [FromBody] InvoiceUpdateDto dto, CancellationToken cancellationToken)
    {
        // Admin update invoice: 60/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"invoices:update:{adminId:N}", 60, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Invoice update rate limit exceeded." });

        var vctx = new ValidationContext<InvoiceUpdateDto>(dto);
        vctx.RootContextData[ValidationContextKeys.InvoiceId] = id;
        var validation = await _updateValidator.ValidateAsync(vctx, cancellationToken);
        if (!validation.IsValid) return ApiValidationError("Validation failed.", validation.ToDictionary());
        return ApiOk(await _service.UpdateAsync(id, dto, cancellationToken), "Invoice updated.");
    }

    // DELETE xóa mềm hóa đơn (Admin) — quota.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Admin delete invoice: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"invoices:delete:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Invoice delete rate limit exceeded." });

        await _service.DeleteAsync(id, cancellationToken);
        return ApiDeleted("Invoice deleted.");
    }

    // POST khôi phục hóa đơn đã xóa mềm (Admin) — quota.
    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("crud-ip-300-per-min")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        // Admin restore invoice: 10/hour/admin
        var adminId = GetUserId();
        if (!_quota.TryConsume($"invoices:restore:{adminId:N}", 10, TimeSpan.FromHours(1), out _, out _))
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = "Invoice restore rate limit exceeded." });

        await _service.RestoreAsync(id, cancellationToken);
        return ApiOk(new { restored = true }, "Invoice restored.");
    }
}
