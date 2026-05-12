using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectTalaria.Infrastructure.Data;

public class TalariaDbContextFactory : IDesignTimeDbContextFactory<TalariaDbContext>
{
    public TalariaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TalariaDbContext>();
        var connectionString = args.Length > 0
            ? args[0]
            : "Data Source=design-time-talaria.db";

        var isSqlite = connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);
        if (isSqlite)
            optionsBuilder.UseSqlite(connectionString);
        else
            optionsBuilder.UseSqlServer(connectionString);

        return new TalariaDbContext(optionsBuilder.Options);
    }
}
