using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nagomi.Api.Infrastructure.Persistence;

public sealed class NagomiDbContextFactory : IDesignTimeDbContextFactory<NagomiDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=nagomi;Username=nagomi;Password=nagomi";

    public NagomiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Nagomi")
            ?? Environment.GetEnvironmentVariable("NAGOMI_DB_CONNECTION")
            ?? DefaultConnectionString;
        var options = new DbContextOptionsBuilder<NagomiDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(NagomiDbContext).Assembly.FullName))
            .UseOpenIddict()
            .Options;

        return new NagomiDbContext(options);
    }
}
