using ITManagement.Infrastructure.Persistence;
using ITManagement.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.API.Extensions;

public static class ApplicationExtensions
{
    /// <summary>
    /// Tự động migrate DB và seed dữ liệu khởi tạo khi ứng dụng khởi động.
    /// </summary>
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Đang migrate database...");
            await db.Database.MigrateAsync();

            logger.LogInformation("Đang seed dữ liệu khởi tạo...");
            await DatabaseSeeder.SeedAsync(scope.ServiceProvider);

            logger.LogInformation("Database sẵn sàng.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi khởi tạo database.");
            throw;
        }
    }
}
