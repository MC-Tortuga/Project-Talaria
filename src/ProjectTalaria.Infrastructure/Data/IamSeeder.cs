using Microsoft.EntityFrameworkCore;
using ProjectTalaria.Domain.Entities;

namespace ProjectTalaria.Infrastructure.Data;

public static class IamSeeder
{
    public static async Task SeedAsync(TalariaDbContext db)
    {
        if (await db.Roles.AnyAsync())
            return;

        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Description = "Full system access" };
        var userRole = new Role { Id = Guid.NewGuid(), Name = "User", Description = "Standard user access" };
        var auditorRole = new Role { Id = Guid.NewGuid(), Name = "Auditor", Description = "Read-only access for auditing" };

        db.Roles.AddRange(adminRole, userRole, auditorRole);
        await db.SaveChangesAsync();

        var permissions = new List<Permission>
        {
            new() { Id = Guid.NewGuid(), Resource = "statements", Action = "read", Description = "View statements" },
            new() { Id = Guid.NewGuid(), Resource = "statements", Action = "write", Description = "Create/modify statements" },
            new() { Id = Guid.NewGuid(), Resource = "stream", Action = "read", Description = "Download documents" },
            new() { Id = Guid.NewGuid(), Resource = "admin", Action = "read", Description = "View admin panel" },
            new() { Id = Guid.NewGuid(), Resource = "admin", Action = "write", Description = "Admin operations" },
            new() { Id = Guid.NewGuid(), Resource = "users", Action = "read", Description = "View users" },
            new() { Id = Guid.NewGuid(), Resource = "users", Action = "write", Description = "Manage users" },
            new() { Id = Guid.NewGuid(), Resource = "apikeys", Action = "read", Description = "View API keys" },
            new() { Id = Guid.NewGuid(), Resource = "apikeys", Action = "write", Description = "Manage API keys" },
        };

        db.Permissions.AddRange(permissions);
        await db.SaveChangesAsync();

        var rolePermissions = new List<RolePermission>
        {
            // Admin - all permissions
            new() { RoleId = adminRole.Id, PermissionId = permissions[0].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[1].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[2].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[3].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[4].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[5].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[6].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[7].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = adminRole.Id, PermissionId = permissions[8].Id, AssignedAt = DateTime.UtcNow },

            // User - statements and stream read
            new() { RoleId = userRole.Id, PermissionId = permissions[0].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = userRole.Id, PermissionId = permissions[2].Id, AssignedAt = DateTime.UtcNow },

            // Auditor - read access to everything
            new() { RoleId = auditorRole.Id, PermissionId = permissions[0].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = auditorRole.Id, PermissionId = permissions[2].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = auditorRole.Id, PermissionId = permissions[3].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = auditorRole.Id, PermissionId = permissions[5].Id, AssignedAt = DateTime.UtcNow },
            new() { RoleId = auditorRole.Id, PermissionId = permissions[7].Id, AssignedAt = DateTime.UtcNow },
        };

        db.RolePermissions.AddRange(rolePermissions);
        await db.SaveChangesAsync();

        var adminUser = new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "admin@talaria.local",
            Name = "System Admin",
            PasswordHash = null,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole
        {
            UserId = adminUser.Id,
            RoleId = adminRole.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = "system"
        });
        await db.SaveChangesAsync();

        Console.WriteLine($"Seeded roles, permissions, and admin user ({adminUser.Email})");
    }
}