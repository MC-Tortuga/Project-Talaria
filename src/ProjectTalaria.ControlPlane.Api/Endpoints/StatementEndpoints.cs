using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProjectTalaria.ControlPlane.Api.Attributes;
using ProjectTalaria.ControlPlane.Api.Services;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Infrastructure.Data;

namespace ProjectTalaria.ControlPlane.Api.Endpoints;

public static class StatementEndpoints
{
    public static void MapStatementEndpoints(this IEndpointRouteBuilder app, IConfiguration config)
    {
        app.MapGet("/api/statements", async (
            TalariaDbContext dbContext,
            HttpContext context) =>
        {
            var userAccountId = context.User.FindFirst("account_id")?.Value;

            if (string.IsNullOrEmpty(userAccountId))
            {
                return Results.Unauthorized();
            }

            var statements = await dbContext.BankStatements.AsNoTracking()
                .Where(s => s.AccountNumber == userAccountId)
                .Select(s => new { s.Id, s.AccountNumber, s.StatementDate, s.S3Key })
                .ToListAsync();

            return Results.Ok(statements);
        }).RequireAuthorization()
        .RequireRateLimiting("Fixed")
          .WithMetadata(new RequirePermissionAttribute("statements", "read"));

        app.MapGet("/api/statements/{documentId}/download", async (
            string documentId,
            TalariaDbContext dbContext,
            TokenGenerator tokenGenerator,
            ILogger<Program> logger,
            HttpContext context) =>
        {
            if (!Guid.TryParse(documentId, out var docId))
                return Results.BadRequest("Invalid document ID");

            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("Missing user claim");

            var userAccountId = context.User.FindFirst("account_id")?.Value
                ?? throw new UnauthorizedAccessException("Missing account_id claim");

            var statement = await dbContext.BankStatements.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == docId);

            if (statement == null)
                return Results.NotFound("Statement not found");

            if (statement.AccountNumber != userAccountId)
                return Results.Forbid();

            var ipAddress = context.Connection.RemoteIpAddress?.ToString();

            // Log to database
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DocumentId = docId,
                Action = AuditAction.TokenGenerated,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow,
                Details = $"Download requested for statement {docId}"
            });
            await dbContext.SaveChangesAsync();

            logger.LogInformation(
                "Intent: UserID={UserId}, DocID={DocId}, IP={Ip}, Timestamp={Timestamp}",
                userId, docId, ipAddress, DateTime.UtcNow);

            var streamBaseUrl = config["Streamer:BaseUrl"] ?? "http://localhost:5001";
            var (plaintextToken, streamUrl) = await tokenGenerator.GenerateAccessTokenAsync(
                docId, userId, streamBaseUrl);

            return Results.Ok(new
            {
                token = plaintextToken,
                streamUrl,
                expiresInSeconds = 300
            });
        }).RequireAuthorization()
          .RequireRateLimiting("Fixed")
          .WithMetadata(new RequirePermissionAttribute("statements", "read"));
    }
}
