using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.EntityFrameworkCore;

namespace ProjectTalaria.Infrastructure.Data;

public class TalariaDbContextFactory : IDesignTimeDbContextFactory<TalariaDbContext>
{
    public TalariaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TalariaDbContext>();
        var connectionString = args.Length > 0
            ? args[0]
            : throw new InvalidOperationException("Connection string is required for design-timeDbContext creation. Pass it as a command-line argument.");

        optionsBuilder.UseMySQL(connectionString);

        return new TalariaDbContext(optionsBuilder.Options);
    }
}
