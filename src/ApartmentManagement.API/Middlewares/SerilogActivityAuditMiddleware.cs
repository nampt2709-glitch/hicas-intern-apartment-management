using System.Security.Claims;
using System.Text;
using Serilog;
using Serilog.Context;

namespace ApartmentManagement.Middlewares;

// Ghi Activity.log cho mọi API; Audit.log cho CRUD/view thành công (trừ auth); redact secret trong body JSON.
public sealed class SerilogActivityAuditMiddleware
{
    private readonly RequestDelegate _next;

    // True nếu path là API auth (ví dụ /api/v1.0/auth/...).
    private static bool IsAuthApiPath(string? path)
        => !string.IsNullOrEmpty(path)
           && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
           && path.Contains("/auth/", StringComparison.OrdinalIgnoreCase);

    private static string RedactSecrets(string json)
    {
        // Che password/token trong chuỗi JSON (regex đơn giản, không parse đầy đủ).
        if (string.IsNullOrWhiteSpace(json)) return json;

        var redacted = System.Text.RegularExpressions.Regex.Replace(
            json,
            "\"(password|newPassword|currentPassword|accessToken|refreshToken)\"\\s*:\\s*\"[^\"]*\"",
            "\"$1\":\"***\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return redacted;
    }

    public SerilogActivityAuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Đọc snippet body JSON (POST/PUT/PATCH) tối đa ~800 ký tự; bỏ qua đường /auth.
        var contentType = context.Request.ContentType ?? string.Empty;
        var isJson = contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
        var shouldCaptureJsonBody = isJson
            && (context.Request.Method == HttpMethods.Post ||
                context.Request.Method == HttpMethods.Put ||
                context.Request.Method == HttpMethods.Patch);

        if (shouldCaptureJsonBody && !IsAuthApiPath(context.Request.Path.Value))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            body = RedactSecrets(body);
            context.Items["AuditRequestBody"] = body.Length > 800 ? body[..800] : body;
            context.Request.Body.Position = 0;
        }

        // Sau khi response hoàn tất: log Activity; nếu thành công và không phải auth — log Audit kèm body snippet.
        // OnCompleted chạy sau khi CorrelationIdMiddleware đã dispose LogContext — phải PushProperty lại từ Items (hoặc TraceIdentifier).
        context.Response.OnCompleted(() =>
        {
            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var cid) && cid is string s && !string.IsNullOrWhiteSpace(s)
                ? s.Trim()
                : context.TraceIdentifier;

            using (LogContext.PushProperty("CorrelationId", correlationId ?? string.Empty))
            {
                var status = context.Response.StatusCode;
                var isSuccess = status >= 200 && status < 300;

                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var actor = context.User.Identity?.IsAuthenticated == true ? userId ?? "unknown" : "anonymous";

                var method = context.Request.Method;
                var path = context.Request.Path.Value ?? string.Empty;

                var errorDetail = context.Items.TryGetValue("error_detail", out var detailObj) && detailObj is not null
                    ? detailObj.ToString()
                    : $"HTTP {status}";

                var activityMessage = isSuccess
                    ? $"{method} {path} - actor={actor} - success"
                    : $"{method} {path} - actor={actor} - failed - {errorDetail}";

                Log.ForContext("LogType", "Activity").Information("{Message}", activityMessage);

                if (!isSuccess)
                    return Task.CompletedTask;

                if (IsAuthApiPath(path))
                    return Task.CompletedTask;

                if (!IsAuthApiPath(path) &&
                    (method is "GET" or "POST" or "PUT" or "DELETE"))
                {
                    var auditKind = method switch
                    {
                        "POST" => "CREATE",
                        "PUT" => "UPDATE",
                        "DELETE" => "DELETE",
                        "GET" => "VIEW",
                        _ => "ACTION"
                    };

                    var bodySnippet = context.Items.TryGetValue("AuditRequestBody", out var snippetObj) && snippetObj is not null
                        ? snippetObj.ToString() ?? string.Empty
                        : string.Empty;

                    var auditMessage = $"{auditKind} {method} {path} - actor={actor}" +
                                        (bodySnippet.Length > 0 ? $" - body={bodySnippet}" : string.Empty);

                    Log.ForContext("LogType", "Audit").Information("{Message}", auditMessage);
                }
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

