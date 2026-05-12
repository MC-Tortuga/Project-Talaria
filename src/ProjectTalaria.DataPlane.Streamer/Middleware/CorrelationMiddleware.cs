using System.Diagnostics;

namespace ProjectTalaria.DataPlane.Streamer.Middleware;

public class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string CorrelationIdKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        }

        context.Items[CorrelationIdKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        var routePattern = context.GetEndpoint() is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText
            : context.Request.Path.ToString();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Request {Method} {Route} completed with {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                context.Request.Method,
                routePattern,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
    }
}