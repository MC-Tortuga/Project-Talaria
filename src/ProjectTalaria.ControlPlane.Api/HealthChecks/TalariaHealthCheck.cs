using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTalaria.Infrastructure.Data;

namespace ProjectTalaria.ControlPlane.Api.HealthChecks;

public class TalariaDbHealthCheck(TalariaDbContext dbContext, ILogger<TalariaDbHealthCheck> logger) : IHealthCheck
{
    private readonly TalariaDbContext _dbContext = dbContext;
    private readonly ILogger<TalariaDbHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database connection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}

public class StorageHealthCheck(IConfiguration configuration, ILogger<StorageHealthCheck> logger) : IHealthCheck
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<StorageHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var useLocal = _configuration["Storage:UseLocal"]?.ToLower() == "true";

            if (useLocal)
            {
                var localPath = _configuration["Storage:LocalPath"] ?? "statements";
                if (Directory.Exists(localPath))
                    return HealthCheckResult.Healthy("Local storage accessible");

                return HealthCheckResult.Degraded("Local storage directory not found");
            }

            return HealthCheckResult.Healthy("Remote storage (S3) - connectivity assumed ok");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health check failed");
            return HealthCheckResult.Unhealthy("Storage check failed", ex);
        }
    }
}
