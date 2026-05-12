using ProjectTalaria.Domain.Entities;

namespace ProjectTalaria.Domain.Interfaces;

public interface IAccessTokenRepository
{
    Task<AccessToken?> GetByTokenValueAsync(string hashedToken, CancellationToken ct = default);
    Task<AccessToken> CreateAsync(AccessToken token, CancellationToken ct = default);
    Task<bool> MarkAsUsedAsync(Guid tokenId, CancellationToken ct = default);
}