using Hospitality.Api.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hospitality.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        EnvFile.LoadIfPresent();
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var connectionString = string.IsNullOrWhiteSpace(databaseUrl)
            ? "Host=localhost;Database=hospitality;Username=postgres;Password=postgres"
            : PostgresUrl.ToConnectionString(databaseUrl);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
