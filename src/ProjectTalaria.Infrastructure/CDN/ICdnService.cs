namespace ProjectTalaria.Infrastructure.CDN;

public interface ICdnService
{
    Task<string> GetSignedUrlAsync(string resourcePath, DateTime expiry);
    bool IsEnabled { get; }
}