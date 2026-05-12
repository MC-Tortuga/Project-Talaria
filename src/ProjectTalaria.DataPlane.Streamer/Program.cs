using System.Diagnostics;
using Amazon.S3;
using Amazon.KeyManagementService;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTalaria.Domain.Interfaces;
using ProjectTalaria.Infrastructure.Data;
using ProjectTalaria.Infrastructure.Storage;
using ProjectTalaria.DataPlane.Streamer.Endpoints;
using ProjectTalaria.DataPlane.Streamer.Middleware;
using ProjectTalaria.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var storageConfig = builder.Configuration["Storage:UseLocal"] ?? "true";
if (storageConfig.StartsWith("${"))
    storageConfig = Environment.GetEnvironmentVariable("USE_LOCAL_STORAGE") ?? "true";
var useLocalStorage = storageConfig.ToLower() == "true";

if (!useLocalStorage)
{
    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddAWSService<IAmazonKeyManagementService>();
}

var connectionString = builder.Configuration.GetConnectionString("TalariaDb")
    ?? throw new InvalidOperationException("Missing TalariaDb connection string");
builder.Services.AddDbContext<TalariaDbContext>(options =>
{
    if (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
});

if (useLocalStorage)
{
    builder.Services.AddScoped<IStorageProvider, LocalFileStorageProvider>();
}
else
{
    builder.Services.AddScoped<IStorageProvider, S3StorageProvider>();
    builder.Services.AddScoped<KmsDecryptionService>();
}


builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("DataPlane.Streamer is running"))
    .AddCheck<ProjectTalaria.DataPlane.Streamer.HealthChecks.StreamerDbHealthCheck>("database");

var app = builder.Build();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandlerFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception processing request {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "Internal Server Error",
            status = StatusCodes.Status500InternalServerError,
            detail = app.Environment.IsDevelopment()
                ? exception?.ToString()
                : "An error occurred processing your request.",
            instance = context.Request.Path
        });
    });
});

// Correlation ID middleware
app.UseMiddleware<CorrelationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health check endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name != "self"
});

app.MapStreamingEndpoints(app.Configuration);

// Keep the app running by ignoring SIGINT in background mode
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
};

app.Run();
