using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using ITManagement.Application.Common.Interfaces;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Entities.Agents;
using ITManagement.Domain.Entities.Simulation;
using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Enums;
using ITManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ITManagement.IntegrationTests;

public class DashboardSqlServerTests
{
    [Fact]
    public async Task Dashboard_returns_live_counts_and_requires_admin()
    {
        // Database riêng cho test; không chạy migration/seed trên database của người dùng.
        var database = "itam_dashboard_test_" + Guid.NewGuid().ToString("N");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
                            Environment.GetEnvironmentVariable("TEST_SQLSERVER")
                            ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;Encrypt=False")
                        { InitialCatalog = database }.ConnectionString,
                    ["Database:InitializeOnStartup"] = "false",
                    ["Jwt:Secret"] = "dashboard-test-only-secret-at-least-32-bytes",
                    ["Jwt:Issuer"] = "ITManagement",
                    ["Jwt:Audience"] = "ITManagement"
                }));
            // Đồng bộ key kiểm thử với options đã được host đăng ký trước config override.
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("dashboard-test-only-secret-at-least-32-bytes"));
                    options.TokenValidationParameters.ValidIssuer = "ITManagement";
                    options.TokenValidationParameters.ValidAudience = "ITManagement";
                }));
        });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(database, db.Database.GetDbConnection().Database);
        try
        {
            await db.Database.EnsureCreatedAsync();
            using var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard")).StatusCode);

            AppUser User(string name, UserRole role, bool deleted = false) => new()
            {
                Username = name, Email = name + "@test.invalid", IdentityUserId = Guid.NewGuid().ToString(),
                Role = role, IsDeleted = deleted
            };
            var admin = User("admin", UserRole.Admin);
            var employee = User("employee", UserRole.Employee);
            var technician = User("technician", UserRole.Technician);
            var deletedUser = User("deleted", UserRole.Employee, true);
            db.AppUsers.AddRange(admin, employee, technician, deletedUser);
            var asset = new Asset { AssetCode = "active", Status = AssetStatus.Assigned };
            var deletedAsset = new Asset { AssetCode = "deleted", Status = AssetStatus.Assigned, IsDeleted = true };
            db.Assets.AddRange(asset, deletedAsset, new Asset { AssetCode = "available" });
            db.LocalProfiles.AddRange(
                new LocalProfile { UserId = employee.Id, AssetId = asset.Id },
                new LocalProfile { UserId = employee.Id, AssetId = deletedAsset.Id },
                new LocalProfile { UserId = deletedUser.Id, AssetId = asset.Id });
            db.Agents.AddRange(
                new RegisteredAgent { AssetId = asset.Id, InstanceId = Guid.NewGuid(), CredentialHash = "active", CreatedAt = DateTime.UtcNow },
                new RegisteredAgent { AssetId = asset.Id, InstanceId = Guid.NewGuid(), CredentialHash = "revoked", CreatedAt = DateTime.UtcNow, RevokedAt = DateTime.UtcNow },
                new RegisteredAgent { AssetId = deletedAsset.Id, InstanceId = Guid.NewGuid(), CredentialHash = "deleted", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
            foreach (var user in new[] { employee, technician, admin })
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                    jwt.GenerateAccessToken(user.Id, user.Username, user.Role.ToString(), user.Email, user.SecurityVersion));
                var response = await client.GetAsync("/api/dashboard");
                if (user.Role != UserRole.Admin)
                {
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                    continue;
                }
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.True(body.GetProperty("success").GetBoolean());
                var data = body.GetProperty("data");
                Assert.Equal(5, data.EnumerateObject().Count());
                Assert.Equal(3, data.GetProperty("users").GetInt32());
                Assert.Equal(2, data.GetProperty("assets").GetInt32());
                Assert.Equal(1, data.GetProperty("assignedAssets").GetInt32());
                Assert.Equal(1, data.GetProperty("agents").GetInt32());
                Assert.Equal(1, data.GetProperty("profiles").GetInt32());
            }
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
