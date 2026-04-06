using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApartmentManagement.Settings;

// Cấu hình Swagger: một tài liệu mỗi nhóm phiên bản API + security scheme Bearer JWT.
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        // SwaggerDoc theo từng ApiVersionDescription từ ApiExplorer.
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Apartment Management API",
                Version = description.ApiVersion.ToString(),
                Description = BuildDescription(description)
            });
        }

        // nút Authorize trong UI — header Authorization: Bearer <JWT>.
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter JWT token"
        });

        // Áp security requirement mặc định cho mọi operation (client gửi JWT).
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    }

    private static string BuildDescription(ApiVersionDescription description)
    {
        if (description.IsDeprecated)
            return "This API version is deprecated.";
        return "Version 1.0 HTTP API. Base path: /api/v1.0/ (e.g. /api/v1.0/auth/login). Add V1.1, V1.2 folders for future minor versions.";
    }
}
