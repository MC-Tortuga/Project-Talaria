using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Infrastructure.Data;

namespace ProjectTalaria.ControlPlane.Api.Services;

public interface IApiKeyService
{
    Task<(string PlainKey, ApiKey StoredKey)> CreateApiKeyAsync(Guid userId, string name, DateTime? expiresAt = null);
    Task<bool> ValidateApiKeyAsync(string plainKey);
    Task<Guid?> GetUserIdFromKeyAsync(string plainKey);
    Task<bool> RevokeApiKeyAsync(Guid keyId, Guid userId);
    Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(Guid userId);
}

public class ApiKeyService(TalariaDbContext dbContext) : IApiKeyService
{
    private readonly TalariaDbContext _dbContext = dbContext;

    public async Task<(string PlainKey, ApiKey StoredKey)> CreateApiKeyAsync(Guid userId, string name, DateTime? expiresAt = null)
    {
        var plainKey = GenerateApiKey();
        var keyHash = HashKey(plainKey);

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            KeyHash = keyHash,
            UserId = userId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync();

        return (plainKey, apiKey);
    }

    public async Task<bool> ValidateApiKeyAsync(string plainKey)
    {
        if (string.IsNullOrEmpty(plainKey))
            return false;

        var keyHash = HashKey(plainKey);

        var apiKey = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

        if (apiKey == null)
            return false;

        if (apiKey.RevokedAt != null)
            return false;

        if (apiKey.ExpiresAt != null && apiKey.ExpiresAt < DateTime.UtcNow)
            return false;

        apiKey.LastUsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<Guid?> GetUserIdFromKeyAsync(string plainKey)
    {
        if (string.IsNullOrEmpty(plainKey))
            return null;

        var keyHash = HashKey(plainKey);

        var apiKey = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

        if (apiKey == null || apiKey.RevokedAt != null)
            return null;

        if (apiKey.ExpiresAt != null && apiKey.ExpiresAt < DateTime.UtcNow)
            return null;

        return apiKey.UserId;
    }

    public async Task<bool> RevokeApiKeyAsync(Guid keyId, Guid userId)
    {
        var apiKey = await _dbContext.ApiKeys.FindAsync(keyId);
        if (apiKey == null || apiKey.UserId != userId)
            return false;

        apiKey.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(Guid userId)
    {
        return await _dbContext.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string HashKey(string plainKey)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
