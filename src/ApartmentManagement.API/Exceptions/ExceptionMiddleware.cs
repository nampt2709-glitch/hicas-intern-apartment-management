using System.Net;
using System.Text.Json;
using Serilog;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using ApartmentManagement.Middlewares;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Exceptions;

public sealed class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Exception after response started; rethrowing.");
                throw;
            }

            context.Items["error_type"] = ex.GetType().Name;
            context.Items["error_detail"] = ex.Message;

            Log.ForContext("LogType", "Error")
                .Error(ex, "{ErrorType} - {ErrorDetail}", ex.GetType().Name, ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        switch (ex)
        {
            case UnauthorizedAccessException uax:
                await WriteErrorAsync(context, HttpStatusCode.Unauthorized, uax.Message);
                return;

            case KeyNotFoundException knf:
                await WriteErrorAsync(context, HttpStatusCode.NotFound, knf.Message);
                return;

            case ArgumentException ax:
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, ax.Message);
                return;

            case FormatException fx:
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, fx.Message);
                return;

            case JsonException:
                await WriteErrorAsync(context, HttpStatusCode.BadRequest,
                    "The request body is not valid JSON.");
                return;

            case InvalidOperationException iox:
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, iox.Message);
                return;

            case ValidationException vx:
                await WriteValidationAsync(context, vx.Errors);
                return;

            case AutoMapperMappingException amx:
                _logger.LogWarning(amx, "AutoMapper mapping failed");
                await WriteErrorAsync(context, HttpStatusCode.BadRequest,
                    "The request could not be mapped to the expected format.");
                return;

            case DbUpdateConcurrencyException:
                await WriteErrorAsync(context, HttpStatusCode.Conflict,
                    "The record was modified or deleted by another request. Refresh and try again.");
                return;

            case DbUpdateException dbx when DatabaseExceptionMapper.IsUniqueConstraintViolation(dbx):
                await WriteErrorAsync(context, HttpStatusCode.Conflict,
                    "A record with this value already exists.");
                return;

            case DbUpdateException dbx:
                _logger.LogWarning(dbx, "Database update failed");
                await WriteErrorAsync(context, HttpStatusCode.BadRequest,
                    "The data could not be saved. Please check your input and try again.");
                return;

            default:
                _logger.LogError(ex, "Unhandled exception");
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "Server error.");
                return;
        }
    }

    private static async Task WriteValidationAsync(HttpContext context, IEnumerable<ValidationFailure> failures)
    {
        var errors = failures
            .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "_" : e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        context.Items["error_type"] = "ValidationException";
        context.Items["error_detail"] = "Validation failed: " +
                                         string.Join("; ", errors.Select(kv =>
                                             $"{kv.Key}=[{string.Join(",", kv.Value)}]"));

        Log.ForContext("LogType", "Error")
            .Error("{ErrorType} - {ErrorDetail}", context.Items["error_type"], context.Items["error_detail"]);

        await WriteErrorAsync(context, HttpStatusCode.BadRequest, "Validation failed.", errors);
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        context.Response.Clear();
        context.Response.StatusCode = (int)code;
        context.Response.ContentType = "application/json";
        ResponseDiagnosticsHeaders.Apply(context);

        var body = new Dictionary<string, object?> { ["success"] = false, ["message"] = message };
        if (errors is { Count: > 0 })
            body["errors"] = errors;

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
