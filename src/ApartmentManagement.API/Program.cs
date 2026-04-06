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

// =============================================================================
// Điểm vào ứng dụng ASP.NET Core: cấu hình DI, pipeline, JWT, rate limit, Swagger.
// Luồng: đọc cấu hình → đăng ký dịch vụ → build app → migrate DB → middleware → MapControllers.
// =============================================================================

// nạp biến môi trường từ .env (repo root / thư mục chạy). Docker Compose có thể inject trực tiếp.
DotEnvLoader.TryLoad();
DotEnvLoader.TryLoad(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

// kiểm tra chuỗi kết nối SQL Server bắt buộc (fail fast nếu thiếu cấu hình).
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing. Copy .env.example to .env at the repository root and set SA_PASSWORD / connection string (see README).");
}

// kiểm tra khóa JWT (tối thiểu 32 ký tự) để ký/verify token an toàn.
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be set and at least 32 characters. Set JWT__KEY in .env or User Secrets (see README).");
}

// cấu hình Serilog — tách log theo loại (Error / Activity / Security / Audit) vào file riêng, kèm CorrelationId.
builder.Host.UseSerilog((context, services, cfg) =>
{
    // Đường dẫn thư mục log: ưu tiên cấu hình (Docker gắn volume), mặc định cạnh DLL đã publish.
    var logsPath = context.Configuration["Logs:Path"]?.Trim();
    if (string.IsNullOrEmpty(logsPath))
        logsPath = Path.Combine(AppContext.BaseDirectory, "Logs");
    Directory.CreateDirectory(logsPath);

    const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] - [CorrId={CorrelationId}] - {Message}";

    cfg
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        // chỉ ghi LogType=Error vào Error.log (exception, input sai).
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Error'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Error.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate + "{NewLine}{Exception}")
        )
        // Activity.log — mọi request/response API (thành công hay lỗi).
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Activity'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Activity.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate)
        )
        // Security.log — hành vi liên quan xác thực (đăng nhập, token...).
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Security'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Security.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate)
        )
        // Audit.log — chỉ thao tác CRUD/xem thành công (phục vụ kiểm toán).
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("LogType = 'Audit'")
            .WriteTo.File(
                path: Path.Combine(logsPath, "Audit.log"),
                rollingInterval: RollingInterval.Infinite,
                outputTemplate: outputTemplate)
        );
});

// đăng ký API Controllers và versioning (URL segment v1), explorer cho Swagger nhóm theo phiên bản.
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

// chuẩn hóa phản hồi lỗi ModelState (validation tự động) thành JSON thống nhất + ghi log Error.
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
// FluentValidation — tự chạy validator theo assembly chứa LoginRequestDtoValidator.
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

// bind cấu hình strongly-typed (JWT, upload, rate limit toàn cục).
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection("Upload"));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

// HttpContext, cache bộ nhớ, giới hạn quota tùy chỉnh, metrics hiệu năng (singleton).
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<QuotaRateLimiter>();
builder.Services.AddSingleton<PerformanceMetricsService>();

// theo dõi request (scoped) và interceptor đếm lệnh SQL (scoped, gắn DbContext).
builder.Services.AddScoped<RequestMetrics>();
builder.Services.AddScoped<DbCommandCountingInterceptor>();

// đăng ký EF Core + SQL Server + interceptor; tắt log dữ liệu nhạy cảm trong production.
builder.Services.AddDbContext<ApartmentDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(sp.GetRequiredService<DbCommandCountingInterceptor>());
    options.EnableSensitiveDataLogging(false);
});

// tra cứu tồn tại entity (căn hộ, user...) phục vụ validator — scoped theo request.
builder.Services.AddScoped<IReferenceEntityLookup, ReferenceEntityLookup>();

// ASP.NET Identity — user/role, ràng buộc mật khẩu, lưu trữ qua ApartmentDbContext.
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

