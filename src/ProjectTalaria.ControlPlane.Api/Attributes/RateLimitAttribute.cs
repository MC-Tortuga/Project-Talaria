using Microsoft.AspNetCore.Mvc.Filters;

namespace ProjectTalaria.ControlPlane.Api.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RateLimitAttribute(int maxRequests = 100, int windowSeconds = 60) : Attribute, IAsyncAuthorizationFilter
{
    private readonly int _maxRequests = maxRequests;
    private readonly int _windowSeconds = windowSeconds;
    private static readonly Dictionary<string, (int Count, DateTime WindowStart)> _requests = new();
    private static readonly object _lock = new();

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var clientId = context.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? context.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? "unknown";

        lock (_lock)
        {
            var now = DateTime.UtcNow;

            if (!_requests.TryGetValue(clientId, out var window))
            {
                window = (0, now);
            }

            if ((now - window.WindowStart).TotalSeconds > _windowSeconds)
            {
                window = (0, now);
                _requests[clientId] = window;
            }

            window.Count++;
            _requests[clientId] = window;

            if (window.Count > _maxRequests)
            {
                context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(
                    new { error = "Rate limit exceeded", retryAfter = _windowSeconds })
                {
                    StatusCode = 429
                };
            }
        }

        return Task.CompletedTask;
    }
}
