using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApartmentManagement.Data;

// Tiện ích tĩnh: áp migration EF Core khi khởi động ứng dụng, có retry khi DB chưa sẵn sàng (Docker).
public static class DatabaseMigrator
{
    private const int MaxMigrateAttempts = 20;

    // Áp các migration EF Core đang chờ, có thử lại (hữu ích khi SQL Server trong Docker vừa khởi động). Không seed dữ liệu mẫu.
    public static async Task ApplyMigrationsWithRetryAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        // Tạo scope ngắn để lấy DbContext và logger (tránh giữ DbContext singleton).
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApartmentDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator");

        // Vòng lặp thử tối đa MaxMigrateAttempts; thành công thì thoát, lỗi thì chờ 3s rồi thử lại.
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
