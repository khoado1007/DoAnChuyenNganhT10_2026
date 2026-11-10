using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Entities.Storage;
using ITManagement.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITManagement.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed dữ liệu ban đầu: tạo tài khoản Admin gốc.
/// Chỉ chạy nếu chưa có Admin nào trong hệ thống.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>Tạo dữ liệu khởi tạo nếu chưa có trong cơ sở dữ liệu.</summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<AppDbContext>();
        var adminPassword = services.GetRequiredService<IConfiguration>()["SeedAdmin:Password"];
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Tạo roles
        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (!await roleManager.RoleExistsAsync(role))
                EnsureSuccess(await roleManager.CreateAsync(new IdentityRole(role)));
        }

        // Tạo Admin gốc nếu chưa có
        const string adminEmail = "admin@itmanagement.local";
        var identityAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (identityAdmin is null)
        {
            if (string.IsNullOrWhiteSpace(adminPassword))
                throw new InvalidOperationException("SeedAdmin:Password must be set before initializing a database.");
            identityAdmin = new IdentityUser { UserName = "admin", Email = adminEmail, EmailConfirmed = true };
            EnsureSuccess(await userManager.CreateAsync(identityAdmin, adminPassword));
        }
        if (!await userManager.IsInRoleAsync(identityAdmin, nameof(UserRole.Admin)))
            EnsureSuccess(await userManager.AddToRoleAsync(identityAdmin, nameof(UserRole.Admin)));

        // Repair an interrupted old seed without resetting credentials or creating duplicates.
        var appAdmin = await db.AppUsers.IgnoreQueryFilters().Include(u => u.StorageQuota)
            .SingleOrDefaultAsync(u => u.IdentityUserId == identityAdmin.Id);
        if (appAdmin is null)
        {
            appAdmin = new AppUser
            {
                IdentityUserId = identityAdmin.Id, Username = identityAdmin.UserName!, Email = adminEmail,
                FullName = "System Administrator", Department = "IT", Role = UserRole.Admin,
                Status = UserStatus.Active
            };
            db.AppUsers.Add(appAdmin);
        }
        if (appAdmin.StorageQuota is null)
            appAdmin.StorageQuota = new StorageQuota { UserId = appAdmin.Id };
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // Phát sinh lỗi khi thao tác Identity khởi tạo thất bại.
    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Không thể seed tài khoản: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}