// đọc lại JwtSettings để cấu hình Bearer (khóa đối xứng ký JWT).
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
          ?? throw new InvalidOperationException("Jwt settings missing.");

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));

// xác thực JWT Bearer — validate issuer/audience/lifetime + kiểm tra token bị thu hồi.
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
        // Sau khi token hợp lệ: kiểm tra blacklist thu hồi (logout toàn cục / revoke).
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
        // Khi thiếu quyền: trả JSON 401 thống nhất + header chẩn đoán.
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

// mặc định mọi endpoint yêu cầu đăng nhập (trừ khi [AllowAnonymous]).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// 403 Forbidden trả JSON thay vì redirect/HTML (API-first).
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenJsonAuthorizationMiddlewareResultHandler>();

// ASP.NET Rate Limiter — các policy theo IP (auth, CRUD, upload ảnh/avatar).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Policy "auth": giới hạn endpoint đăng nhập theo IP (60/phút).
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

    // Policy CRUD chung theo IP: 300 request/phút (dùng cho endpoint ghi).
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

    // Policy upload ảnh căn hộ: 20 file/phút/IP.
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

    // Policy upload avatar: 20 file/phút/IP (quota theo tài khoản/giờ do QuotaRateLimiter xử lý thêm).
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

// AutoMapper — cấu hình MappingProfile một lần, assert hợp lệ khi khởi động.
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

// cache hybrid (memory + distributed nếu có), thu hồi token, telemetry upload, quét malware heuristic.
builder.Services.AddScoped<ICacheService, HybridCacheService>();
builder.Services.AddSingleton<ITokenRevocationService, TokenRevocationService>();

builder.Services.AddSingleton<UploadValidationTelemetry>();
builder.Services.AddScoped<IMalwareScanner, HeuristicMalwareScanner>();


// đăng ký dịch vụ nghiệp vụ (scoped — mỗi request một instance).
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUploadValidator, UploadValidatorService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IApartmentService, ApartmentService>();
builder.Services.AddScoped<IResidentService, ResidentService>();
builder.Services.AddScoped<IUtilityServiceService, UtilityServiceService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();

// repository truy cập dữ liệu (scoped, dùng chung DbContext trong request).
builder.Services.AddScoped<IApartmentRepository, ApartmentRepository>();
builder.Services.AddScoped<IResidentRepository, ResidentRepository>();
builder.Services.AddScoped<IUtilityServiceRepository, UtilityServiceRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// Swagger/OpenAPI + tùy chọn theo phiên bản API (ConfigureSwaggerOptions).
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

var app = builder.Build();

// áp dụng migration EF Core có retry (chờ SQL Server sẵn sàng trong Docker).
await DatabaseMigrator.ApplyMigrationsWithRetryAsync(app.Services);

// Khối pipeline HTTP (thứ tự quan trọng): CorrelationId sớm nhất để trace xuyên suốt.
app.UseMiddleware<CorrelationIdMiddleware>();
// thu thập metrics (thời gian/DB/cache), bắt exception toàn cục, đo thời gian phản hồi.
app.UseMiddleware<PerformanceMetricsMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<ResponseTimingMiddleware>();

// Swagger JSON + UI theo từng nhóm phiên bản API.
app.UseSwagger();
var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
app.UseSwaggerUI(options =>
{
    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName);
    options.DisplayRequestDuration();
});

// rate limit built-in → xác thực JWT → quota tùy chỉnh → log activity/audit Serilog → phân quyền.
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<GlobalQuotaRateLimitMiddleware>();
app.UseMiddleware<SerilogActivityAuditMiddleware>();
app.UseAuthorization();

// ánh xạ tất cả controller API (route theo attribute).
app.MapControllers();

// root redirect sang Swagger UI (anonymous, ẩn khỏi OpenAPI).
app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

// chạy Kestrel — chặn thread cho đến khi host dừng.
app.Run();