using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ApartmentManagement.Middlewares;

// Khi đã đăng nhập nhưng không đủ quyền (sai role): trả JSON 403 có message thay vì body trống.
public sealed class ForbiddenJsonAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy? policy,
        PolicyAuthorizationResult authorizeResult)
    {
        // Thành công: ủy quyền handler mặc định của ASP.NET Core.
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy!, authorizeResult);
            return;
        }

        // Forbidden + chưa ghi response: trả JSON có gợi ý role cần (nếu suy ra được).
        if (authorizeResult.Forbidden && !context.Response.HasStarted)
        {
            var roleHint = TryFormatRoleHint(authorizeResult.AuthorizationFailure);
            var message =
                string.IsNullOrEmpty(roleHint)
                    ? "You do not have permission to access this resource. Your account may be missing a required role (for example Admin for administrative endpoints)."
                    : $"You do not have permission to access this resource. Required: {roleHint}.";

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            ResponseDiagnosticsHeaders.Apply(context);

            await context.Response.WriteAsJsonAsync(
                new Dictionary<string, object?>
                {
                    ["success"] = false,
                    ["message"] = message,
                    ["code"] = "forbidden"
                },
                JsonOptions);
            return;
        }

        // Các trường hợp khác (401, challenge...) — xử lý theo mặc định.
        await _defaultHandler.HandleAsync(next, context, policy!, authorizeResult);
    }

    // Trích danh sách role từ RolesAuthorizationRequirement để hiển thị trong message.
    private static string? TryFormatRoleHint(AuthorizationFailure? failure)
    {
        if (failure?.FailedRequirements == null)
            return null;

        foreach (var req in failure.FailedRequirements)
        {
            if (req is RolesAuthorizationRequirement rolesReq)
            {
                var roles = rolesReq.AllowedRoles.ToList();
                if (roles.Count > 0)
                    return "role(s): " + string.Join(", ", roles);
            }
        }

        return null;
    }
}
