using System.Security.Claims;
using ProjectTalaria.ControlPlane.Api.Attributes;
using ProjectTalaria.ControlPlane.Api.Services;

namespace ProjectTalaria.ControlPlane.Api.Endpoints;

public record CreateApiKeyRequest(string Name, int? ExpiresInDays);

public static class ApiKeyEndpoints
{
    public static void MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/apikeys").RequireAuthorization();

        group.MapGet("/", async (IApiKeyService apiKeyService, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var keys = await apiKeyService.GetUserApiKeysAsync(userId);
            return Results.Ok(keys.Select(k => new
            {
                k.Id,
                k.Name,
                k.CreatedAt,
                k.ExpiresAt,
                k.RevokedAt,
                k.LastUsedAt
            }));
        }).WithMetadata(new RequirePermissionAttribute("apikeys", "read"));

        group.MapPost("/", async (CreateApiKeyRequest request, IApiKeyService apiKeyService, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var expiresAt = request.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
                : DateTime.UtcNow.AddYears(1);

            var (plainKey, storedKey) = await apiKeyService.CreateApiKeyAsync(userId, request.Name, expiresAt);

            return Results.Created($"/api/apikeys/{storedKey.Id}", new
            {
                id = storedKey.Id,
                key = plainKey,
                name = storedKey.Name,
                expiresAt = storedKey.ExpiresAt
            });
        }).WithMetadata(new RequirePermissionAttribute("apikeys", "write"));

        group.MapDelete("/{keyId}", async (string keyId, IApiKeyService apiKeyService, HttpContext context) =>
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            if (!Guid.TryParse(keyId, out var keyGuid))
                return Results.BadRequest("Invalid key ID");

            var revoked = await apiKeyService.RevokeApiKeyAsync(keyGuid, userId);
            if (!revoked)
                return Results.NotFound("API key not found or not owned by user");

            return Results.NoContent();
        }).WithMetadata(new RequirePermissionAttribute("apikeys", "write"));
    }
}