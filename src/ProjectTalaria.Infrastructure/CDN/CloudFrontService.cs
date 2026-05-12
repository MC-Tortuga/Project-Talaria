using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using ProjectTalaria.Infrastructure.Security;

namespace ProjectTalaria.Infrastructure.CDN;

public class CloudFrontService(IConfiguration config, SecretsManagerService secretsManager) : ICdnService
{
    private readonly CdnConfiguration _config = config.GetSection("CDN").Get<CdnConfiguration>()
            ?? throw new InvalidOperationException("Missing CDN configuration");
    private readonly SecretsManagerService _secretsManager = secretsManager;

    public bool IsEnabled => _config.Enabled;

    public async Task<string> GetSignedUrlAsync(string resourcePath, DateTime expiry)
    {
        if (!_config.Enabled)
            throw new InvalidOperationException("CDN is not enabled");

        var privateKey = await _secretsManager.GetCloudFrontPrivateKeyAsync();

        var url = $"https://{_config.DistributionDomain}{resourcePath}";

        return GenerateSignedUrl(url, privateKey, _config.KeyPairId, expiry);
    }

    private string GenerateSignedUrl(string url, string privateKeyPem, string keyPairId, DateTime expiry)
    {
        var policy = GeneratePolicy(url, expiry);
        var policyBytes = System.Text.Encoding.UTF8.GetBytes(policy);
        var signature = SignWithRsa(privateKeyPem, policyBytes);

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}Policy={Uri.EscapeDataString(policy)}&Signature={Uri.EscapeDataString(signature)}&Key-Pair-Id={Uri.EscapeDataString(keyPairId)}";
    }

    private string GeneratePolicy(string resourceUrl, DateTime expiry)
    {
        var nowEpoch = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        var expiryEpoch = new DateTimeOffset(expiry).ToUnixTimeSeconds();

        var policyJson = $@"{{""Statement"":[{{""Resource"":""{resourceUrl}"",""Condition"":{{""DateGreaterThan"":{{""aws:EpochTime"":{nowEpoch}}},""DateLessThan"":{{""aws:EpochTime"":{expiryEpoch}}}}}}}]}}";

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(policyJson));
    }

    private string SignWithRsa(string privateKeyPem, byte[] data)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }
}
