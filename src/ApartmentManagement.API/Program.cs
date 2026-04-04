using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.IO;
using Serilog;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using ApartmentManagement.Settings;
using ApartmentManagement.Middlewares;
using ApartmentManagement.Exceptions;
using ApartmentManagement.API.V1.Validators;
using ApartmentManagement.Infrastructure;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Performance;
using ApartmentManagement.Utilities;
using ApartmentManagement.API.V1.Mapping;
using ApartmentManagement.API.V1.Repositories;
using ApartmentManagement.API.V1.Services;
using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

// Load secrets from repo-root or project .env (gitignored). Docker Compose injects env vars instead.
DotEnvLoader.TryLoad();
DotEnvLoader.TryLoad(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing. Copy .env.example to .env at the repository root and set SA_PASSWORD / connection string (see README).");
}

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be set and at least 32 characters. Set JWT__KEY in .env or User Secrets (see README).");
}

builder.Host.UseSerilog((context, services, cfg) =>
{
    // Prefer configured path (Docker: Logs__Path=/app/Logs matches volume). Fallback: next to published DLL.
    var logsPath = context.Configuration["Logs:Path"]?.Trim();
    if (string.IsNullOrEmpty(logsPath))
        logsPath = Path.Combine(AppContext.BaseDirectory, "Logs");
    Directory.CreateDirectory(logsPath);

    const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] - [CorrId={CorrelationId}] - {Message}";

    cfg
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        // Error.log: exceptions + invalid input (must include datetime, error name, details)
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Error'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Error.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate + "{NewLine}{Exception}")
        )
        // Activity.log: every request/response for /api (success or failure)
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Activity'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Activity.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate)
        )
        // Security.log: authentication-related actions
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Security'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Security.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate)
        )
        // Audit.log: successful CRUD/view actions only
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Audit'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Audit.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate)
        );
});

builder.Services.AddControllers();
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'M.m";
        options.SubstituteApiVersionInUrl = false;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value is { Errors.Count: > 0 })
            .ToDictionary(
                x => string.IsNullOrEmpty(x.Key) ? "_" : x.Key,
                x => x.Value!.Errors
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                    .ToArray());

        var errorDetail = string.Join("; ", errors.Select(kv => $"{kv.Key}=[{string.Join(",", kv.Value)}]"));
        Log.ForContext("LogType", "Error").Error("ValidationException - {ErrorDetail}", errorDetail);

        return new BadRequestObjectResult(new
        {
            success = false,
            message = "Validation failed.",
            errors
        });
    };
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection("Upload"));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<QuotaRateLimiter>();
builder.Services.AddSingleton<PerformanceMetricsService>();

builder.Services.AddScoped<RequestMetrics>();
builder.Services.AddScoped<DbCommandCountingInterceptor>();

builder.Services.AddDbContext<ApartmentDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(sp.GetRequiredService<DbCommandCountingInterceptor>());
    options.EnableSensitiveDataLogging(false);
});

builder.Services.AddScoped<IReferenceEntityLookup, ReferenceEntityLookup>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApartmentDbContext>()
.AddDefaultTokenProviders();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
          ?? throw new InvalidOperationException("Jwt settings missing.");

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = key,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                context.Fail("Authorization header is missing.");
                return;
            }

            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : authHeader.Trim();

            var revocation = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
            if (await revocation.IsAccessTokenRevokedAsync(token, context.HttpContext.RequestAborted))
            {
                context.Fail("Token revoked.");
            }
        },
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            ResponseDiagnosticsHeaders.Apply(context.HttpContext);
            var payload = JsonSerializer.Serialize(
                new Dictionary<string, object?> { ["success"] = false, ["message"] = "You are not authorized to access this resource." },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return context.Response.WriteAsync(payload);
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenJsonAuthorizationMiddlewareResultHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Basic auth policy used by AuthController attribute.
    options.AddPolicy("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // CRUD per IP (generic): 300 req/min/IP (use on write endpoints)
    options.AddPolicy("crud-ip-300-per-min", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"crud:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // Apartment image upload: 20 file/min/IP
    options.AddPolicy("apartment-images-20-per-min-ip", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"aptimg:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // Upload avatar: 20 file/min/IP (account/hour limit handled by QuotaRateLimiter)
    options.AddPolicy("avatar-upload-20-per-min-ip", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"avip:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddSingleton<MapperConfiguration>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    var config = new MapperConfiguration(cfg =>
    {
        cfg.AddProfile<MappingProfile>();
    }, loggerFactory);

    config.AssertConfigurationIsValid();
    return config;
});

builder.Services.AddSingleton<IMapper>(sp =>
    sp.GetRequiredService<MapperConfiguration>().CreateMapper());

builder.Services.AddScoped<ICacheService, HybridCacheService>();
builder.Services.AddSingleton<ITokenRevocationService, TokenRevocationService>();

builder.Services.AddSingleton<UploadValidationTelemetry>();
builder.Services.AddScoped<IMalwareScanner, HeuristicMalwareScanner>();


builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUploadValidator, UploadValidatorService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IApartmentService, ApartmentService>();
builder.Services.AddScoped<IResidentService, ResidentService>();
builder.Services.AddScoped<IUtilityServiceService, UtilityServiceService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();

builder.Services.AddScoped<IApartmentRepository, ApartmentRepository>();
builder.Services.AddScoped<IResidentRepository, ResidentRepository>();
builder.Services.AddScoped<IUtilityServiceRepository, UtilityServiceRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

var app = builder.Build();

await DatabaseMigrator.ApplyMigrationsWithRetryAsync(app.Services);

// Correlation id must be established as early as possible in the pipeline.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<PerformanceMetricsMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<ResponseTimingMiddleware>();

app.UseSwagger();
var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
app.UseSwaggerUI(options =>
{
    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName);
    options.DisplayRequestDuration();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<GlobalQuotaRateLimitMiddleware>();
app.UseMiddleware<SerilogActivityAuditMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

app.Run();