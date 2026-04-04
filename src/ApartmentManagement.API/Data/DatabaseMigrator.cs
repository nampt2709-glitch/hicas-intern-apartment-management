using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApartmentManagement.Data;

public static class DatabaseMigrator
{
    private const int MaxMigrateAttempts = 20;

    /// <summary>
    /// Applies pending EF Core migrations with retries (useful when SQL Server is still starting in Docker).
    /// Does not seed data.
    /// </summary>
    public static async Task ApplyMigrationsWithRetryAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApartmentDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator");

        for (var attempt = 1; attempt <= MaxMigrateAttempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxMigrateAttempts)
            {
                logger.LogWarning(ex, "Migrate attempt {Attempt}/{Max} failed; SQL Server may still be starting. Retrying...",
                    attempt, MaxMigrateAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }
}
