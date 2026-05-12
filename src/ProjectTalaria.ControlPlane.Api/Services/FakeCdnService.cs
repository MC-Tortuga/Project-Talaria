namespace ProjectTalaria.ControlPlane.Api.Services;

public class FakeCdnService : ProjectTalaria.Infrastructure.CDN.ICdnService
{
    public bool IsEnabled => false;

    public Task<string> GetSignedUrlAsync(string resourcePath, DateTime expiry)
    {
        throw new NotSupportedException("CDN signing not available in local test mode");
    }
}