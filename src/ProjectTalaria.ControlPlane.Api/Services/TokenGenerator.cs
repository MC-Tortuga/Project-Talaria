using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Domain.Interfaces;
using ProjectTalaria.Infrastructure.CDN;

namespace ProjectTalaria.ControlPlane.Api.Services;

public class TokenGenerator(
    IAccessTokenRepository tokenRepository, 
    ICdnService? cdnService,
    IConfiguration configuration)
{
    private readonly int _tokenByteSize = int.TryParse(configuration["TokenSettings:ByteSize"], out var size) ? size : 32;
    private readonly int _tokenExpiryMinutes = int.TryParse(configuration["TokenSettings:ExpiryMinutes"], out var expiry) ? expiry : 5;

    public async Task<(string PlaintextToken, string StreamUrl)> GenerateAccessTokenAsync(
        Guid documentId, 
        string userId,
        string streamBaseUrl)
    {
        var plaintextToken = GenerateRandomToken();
        var hashedToken = HashToken(plaintextToken);

        var accessToken = new AccessToken
        {
            Id = Guid.NewGuid(),
            TokenValue = hashedToken,
            UserId = userId,
            DocumentId = documentId,
            Status = TokenStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_tokenExpiryMinutes)
        };

        await tokenRepository.CreateAsync(accessToken);

        string streamUrl;
        if (cdnService?.IsEnabled == true)
        {
            var expiry = DateTime.UtcNow.AddMinutes(_tokenExpiryMinutes);
            streamUrl = await cdnService.GetSignedUrlAsync($"/stream/{documentId}", expiry);
        }
        else
        {
            streamUrl = $"{streamBaseUrl}/stream/{documentId}";
        }

        return (plaintextToken, streamUrl);
    }

    private string GenerateRandomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(_tokenByteSize);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string plaintextToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintextToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}