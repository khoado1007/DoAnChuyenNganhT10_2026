using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

/// <summary>Khởi tạo bộ kiểm thử quản lý người dùng với máy chủ API dùng chung.</summary>
public class UserTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Đăng nhập người dùng kiểm thử và trả về client đã xác thực.
    private async Task<HttpClient> Login(string username = "admin", string password = "Admin@12345")
    {
        var client = factory.CreateClient();
        var login = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        return client;
    }
    // Đọc dữ liệu phản hồi API sau khi kiểm tra trạng thái thành công.
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    // Tạo payload người dùng mới cho các yêu cầu kiểm thử.
    private static object NewUser(string name, string role = "Employee") => new
    { username = name, email = name + "@example.test", fullName = "Test Employee", department = "IT", role, password = "Test@12345" };
    // Tạo payload cập nhật thông tin người dùng cho các yêu cầu kiểm thử.
    private static object Edit(string name, string role = "Employee", string status = "Active") => new
    { email = name + "@example.test", fullName = "Updated Employee", department = "Operations", role, status };

    /// <summary>Kiểm thử quản trị viên tạo, liệt kê và cập nhật nhân viên cùng thông tin riêng tư và hạn mức.</summary>
    [Fact]
    public async Task Admin_creates_lists_and_updates_employee_with_private_identity_and_quota()
    {
        using var admin = await Login();
        var name = "u" + Guid.NewGuid().ToString("N");
        var created = await Data(await admin.PostAsJsonAsync("/api/users", NewUser(name)));
        var id = created.GetProperty("id").GetGuid();
        Assert.False(created.TryGetProperty("password", out _));
        Assert.False(created.TryGetProperty("passwordHash", out _));
        Assert.False(created.TryGetProperty("identityUserId", out _));
        var updated = await Data(await admin.PutAsJsonAsync($"/api/users/{id}", Edit(name)));
        Assert.Equal("Operations", updated.GetProperty("department").GetString());
        var page = await Data(await admin.GetAsync($"/api/users?search={name}&page=1&pageSize=10"));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        using var employee = await Login(name, "Test@12345");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/users", NewUser("forbidden"))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppUsers.Include(u => u.StorageQuota).SingleAsync(u => u.Id == id);
        Assert.NotNull(user.StorageQuota);
    }

    /// <summary>Kiểm thử mở khóa hoặc đổi vai trò không làm token cũ có hiệu lực trở lại.</summary>
    [Fact]
    public async Task Lock_unlock_and_role_change_do_not_restore_old_tokens()
    {
        using var admin = await Login();
        var name = "u" + Guid.NewGuid().ToString("N");
        var user = await Data(await admin.PostAsJsonAsync("/api/users", NewUser(name)));
        var id = user.GetProperty("id").GetGuid();
        using var oldSession = await Login(name, "Test@12345");
        await Data(await admin.PutAsJsonAsync($"/api/users/{id}", Edit(name, status: "Locked")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/users")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { username = name, password = "Test@12345" })).StatusCode);
        await Data(await admin.PutAsJsonAsync($"/api/users/{id}", Edit(name)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/auth/me")).StatusCode);
        using var fresh = await Login(name, "Test@12345");
        await Data(await admin.PutAsJsonAsync($"/api/users/{id}", Edit(name, role: "Technician")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.GetAsync("/api/auth/me")).StatusCode);
        using var technician = await Login(name, "Test@12345");
        Assert.Equal("Technician", (await Data(await technician.GetAsync("/api/auth/me"))).GetProperty("role").GetString());
    }

    /// <summary>Kiểm thử tạo tài khoản trùng đồng thời là nguyên tử và không phân biệt hoa thường.</summary>
    [Fact]
    public async Task Concurrent_duplicate_creation_is_atomic_and_case_insensitive()
    {
        using var admin = await Login();
        var name = "u" + Guid.NewGuid().ToString("N");
        var results = await Task.WhenAll(admin.PostAsJsonAsync("/api/users", NewUser(name)), admin.PostAsJsonAsync("/api/users", NewUser(name.ToUpperInvariant())));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.AppUsers.CountAsync(u => u.Username.ToLower() == name));
        Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedUserName == name.ToUpperInvariant()));
    }

    /// <summary>Kiểm thử từ chối tự tạo quản trị viên, tự khóa và nâng quyền trái phép.</summary>
    [Fact]
    public async Task Admin_creation_self_lock_and_role_escalation_are_rejected()
    {
        using var admin = await Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", NewUser("otheradmin", "Admin"))).StatusCode);
        var me = await Data(await admin.GetAsync("/api/auth/me"));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{me.GetProperty("id").GetGuid()}", Edit("admin", status: "Locked"))).StatusCode);
        var name = "u" + Guid.NewGuid().ToString("N");
        var employee = await Data(await admin.PostAsJsonAsync("/api/users", NewUser(name)));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{employee.GetProperty("id").GetGuid()}", Edit(name, "Admin"))).StatusCode);
    }

    /// <summary>Kiểm thử từ chối dữ liệu không hợp lệ và thao tác quản lý từ người dùng ẩn danh.</summary>
    [Fact]
    public async Task Invalid_input_and_anonymous_management_are_rejected()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users")).StatusCode);
        using var admin = await Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new { username = " ", email = "invalid", fullName = "", department = "IT", role = "Invalid", password = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/users?page=0&pageSize=100000")).StatusCode);
    }
}
