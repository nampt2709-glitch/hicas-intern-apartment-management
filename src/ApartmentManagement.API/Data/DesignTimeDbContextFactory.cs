using ApartmentManagement.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApartmentManagement.Data;

// Factory thiết kế thời gian: dùng bởi CLI `dotnet ef` (migration/add) khi không có host ASP.NET chạy.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApartmentDbContext>
{
    public ApartmentDbContext CreateDbContext(string[] args)
    {
        // nạp .env giống runtime để biến ConnectionStrings__DefaultConnection có sẵn.
        DotEnvLoader.TryLoad();
        DotEnvLoader.TryLoad(AppContext.BaseDirectory);

        // đọc chuỗi kết nối từ biến môi trường (chuẩn .NET với __ thay cho :).
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection is not set. Copy .env.example to .env at the repository root " +
                "(see README), or set the environment variable before running EF Core tools.");
        }

        // build options SQL Server và trả về DbContext cho công cụ EF.
        var optionsBuilder = new DbContextOptionsBuilder<ApartmentDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new ApartmentDbContext(optionsBuilder.Options);
    }
}
