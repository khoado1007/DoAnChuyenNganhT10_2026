using System.Security.Claims;
using System.Text;
using ITManagement.API.Responses;
using ITManagement.Domain.Enums;
using ITManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace ITManagement.API.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Đăng ký xác thực JWT và các tùy chọn kiểm tra token.</summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32 ||
            secret.StartsWith("CHANGE_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 bytes of non-placeholder secret material.");

        services.AddAuthentication(opts =>
        {
            opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opts =>
        {
            opts.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal;
                    if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                        || !int.TryParse(principal.FindFirstValue("security_version"), out var version))
                    { context.Fail("Invalid user session."); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, context.HttpContext.RequestAborted);
                    if (user is null || user.Status != UserStatus.Active || user.SecurityVersion != version
                        || user.Role.ToString() != principal.FindFirstValue(ClaimTypes.Role))
                        context.Fail("User session was revoked.");
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Vui lòng đăng nhập."));
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(ApiResponse.Fail("Bạn không có quyền thực hiện thao tác này."));
                }
            };
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    /// <summary>Đăng ký các chính sách phân quyền của ứng dụng.</summary>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
            opts.AddPolicy("TechnicianOrAdmin", p => p.RequireRole("Admin", "Technician"));
            opts.AddPolicy("AnyAuthenticated", p => p.RequireAuthenticatedUser());
        });

        return services;
    }

    /// <summary>Khai báo cơ chế Bearer token cho tài liệu OpenAPI.</summary>
    public static void AddBearerSecurityDefinition(this Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions c)
    {
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Format: Bearer {token}",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    }
}
