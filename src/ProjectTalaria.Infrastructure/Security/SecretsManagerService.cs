using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ProjectTalaria.Infrastructure.CDN;

namespace ProjectTalaria.Infrastructure.Security;

public class SecretsManagerService
{
    private readonly IAmazonSecretsManager _secretsClient;
    private readonly string _secretName;
    private string? _cachedPrivateKey;
    private DateTime? _cacheExpiry;

    public SecretsManagerService(IAmazonSecretsManager secretsClient, IConfiguration config)
    {
        _secretsClient = secretsClient;
        var secretsConfig = config.GetSection("SecretsManager").Get<SecretsManagerConfig>() 
            ?? throw new InvalidOperationException("Missing SecretsManager configuration");
        _secretName = secretsConfig.SecretName;
    }

    public async Task<string> GetCloudFrontPrivateKeyAsync()
    {
        if (_cachedPrivateKey != null && _cacheExpiry > DateTime.UtcNow)
            return _cachedPrivateKey;

        var request = new GetSecretValueRequest
        {
            SecretId = _secretName
        };

        var response = await _secretsClient.GetSecretValueAsync(request);
        
        var secretJson = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString) 
            ?? throw new InvalidOperationException("Failed to parse secret JSON");

        if (!secretJson.TryGetValue("PrivateKey", out var privateKey))
            throw new InvalidOperationException("PrivateKey not found in secret");

        _cachedPrivateKey = privateKey;
        _cacheExpiry = DateTime.UtcNow.AddHours(1);

        return _cachedPrivateKey;
    }
}