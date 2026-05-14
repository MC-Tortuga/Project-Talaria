using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Amazon.SecretsManager;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MySql.EntityFrameworkCore;
using ProjectTalaria.ControlPlane.Api.Endpoints;
using ProjectTalaria.ControlPlane.Api.HealthChecks;
using ProjectTalaria.ControlPlane.Api.Middleware;
using ProjectTalaria.ControlPlane.Api.Services;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Infrastructure.CDN;
using ProjectTalaria.Infrastructure.Data;
using ProjectTalaria.Infrastructure.Security;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddAuthorization(options => options.FallbackPolicy = null);

var jwtSecret = builder.Configuration["Jwt:DevSecret"] ?? Environment.GetEnvironmentVariable("JWT_DEV_SECRET");
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.StartsWith("${"))
{
    throw new InvalidOperationException("Jwt:DevSecret is not configured. Set Jwt__DevSecret or JWT_DEV_SECRET.");
}

var authBuilder = builder.Services.AddAuthentication("dev");

authBuilder.AddJwtBearer("dev", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret))
    };
});

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var oauthAuthority = builder.Configuration["Auth:OAuth:Authority"];
var oauthClientId = builder.Configuration["Auth:OAuth:ClientId"];
var oauthClientSecret = builder.Configuration["Auth:OAuth:ClientSecret"];

if (!string.IsNullOrEmpty(oauthAuthority) && !oauthAuthority.StartsWith("${") &&
    !string.IsNullOrEmpty(oauthClientId) && !oauthClientId.StartsWith("${"))
{
    authBuilder.AddOpenIdConnect("oauth", options =>
    {
        options.Authority = oauthAuthority;
        options.ClientId = oauthClientId;
        options.ClientSecret = oauthClientSecret ?? "";
        options.ResponseType = "code";
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath = "/signin-oidc";
        options.SignInScheme = "dev";

        var audience = builder.Configuration["Auth:OAuth:Audience"];
        if (!string.IsNullOrEmpty(audience) && !audience.StartsWith("${"))
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidAudience = audience
            };
        }

        options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
        {
            OnTokenValidated = context =>
            {
                var roleClaims = context.Principal?.FindAll("roles") ?? Enumerable.Empty<Claim>();
                if (roleClaims.Any())
                {
                    var identity = (ClaimsIdentity)context.Principal!.Identity!;
                    foreach (var role in roleClaims)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role.Value));
                    }
                }
                return Task.CompletedTask;
            }
        };
    });
}

var connectionString = builder.Configuration.GetConnectionString("TalariaDb")
    ?? throw new InvalidOperationException("Missing TalariaDb connection string");
builder.Services.AddDbContext<TalariaDbContext>(options =>
    options.UseMySQL(connectionString));

var useLocalMode = builder.Configuration["Storage:UseLocal"]?.ToLower() == "true" ||
                   builder.Configuration["Dev:EnableTestTokenEndpoint"]?.ToLower() == "true";

if (!useLocalMode)
{
    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<IAmazonSecretsManager>();
    builder.Services.AddScoped<SecretsManagerService>();
    builder.Services.AddScoped<ICdnService, CloudFrontService>();
}
else
{
    builder.Services.AddScoped<ICdnService>(sp => new FakeCdnService());
}

builder.Services.AddScoped<ProjectTalaria.Domain.Interfaces.IAccessTokenRepository, AccessTokenRepository>();
builder.Services.AddScoped<TokenGenerator>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("ControlPlane.Api is running"))
    .AddCheck<TalariaDbHealthCheck>("database")
    .AddCheck<StorageHealthCheck>("storage");

var rateLimitSection = builder.Configuration.GetSection("RateLimiting:Fixed");
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Fixed", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(rateLimitSection.GetValue<int>("WindowMinutes", 1));
        opt.PermitLimit = rateLimitSection.GetValue<int>("PermitLimit", 100);
        opt.QueueLimit = rateLimitSection.GetValue<int>("QueueLimit", 2);
    });
});
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

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
        context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
});

// Correlation ID middleware for request tracing
app.UseMiddleware<CorrelationMiddleware>();

