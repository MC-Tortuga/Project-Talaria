using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Domain.Interfaces;
using ProjectTalaria.Infrastructure.Data;
using ProjectTalaria.Infrastructure.Security;

namespace ProjectTalaria.DataPlane.Streamer.Endpoints;

public static class StreamEndpoints
{
    public static void MapStreamingEndpoints(this IEndpointRouteBuilder app, IConfiguration config)
    {
        var useLocalStorage = config["Storage:UseLocal"] == "true";

        app.MapGet("/stream/{fileId}", async (
     [FromRoute] string fileId,
     [FromServices] IStorageProvider storage,
     [FromServices] KmsDecryptionService? kms,
     [FromServices] TalariaDbContext dbContext,
     [FromServices] ILogger<Program> logger,
     HttpContext context) =>
 {
     if (!Guid.TryParse(fileId, out var statementId))
         return Results.BadRequest("Invalid file ID");

     var token = ExtractTokenFromHeader(context);
     if (string.IsNullOrEmpty(token))
         return Results.Unauthorized();

     var hashedToken = HashToken(token);

     logger.LogInformation("Token hash: {Hash}, looking for in DB", hashedToken);

     var accessToken = await dbContext.AccessTokens.AsNoTracking()
         .FirstOrDefaultAsync(t => t.TokenValue == hashedToken);

     logger.LogInformation("AccessToken found: {Found}, Status: {Status}", accessToken != null, accessToken?.Status);

     if (accessToken == null)
         return Results.Unauthorized();

      if (accessToken.Status == TokenStatus.Used)
          return Results.Unauthorized();

      if (accessToken.Status == TokenStatus.Expired || accessToken.ExpiresAt < DateTime.UtcNow)
          return Results.Unauthorized();

      if (accessToken.DocumentId != statementId)
          return Results.Forbid();

     var statement = await dbContext.BankStatements.AsNoTracking()
         .FirstOrDefaultAsync(s => s.Id == statementId);

     if (statement == null)
         return Results.NotFound("Statement not found");

     var ipAddress = context.Connection.RemoteIpAddress?.ToString();

     // Log to database
     dbContext.AuditLogs.Add(new AuditLog
     {
         Id = Guid.NewGuid(),
         UserId = accessToken.UserId,
         DocumentId = statementId,
         Action = AuditAction.TokenBurned,
         IpAddress = ipAddress,
         Timestamp = DateTime.UtcNow,
         Details = $"Token burned for document {statementId}"
     });
     await dbContext.SaveChangesAsync();

     logger.LogInformation(
                    "Intent: UserID={UserId}, DocID={DocId}, IP={Ip}, Timestamp={Timestamp}",
                    accessToken.UserId, statementId, ipAddress, DateTime.UtcNow);

     var burned = await dbContext.AccessTokens
         .Where(t => t.Id == accessToken.Id
                  && t.Status == TokenStatus.Active
                  && t.ExpiresAt > DateTime.UtcNow)
         .ExecuteUpdateAsync(setters => setters
             .SetProperty(t => t.Status, TokenStatus.Used)
             .SetProperty(t => t.UsedAt, DateTime.UtcNow));

      if (burned == 0)
          return Results.Unauthorized();

     try
     {
         var fileStream = useLocalStorage
             ? await storage.GetFileStreamAsync(statement.S3Key)
             : await GetDecryptedStreamAsync(storage, kms!, statement.S3Key, statement.EncryptedDataKey);

         fileStream.Position = 0;
         return Results.Stream(fileStream, "application/pdf", $"statement-{statementId}.pdf", enableRangeProcessing: true);
     }
     catch (Exception ex)
     {
         logger.LogError(ex, "Error processing stream for document {DocumentId}", statementId);
         return Results.StatusCode(500);
     }
 });
    }

    private static string HashToken(string plaintextToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintextToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<Stream> GetDecryptedStreamAsync(IStorageProvider storage, KmsDecryptionService kms, string s3Key, byte[] encryptedKey)
    {
        await using var encryptedStream = await storage.GetFileStreamAsync(s3Key);
        return await kms.DecryptStreamAsync(encryptedStream, encryptedKey);
    }

    private static string? ExtractTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
            return null;

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..];

        return authHeader;
    }
}
