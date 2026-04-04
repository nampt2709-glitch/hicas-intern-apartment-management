using System.Security.Claims;
using ApartmentManagement.API.V1.DTOs.Common;
using Asp.Versioning;
using Serilog;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.API.V1.Controllers;

[ApiController]
[Route("api/v1.0/[controller]")]
[ApiVersion("1.0")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ApiOk<T>(T data, string message = "OK")
    {
        return Ok(new ApiResponseDto<T>
        {
            Success = true,
            Message = message,
            Data = data
        });
    }

    protected IActionResult ApiCreated<T>(T data, string message = "Created successfully.")
    {
        return StatusCode(StatusCodes.Status201Created, new ApiResponseDto<T>
        {
            Success = true,
            Message = message,
            Data = data
        });
    }

    protected IActionResult ApiDeleted(string message = "Deleted successfully.")
        => ApiOk(new { deleted = true }, message);

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

    protected Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
