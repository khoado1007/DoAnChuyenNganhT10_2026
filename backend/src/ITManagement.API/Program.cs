using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ITManagement.API.Extensions;
using ITManagement.API.Middleware;
using ITManagement.API.Responses;
using ITManagement.Application;
using ITManagement.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// API services
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(item => JsonNamingPolicy.CamelCase.ConvertName(item.Key),
                item => item.Value!.Errors.Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                    ? "Giá trị không hợp lệ." : error.ErrorMessage).ToArray());
        return new BadRequestObjectResult(ApiResponse.Fail("Vui lòng kiểm tra dữ liệu nhập.", errors));
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "IT Management API", Version = "v1" });
    c.AddBearerSecurityDefinition();
    c.AddSecurityDefinition("Agent", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Agent credential only. Format: Agent {credential}; user JWT is not accepted."
    });
    c.OperationFilter<AgentSecurityOperationFilter>();
});

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorizationPolicies();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
        await context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Fail("Quá nhiều yêu cầu; vui lòng thử lại sau."), ct);
    options.AddPolicy("agent-enrollment", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("agent-heartbeat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Migration và seed chỉ chạy khi bật rõ; mặc định người dùng quản lý bằng PMC.
if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
    await app.SeedDatabaseAsync();

app.Run();

public partial class Program { } // Phục vụ integration testing
