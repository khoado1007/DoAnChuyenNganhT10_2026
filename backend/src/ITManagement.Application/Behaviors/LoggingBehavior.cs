using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ITManagement.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior: log thời gian xử lý mỗi request.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>Khởi tạo behavior ghi nhật ký cho yêu cầu MediatR.</summary>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>Ghi nhật ký trước và sau khi xử lý yêu cầu.</summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("→ Handling {RequestName}", requestName);

        var response = await next(cancellationToken);

        sw.Stop();
        _logger.LogInformation("← Handled {RequestName} in {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);

        return response;
    }
}
