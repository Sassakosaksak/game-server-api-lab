using System.Diagnostics;

namespace GameServerApi.Logging;

public sealed class HttpRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpRequestLoggingMiddleware> _logger;

    public HttpRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<HttpRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            GameLog.RequestFailed(
                _logger,
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path,
                exception);
            throw;
        }
        finally
        {
            GameLog.RequestCompleted(
                _logger,
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
