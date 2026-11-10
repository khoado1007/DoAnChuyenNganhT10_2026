using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Infrastructure.Persistence;
using ITManagement.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ITManagement.IntegrationTests;

// Uses an isolated database; never connects to the application's development database.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "itam-storage-test-" + Guid.NewGuid().ToString("N"));
    private readonly string connection = new Npgsql.NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres")
    { Database = "itmanagement_test_" + Guid.NewGuid().ToString("N") }.ConnectionString;

    // Cấu hình máy chủ kiểm thử để dùng kết nối cơ sở dữ liệu được chỉ định.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Password"] = "Admin@12345"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITManagement.Application.Common.Interfaces.IFileStorageService>();
            services.AddSingleton<ITManagement.Application.Common.Interfaces.IFileStorageService>(new ITManagement.Infrastructure.FileStorage.LocalFileStorageService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:RootPath"] = StorageRoot }).Build()));
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
        });
    }

    /// <summary>Giải phóng tài nguyên máy chủ kiểm thử bất đồng bộ.</summary>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
            await db.Database.EnsureDeletedAsync();
        }
        finally { await base.DisposeAsync(); if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, true); }
    }
}

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;
    /// <summary>Khởi tạo bộ kiểm thử xác thực với máy chủ API dùng chung.</summary>
    public AuthTests(ApiFactory factory) => this.factory = factory;

    /// <summary>Kiểm thử quản trị viên đã seed có thể đăng nhập và khôi phục phiên.</summary>
    [Fact]
    public async Task Seeded_admin_can_login_and_restore_session()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "Admin@12345" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean());
        var data = json.GetProperty("data");
        Assert.Equal("Admin", data.GetProperty("user").GetProperty("role").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin", profile.GetProperty("data").GetProperty("username").GetString());
        Assert.False(profile.GetProperty("data").TryGetProperty("passwordHash", out _));
    }

    /// <summary>Kiểm thử thông tin đăng nhập sai trả về phản hồi 401 theo định dạng API.</summary>
    [Theory]
    [InlineData("admin", "incorrect-password")]
    [InlineData("missing-account", "Admin@12345")]
    public async Task Invalid_credentials_return_401_envelope(string username, string password)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
    }

    /// <summary>Kiểm thử yêu cầu đăng nhập rỗng trả về lỗi theo từng trường.</summary>
    [Fact]
    public async Task Empty_login_returns_field_errors()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Object, json.GetProperty("errors").ValueKind);
    }

    /// <summary>Kiểm thử truy vấn phiên ẩn danh trả về phản hồi 401.</summary>
    [Fact]
    public async Task Anonymous_session_returns_401_envelope()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
    }

    /// <summary>Kiểm thử seed nhiều lần vẫn chỉ giữ một quản trị viên cùng hạn mức.</summary>
    [Fact]
    public async Task Seeding_twice_preserves_single_admin_with_quota()
    {
        using var scope = factory.Services.CreateScope();
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
        await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await db.AppUsers.Include(u => u.StorageQuota).SingleAsync(u => u.Username == "admin");
        Assert.NotNull(admin.StorageQuota);
        Assert.Equal(10L * 1024 * 1024 * 1024, admin.StorageQuota.QuotaBytes);
    }
}
