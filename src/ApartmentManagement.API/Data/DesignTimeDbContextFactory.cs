using ApartmentManagement.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApartmentManagement.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApartmentDbContext>
{
    public ApartmentDbContext CreateDbContext(string[] args)
    {
        DotEnvLoader.TryLoad();
        DotEnvLoader.TryLoad(AppContext.BaseDirectory);

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection is not set. Copy .env.example to .env at the repository root " +
                "(see README), or set the environment variable before running EF Core tools.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApartmentDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new ApartmentDbContext(optionsBuilder.Options);
    }
}
