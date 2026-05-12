using Microsoft.EntityFrameworkCore;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Domain.Interfaces;

namespace ProjectTalaria.Infrastructure.Data;

public class AccessTokenRepository(TalariaDbContext context) : IAccessTokenRepository
{
    public async Task<AccessToken?> GetByTokenValueAsync(string hashedToken, CancellationToken ct = default)
    {
        return await context.AccessTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenValue == hashedToken, ct);
    }

    public async Task<AccessToken> CreateAsync(AccessToken token, CancellationToken ct = default)
    {
        context.AccessTokens.Add(token);
        await context.SaveChangesAsync(ct);
        return token;
    }

    public async Task<bool> MarkAsUsedAsync(Guid tokenId, CancellationToken ct = default)
    {
        var token = await context.AccessTokens.FindAsync([tokenId], ct);
        if (token == null) return false;

        token.Status = TokenStatus.Used;
        token.UsedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync(ct);
        return true;
    }
}