// Health check endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name != "self"
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TalariaDbContext>();
    db.Database.Migrate();

    if (!db.BankStatements.Any())
    {
        var statements = Enumerable.Range(1, 10).Select(i => new BankStatement
        {
            Id = Guid.NewGuid(),
            AccountNumber = $"ACC{i:D3}",
            StatementDate = DateTime.Now.AddDays(-i),
            S3Key = $"statements/statement-{i}.pdf",
            EncryptedDataKey = new byte[8]
        }).ToList();
        db.BankStatements.AddRange(statements);
        db.SaveChanges();
        Console.WriteLine("Seeded 10 test statements");
    }

    var localPath = builder.Configuration["Storage:LocalPath"] ?? Path.Combine(AppContext.BaseDirectory, "statements");
    var s3Key = db.BankStatements.FirstOrDefault()?.S3Key;
    if (!string.IsNullOrEmpty(s3Key))
    {
        var statementsDir = Path.Combine(localPath, "statements");
        if (!Directory.Exists(statementsDir))
        {
            Directory.CreateDirectory(statementsDir);
            for (int i = 1; i <= 10; i++)
            {
                var pdfPath = Path.Combine(statementsDir, $"statement-{i}.pdf");
                var pdfContent = GenerateSamplePdf($"Sample Bank Statement {i}", $"Account ACC{i:D3}", DateTime.Now.AddDays(-i));
                File.WriteAllBytes(pdfPath, pdfContent);
                Console.WriteLine($"Created: {pdfPath}");
            }
        }
    }

    await IamSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else if (builder.Configuration["Dev:EnableTestTokenEndpoint"]?.ToLower() == "true")
{
    var securityLogger = app.Services.GetRequiredService<ILogger<Program>>();
    securityLogger.LogCritical("Dev:EnableTestTokenEndpoint is enabled outside Development environment! Disable immediately.");
}

// app.UseHttpsRedirection(); // Disabled for local testing
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStatementEndpoints(app.Configuration);
app.MapApiKeyEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithName("HealthCheck");

if (app.Environment.IsDevelopment())
{
    // Dev test token endpoint
    var devSecret = builder.Configuration["Jwt:DevSecret"];
    if (builder.Configuration["Dev:EnableTestTokenEndpoint"]?.ToLower() == "true" && !string.IsNullOrEmpty(devSecret))
    {
        app.MapGet("/dev/token", (string account_id, string? roles) =>
        {
            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(devSecret);

            var claims = new List<Claim>
            {
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
            new Claim("account_id", account_id ?? "demo-account")
            };

            var roleList = string.IsNullOrEmpty(roles)
                ? new[] { "Admin", "User" }
                : roles.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var role in roleList)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = handler.CreateToken(descriptor);
            return Results.Ok(new
            {
                token = handler.WriteToken(token),
                account_id = account_id ?? "demo-account",
                roles = roleList,
                expires_in = 86400
            });
        });
    }
}

static byte[] GenerateSamplePdf(string title, string account, DateTime date)
{
    using var ms = new MemoryStream();
    var writer = new StreamWriter(ms);
    writer.WriteLine("%PDF-1.4");
    writer.WriteLine("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj");
    writer.WriteLine("2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj");
    writer.WriteLine("3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R/Resources<</Font<</F1 4 0 R>>>>/Contents 5 0 R>>endobj");
    writer.WriteLine("4 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj");
    var content = $"BT /F1 24 Tf 100 700 Td ({title}) Tj ET BT /F1 14 Tf 100 660 Td (Account: {account}) Tj ET BT /F1 12 Tf 100 630 Td (Date: {date:yyyy-MM-dd}) Tj ET BT /F1 12 Tf 100 600 Td (Sample bank statement content for testing.) Tj ET";
    var bytes = System.Text.Encoding.ASCII.GetBytes(content);
    writer.WriteLine($"5 0 obj<</Length {bytes.Length}>>");
    writer.WriteLine("stream");
    writer.Write(content);
    writer.WriteLine("endstream endobj");
    writer.WriteLine("xref 0 6");
    writer.WriteLine("0 1");
    writer.WriteLine("0000000000 65535 f ");
    writer.WriteLine("1 1");
    writer.WriteLine("0000000009 00000 n ");
    writer.WriteLine("2 1");
    writer.WriteLine("0000000058 00000 n ");
    writer.WriteLine("3 1");
    writer.WriteLine("0000000115 00000 n ");
    writer.WriteLine("4 1");
    writer.WriteLine("0000000206 00000 n ");
    writer.WriteLine("5 1");
    writer.WriteLine("0000000267 00000 n ");
    writer.WriteLine("trailer<</Size 6/Root 1 0 R>>");
    writer.WriteLine("startxref");
    writer.WriteLine("0");
    writer.WriteLine("%%EOF");
    writer.Flush();
    return ms.ToArray();
}

app.Run();
