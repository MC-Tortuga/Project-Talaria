using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTalaria.Domain.Interfaces;
using ProjectTalaria.Infrastructure.Data;

namespace ProjectTalaria.DataPlane.Streamer.HealthChecks;

public class StreamerDbHealthCheck(TalariaDbContext dbContext,
        IStorageProvider storageProvider,
        IConfiguration configuration,
        ILogger<StreamerDbHealthCheck> logger) : IHealthCheck
{
    private readonly TalariaDbContext _dbContext = dbContext;
    private readonly IStorageProvider _storageProvider = storageProvider;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<StreamerDbHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync(cancellationToken);

            var useLocalStorage = _configuration["Storage:UseLocal"]?.ToLower() == "true";
            if (!useLocalStorage)
            {
                await _storageProvider.GetFileStreamAsync("health-check", cancellationToken);
            }

            return HealthCheckResult.Healthy("Database and storage are reachable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data plane health check failed");
            return HealthCheckResult.Unhealthy("Data plane dependency check failed", ex);
        }
    }
}
