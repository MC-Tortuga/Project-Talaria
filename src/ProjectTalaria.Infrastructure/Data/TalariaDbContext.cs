using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProjectTalaria.Domain.Entities;

namespace ProjectTalaria.Infrastructure.Data;

public class TalariaDbContext : DbContext
{
    private readonly string? _connectionString;

    public TalariaDbContext(DbContextOptions<TalariaDbContext> options) : base(options) => _connectionString = null;

    public TalariaDbContext(DbContextOptions<TalariaDbContext> options, IConfiguration config) : base(options) => _connectionString = config["ConnectionStrings:TalariaDb"];

    public TalariaDbContext(IConfiguration config) : this(CreateOptions(), config) { }

    private static DbContextOptions<TalariaDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<TalariaDbContext>().Options;
    }

    public DbSet<BankStatement> BankStatements => Set<BankStatement>();
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            if (_connectionString != null)
            {
                var isSqlite = _connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase)
                    || _connectionString.Contains("sqlite", StringComparison.OrdinalIgnoreCase)
                    || _connectionString.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

                if (isSqlite)
                    optionsBuilder.UseSqlite(_connectionString);
                else
                    optionsBuilder.UseSqlServer(_connectionString);
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BankStatement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.S3Key).IsRequired().HasMaxLength(500);
            entity.Property(e => e.EncryptedDataKey).IsRequired();
        });

        modelBuilder.Entity<AccessToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenValue).IsRequired().HasMaxLength(64);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.TokenValue);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Resource).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.Resource, e.Action }).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });
            entity.HasOne(e => e.User).WithMany(u => u.UserRoles).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role).WithMany(r => r.UserRoles).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.PermissionId });
            entity.HasOne(e => e.Role).WithMany(r => r.RolePermissions).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Permission).WithMany(p => p.RolePermissions).HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasOne(e => e.User).WithMany(u => u.ApiKeys).HasForeignKey(e => e.UserId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
