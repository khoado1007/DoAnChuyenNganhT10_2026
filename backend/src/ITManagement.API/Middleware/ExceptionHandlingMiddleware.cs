using System.Text.Json;
using ITManagement.API.Responses;
using ITManagement.Application.Common.Exceptions;
using ITManagement.Domain.Exceptions;

namespace ITManagement.API.Middleware;

/// <summary>
/// Global exception handler — chuyển mọi exception thành ApiResponse chuẩn.
/// Không để lộ stack trace ra ngoài trên production.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    /// <summary>Khởi tạo middleware xử lý ngoại lệ cho yêu cầu HTTP.</summary>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    /// <summary>Chuyển tiếp yêu cầu và chuyển ngoại lệ thành phản hồi API phù hợp.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    // Ánh xạ ngoại lệ sang mã trạng thái và nội dung phản hồi HTTP.
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        context.Response.ContentType = "application/json";

        (int statusCode, string message, IReadOnlyDictionary<string, string[]>? errors) = exception switch
        {
            SimulationException se => (se.StatusCode, se.Message, new Dictionary<string, string[]> { ["code"] = [se.Code] }),
            AuthenticationRequiredException => (StatusCodes.Status401Unauthorized, exception.Message, null),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message, null),
            UnauthorizedException => (StatusCodes.Status403Forbidden, exception.Message, null),
            Domain.Exceptions.ValidationException ve => (StatusCodes.Status422UnprocessableEntity, exception.Message, ve.Errors),
            QuotaExceededException => (StatusCodes.Status413RequestEntityTooLarge, exception.Message, null),
            BusinessRuleException => (StatusCodes.Status400BadRequest, exception.Message, null),
            _ => (StatusCodes.Status500InternalServerError,
                  _env.IsDevelopment() ? exception.Message : "Đã xảy ra lỗi nội bộ. Vui lòng thử lại sau.",
                  null)
        };

        context.Response.StatusCode = statusCode;

        var response = ApiResponse.Fail(message, errors);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await context.Response.WriteAsync(json);
    }
}
