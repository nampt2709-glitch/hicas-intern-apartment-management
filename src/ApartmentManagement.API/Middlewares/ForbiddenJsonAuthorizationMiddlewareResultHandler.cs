using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ApartmentManagement.Middlewares;

/// <summary>
/// When the user is authenticated but fails authorization (e.g. wrong role), return JSON instead of an empty 403 body.
/// </summary>
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
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy!, authorizeResult);
            return;
        }

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

        await _defaultHandler.HandleAsync(next, context, policy!, authorizeResult);
    }

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
