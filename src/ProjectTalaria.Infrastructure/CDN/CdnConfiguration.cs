namespace ProjectTalaria.Infrastructure.CDN;

public class CdnConfiguration
{
    public bool Enabled { get; set; }
    public string DistributionDomain { get; set; } = string.Empty;
    public string KeyPairId { get; set; } = string.Empty;
    public int DefaultTtlSeconds { get; set; } = 300;
}

public class SecretsManagerConfig
{
    public string Region { get; set; } = "us-east-1";
    public string SecretName { get; set; } = string.Empty;
}