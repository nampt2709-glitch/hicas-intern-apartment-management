using System.Security.Claims;
using ApartmentManagement.API.V1.DTOs.Common;
using Asp.Versioning;
using Serilog;
using Microsoft.AspNetCore.Mvc;

// Mục đích file: Lớp cơ sở cho controller API v1 — định tuyến api/v1.0, chuẩn hóa body phản hồi (ApiResponseDto), lỗi validation và đọc UserId từ JWT/claims.

namespace ApartmentManagement.API.V1.Controllers;

[ApiController]
[Route("api/v1.0/[controller]")]
[ApiVersion("1.0")]
// ApiControllerBase — nền tảng chung; các controller kế thừa dùng ApiOk/ApiCreated/GetUserId thay vì tự ghép JSON.
public abstract class ApiControllerBase : ControllerBase
{
    // Trả về 200 với ApiResponseDto (thành công + message + data).
    protected IActionResult ApiOk<T>(T data, string message = "OK")
    {
        return Ok(new ApiResponseDto<T>
        {
            Success = true,
            Message = message,
            Data = data
        });
    }

    // Trả về 201 Created với ApiResponseDto (tạo mới thành công).
    protected IActionResult ApiCreated<T>(T data, string message = "Created successfully.")
    {
        return StatusCode(StatusCodes.Status201Created, new ApiResponseDto<T>
        {
            Success = true,
            Message = message,
            Data = data
        });
    }

    // Phản hồi xóa mềm/thành công dạng { deleted: true } qua ApiOk.
    protected IActionResult ApiDeleted(string message = "Deleted successfully.")
        => ApiOk(new { deleted = true }, message);

    // 400 BadRequest — ghi log Serilog khi có chi tiết lỗi từng field (errors).
    protected IActionResult ApiValidationError(string message, IDictionary<string, string[]>? errors = null)
    {
        if (errors is { Count: > 0 })
        {
            var detail = string.Join("; ", errors.Select(kv => $"{kv.Key}=[{string.Join(",", kv.Value)}]"));
            Log.ForContext("LogType", "Error").Error("ApiValidationError - {Message} - {Detail}", message, detail);
        }
        else
        {
            Log.ForContext("LogType", "Error").Error("ApiValidationError - {Message}", message);
        }

        return BadRequest(new { success = false, message, errors });
    }

    // Lấy Guid người dùng hiện tại từ claim NameIdentifier hoặc sub; không parse được thì Guid.Empty.
    protected Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
