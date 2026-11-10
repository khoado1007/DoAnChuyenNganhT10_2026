using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace ITManagement.IntegrationTests;

/// <summary>Khởi tạo bộ kiểm thử giao nhận tài sản với máy chủ API dùng chung.</summary>
public class AssignmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Đăng nhập người dùng kiểm thử và trả về client đã xác thực.
    private async Task<HttpClient> Login(string name = "admin", string password = "Admin@12345")
    {
        var client = factory.CreateClient();
        var data = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username = name, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }

    // Đọc dữ liệu phản hồi API sau khi kiểm tra trạng thái thành công.
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    // Tạo tài khoản nhân viên kiểm thử và trả về mã định danh cùng tên đăng nhập.
    private static async Task<(Guid id, string name)> Employee(HttpClient admin)
    {
        var name = "e" + Guid.NewGuid().ToString("N");
        var data = await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = name, department = "IT", role = "Employee", password = "Test@12345" }));
        return (data.GetProperty("id").GetGuid(), name);
    }
    // Tạo tài sản kiểm thử và trả về mã định danh.
    private static async Task<Guid> Asset(HttpClient admin) => (await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = "A" + Guid.NewGuid().ToString("N"), name = "Assigned laptop" }))).GetProperty("id").GetGuid();

    /// <summary>Kiểm thử giao tài sản đồng thời chỉ có một yêu cầu thành công và lịch sử theo chủ sở hữu còn nguyên sau hoàn trả hoặc thu hồi.</summary>
    [Fact]
    public async Task Concurrent_assignment_has_one_winner_and_owner_scoped_history_survives_return_and_revoke()
    {
        using var admin = await Login();
        var a = await Employee(admin); var b = await Employee(admin); var asset = await Asset(admin);
        var race = await Task.WhenAll(admin.PostAsJsonAsync($"/api/assets/{asset}/assign", new { userId = a.id, notes = "Issued" }), admin.PostAsJsonAsync($"/api/assets/{asset}/assign", new { userId = a.id, notes = "Issued" }));
        Assert.Single(race, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(race, r => r.StatusCode == HttpStatusCode.BadRequest);
        var assignment = await Data(race.Single(r => r.StatusCode == HttpStatusCode.OK));
        var assignmentId = assignment.GetProperty("id").GetGuid();
        using var owner = await Login(a.name, "Test@12345"); using var other = await Login(b.name, "Test@12345");
        Assert.Single((await Data(await owner.GetAsync("/api/me/assets"))).EnumerateArray());
        Assert.Empty((await Data(await other.GetAsync($"/api/me/assets?userId={a.id}"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/assets/{asset}/assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/assets/{asset}/return", new { assignmentId })).StatusCode);
        await Data(await admin.PostAsJsonAsync($"/api/assets/{asset}/return", new { assignmentId, notes = "Returned intact" }));
        Assert.Empty((await Data(await owner.GetAsync("/api/me/assets"))).EnumerateArray());
        var second = await Data(await admin.PostAsJsonAsync($"/api/assets/{asset}/assign", new { userId = b.id }));
        // Stale return must never close a newer allocation.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/assets/{asset}/return", new { assignmentId })).StatusCode);
        await Data(await admin.PostAsJsonAsync($"/api/assets/{asset}/revoke", new { assignmentId = second.GetProperty("id").GetGuid(), notes = "Replacement" }));
        var history = await Data(await admin.GetAsync($"/api/assets/{asset}/assignments"));
        Assert.Equal(2, history.GetArrayLength());
        Assert.Contains(history.EnumerateArray(), x => x.GetProperty("status").GetString() == "Returned" && x.GetProperty("notes").GetString() == "Issued" && x.GetProperty("closedBy").GetString() == "admin" && x.GetProperty("closeNotes").GetString() == "Returned intact");
        Assert.Contains(history.EnumerateArray(), x => x.GetProperty("status").GetString() == "Revoked");
        Assert.Equal("Available", (await Data(await admin.GetAsync($"/api/assets/{asset}"))).GetProperty("status").GetString());
    }
    /// <summary>Kiểm thử từ chối tài sản không khả dụng, người nhận bị khóa và yêu cầu không hợp lệ.</summary>
    [Fact]
    public async Task Unavailable_asset_locked_recipient_and_invalid_requests_are_rejected()
    {
        using var admin = await Login(); var employee = await Employee(admin); var asset = await Asset(admin);
        await Data(await admin.PutAsJsonAsync($"/api/users/{employee.id}", new { email = employee.name + "@test.local", fullName = employee.name, department = "IT", role = "Employee", status = "Locked" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/assets/{asset}/assign", new { userId = employee.id })).StatusCode);
        var active = await Employee(admin);
        await Data(await admin.PutAsJsonAsync($"/api/assets/{asset}", new { name = "Retired", type = "Desktop", status = "Retired" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/assets/{asset}/assign", new { userId = active.id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/assets/{asset}/return", new { assignmentId = Guid.NewGuid() })).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/assets")).StatusCode);
    }
}

