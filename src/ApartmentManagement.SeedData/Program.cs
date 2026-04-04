using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using ApartmentManagement.DataSeed;
using ApartmentManagement.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

DotEnvLoader.TryLoad();
DotEnvLoader.TryLoad(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args
});

var conn = builder.Configuration["ConnectionStrings:DefaultConnection"];
if (string.IsNullOrWhiteSpace(conn))
{
    Console.WriteLine("Missing ConnectionStrings:DefaultConnection. Copy .env from .env.example or set appsettings.");
    return;
}

builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
builder.Services.AddDbContext<ApartmentDbContext>(o => o.UseSqlServer(conn));
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApartmentDbContext>()
.AddDefaultTokenProviders();

var host = builder.Build();

while (true)
{
    Console.WriteLine();
    Console.WriteLine("Apartment Management — Seed");
    Console.WriteLine("  1 = Insert (~1000 rows)");
    Console.WriteLine("  2 = Delete seeded data");
    Console.WriteLine("  0 = Exit");
    Console.Write("Select: ");
    var line = Console.ReadLine()?.Trim();
    try
    {
        switch (line)
        {
            case "1":
                await SeedData.InsertAsync(host.Services);
                Console.WriteLine("Done.");
                break;
            case "2":
                await SeedData.DeleteAsync(host.Services);
                Console.WriteLine("Done.");
                break;
            case "0":
                return;
            default:
                Console.WriteLine("Invalid option.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedCli");
        log.LogError(ex, "Seed tool error");
    }
}
