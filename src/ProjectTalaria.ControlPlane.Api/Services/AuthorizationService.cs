using Microsoft.EntityFrameworkCore;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Infrastructure.Data;

namespace ProjectTalaria.ControlPlane.Api.Services;

public interface IAuthorizationService
{
    Task<bool> HasPermissionAsync(string userId, string resource, string action);
    Task<IEnumerable<string>> GetUserRolesAsync(string userId);
}

public class AuthorizationService(TalariaDbContext dbContext) : IAuthorizationService
{
    private readonly TalariaDbContext _dbContext = dbContext;

    public async Task<bool> HasPermissionAsync(string userId, string resource, string action)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
            return false;

        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == parsedUserId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (!userRoles.Any())
            return false;

        var hasPermission = await _dbContext.RolePermissions
            .Where(rp => userRoles.Contains(rp.RoleId))
            .Join(_dbContext.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => p)
            .AnyAsync(p => p.Resource == resource && p.Action == action);

        return hasPermission;
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
            return Enumerable.Empty<string>();

        return await _dbContext.UserRoles
            .Where(ur => ur.UserId == parsedUserId)
            .Join(_dbContext.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => r.Name)
            .ToListAsync();
    }
}
