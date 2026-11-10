using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Services;
using ITManagement.Domain.Interfaces.Repositories;
using ITManagement.Infrastructure.FileStorage;
using ITManagement.Infrastructure.Identity;
using ITManagement.Infrastructure.Persistence;
using ITManagement.Infrastructure.Persistence.Repositories;
using ITManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITManagement.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Đăng ký các dịch vụ hạ tầng, kho dữ liệu và lưu trữ.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("ITManagement.Infrastructure")));

        // ASP.NET Identity
        services.AddIdentity<IdentityUser, IdentityRole>(opts =>
        {
            opts.Password.RequireDigit = true;
            opts.Password.RequiredLength = 8;
            opts.Password.RequireUppercase = false;
            opts.Password.RequireNonAlphanumeric = false;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IStorageRepository, StorageRepository>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAgentOperationDispatcher, AgentOperationDispatcher>();
        services.AddScoped<IAgentService, AgentService>();

        // Services
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IDeviceSessionService, DeviceSessionService>();
        services.AddScoped<IProfileOperationService, ProfileOperationService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IStorageAdminService, StorageAdminService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
