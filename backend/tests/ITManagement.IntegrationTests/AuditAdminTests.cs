using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ITManagement.Domain.Entities.Simulation;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

/// <summary>
/// Kiểm thử mốc "nhật ký + metadata file cho Admin": quyền 401/403, bộ lọc thời gian/người/thao tác,
/// thứ tự mới nhất trước, phân trang và việc không trả nội dung file hay thông tin nội bộ.
/// </summary>
public sealed class AuditAdminTests(ServerApiFactory factory) : IClassFixture<ServerApiFactory>
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }

    private async Task<HttpClient> Login(string username = "admin", string password = "Admin@12345")
    {
        var client = factory.CreateClient();
        var auth = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    private static Task<HttpResponseMessage> Op(HttpClient client, Guid asset, object op) =>
        client.PostAsJsonAsync($"/api/machines/{asset}/files/operations", op);

    /// <summary>Tạo nhân viên, máy, phiên đăng nhập máy và một file để sinh nhật ký thật.</summary>
    private async Task<(HttpClient admin, HttpClient employee, Guid userId, Guid assetId, Guid fileId)> Setup()
    {
        var admin = await Login();
        var name = "aud" + Guid.NewGuid().ToString("N");
        var user = await Data(await admin.PostAsJsonAsync("/api/users", new
        {
            username = name,
            email = name + "@test.local",
            fullName = "Audit Employee",
            department = "IT",
            password = "Test@12345",
            role = "Employee"
        }));
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = Guid.NewGuid().ToString("N"), name = "Audit PC" }));
        var assetId = asset.GetProperty("id").GetGuid();
        var employee = await Login(name, "Test@12345");
        var session = await Data(await employee.PostAsJsonAsync($"/api/machines/{assetId}/sessions", new { }));
        employee.DefaultRequestHeaders.Add("X-Device-Session", session.GetProperty("sessionId").GetString());
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes("Báo cáo tháng 11"));
        var created = await Data(await Op(employee, assetId, new { kind = "create", requestId = Guid.NewGuid(), folder = "Desktop", name = "bao_cao.txt", contentBase64 = content }));
        return (admin, employee, user.GetProperty("id").GetGuid(), assetId, created.GetProperty("files")[0].GetProperty("fileId").GetGuid());
    }

    /// <summary>Ghi thẳng sự kiện nhật ký với thời điểm xác định để kiểm thử thứ tự, bộ lọc và phân trang.</summary>
    private void Seed(params (Guid? ActorId, string Action, DateTime At)[] events)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var e in events)
            db.AppAuditEvents.Add(new AppAuditEvent
            {
                ActorId = e.ActorId,
                Action = e.Action,
                ResourceId = Guid.NewGuid(),
                CreatedAt = e.At
            });
        db.SaveChanges();
    }

    /// <summary>Nhật ký và metadata file chỉ dành cho Admin; ẩn danh trả 401, Employee trả 403.</summary>
    [Fact]
    public async Task Audit_log_and_file_metadata_are_admin_only()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/audit-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/files")).StatusCode);

        var (admin, employee, _, _, _) = await Setup();
        using var a = admin; using var e = employee;
        Assert.Equal(HttpStatusCode.Forbidden, (await e.GetAsync("/api/admin/audit-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.GetAsync("/api/admin/files")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.GetAsync($"/api/admin/audit-log?action=ServerFileCreate")).StatusCode);
        // Admin không được đọc hồ sơ máy của nhân viên qua đường dẫn mô phỏng.
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/admin/files")).StatusCode);
    }

    /// <summary>Sự kiện thật từ luồng mô phỏng xuất hiện trong nhật ký và lọc được theo người thao tác.</summary>
    [Fact]
    public async Task Real_simulation_events_are_queryable_by_actor_and_action()
    {
        var (admin, employee, userId, assetId, _) = await Setup();
        using var a = admin; using var e = employee;

        var byActor = await Data(await a.GetAsync($"/api/admin/audit-log?actorId={userId}&pageSize=50"));
        var actions = byActor.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("action").GetString()).ToList();
        Assert.DoesNotContain("DeviceSessionStarted", actions);
        Assert.Contains("ServerFileCreate", actions);
        await Data(await e.PostAsJsonAsync($"/api/machines/{assetId}/logout", new { }));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.AppAuditEvents.AnyAsync(x => x.ActorId == userId && (x.Action == "DeviceSessionStarted" || x.Action == "DeviceSessionEnded")));
        }
        Assert.All(byActor.GetProperty("items").EnumerateArray(), x => Assert.Equal(userId, x.GetProperty("actorId").GetGuid()));

        var byAction = await Data(await a.GetAsync("/api/admin/audit-log?action=ServerFileCreate"));
        Assert.Contains(byAction.GetProperty("items").EnumerateArray(), x => x.GetProperty("actorId").GetGuid() == userId);

        // Nhật ký không trả token, mật khẩu hay nội dung file.
        var raw = await (await a.GetAsync($"/api/admin/audit-log?actorId={userId}&pageSize=50")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("contentBase64", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nhật ký trả mới nhất trước, lọc theo khoảng thời gian và phân trang đúng số bản ghi.</summary>
    [Fact]
    public async Task Audit_log_orders_filters_and_paginates()
    {
        using var admin = await Login();
        var adminId = (await Data(await admin.GetAsync("/api/auth/me"))).GetProperty("id").GetGuid();
        var adminName = (await Data(await admin.GetAsync("/api/auth/me"))).GetProperty("fullName").GetString();

        var start = new DateTime(2026, 3, 9, 8, 0, 0, DateTimeKind.Utc);
        Seed(
            (adminId, "ServerFileCreate", start),
            (adminId, "ServerFileWrite", start.AddMinutes(30)),
            (null, "ServerFileDelete", start.AddMinutes(60)));

        var range = $"from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(start.AddHours(1).ToString("O"))}";
        var all = await Data(await admin.GetAsync($"/api/admin/audit-log?{range}&pageSize=10"));
        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, all.GetProperty("items").GetArrayLength());
        var actions = all.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("action").GetString()).ToList();
        Assert.Equal(new[] { "ServerFileDelete", "ServerFileWrite", "ServerFileCreate" }, actions);
        var named = all.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("action").GetString() == "ServerFileCreate");
        Assert.Equal(adminName, named.GetProperty("actorName").GetString());
        Assert.EndsWith("Z", named.GetProperty("createdAt").GetString());

        var byActor = await Data(await admin.GetAsync($"/api/admin/audit-log?{range}&actorId={adminId}"));
        Assert.Equal(2, byActor.GetProperty("totalCount").GetInt32());

        var from = Uri.EscapeDataString(start.AddMinutes(20).ToString("O"));
        var to = Uri.EscapeDataString(start.AddMinutes(40).ToString("O"));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/admin/audit-log?from={to}&to={from}")).StatusCode);
        var window = await Data(await admin.GetAsync($"/api/admin/audit-log?from={from}&to={to}"));
        Assert.Equal(1, window.GetProperty("totalCount").GetInt32());
        Assert.Equal("ServerFileWrite", window.GetProperty("items")[0].GetProperty("action").GetString());

        var secondPage = await Data(await admin.GetAsync($"/api/admin/audit-log?{range}&page=2&pageSize=1"));
        Assert.Equal("ServerFileWrite", secondPage.GetProperty("items")[0].GetProperty("action").GetString());
        Assert.Equal(3, secondPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, secondPage.GetProperty("page").GetInt32());
        Assert.True(secondPage.GetProperty("hasNextPage").GetBoolean());

        // pageSize ngoài biên được kẹp về 1..200; page nhỏ hơn 1 được đưa về 1.
        var clamped = await Data(await admin.GetAsync($"/api/admin/audit-log?{range}&page=0&pageSize=0"));
        Assert.Equal(1, clamped.GetProperty("page").GetInt32());
        Assert.Equal(1, clamped.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, clamped.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Legacy_file_actions_are_normalized_and_non_file_history_is_preserved_but_hidden()
    {
        using var admin = await Login();
        var actor = Guid.NewGuid();
        var at = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Seed((actor, "ServerFilecreate", at), (actor, "DeviceSessionStarted", at),
            (actor, "ProfileQuotaUpdated", at), (actor, "ServerFileRead", at));
        var result = await Data(await admin.GetAsync($"/api/admin/audit-log?actorId={actor}"));
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
        Assert.Equal("ServerFileCreate", result.GetProperty("items")[0].GetProperty("action").GetString());
        Assert.EndsWith("Z", result.GetProperty("items")[0].GetProperty("createdAt").GetString());
        var filtered = await Data(await admin.GetAsync($"/api/admin/audit-log?actorId={actor}&action=ServerFileCreate"));
        Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
        var hidden = await Data(await admin.GetAsync($"/api/admin/audit-log?actorId={actor}&action=DeviceSessionStarted"));
        Assert.Equal(0, hidden.GetProperty("totalCount").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, await db.AppAuditEvents.CountAsync(x => x.ActorId == actor));
        Assert.True(await db.AppAuditEvents.AnyAsync(x => x.ActorId == actor && x.Action == "ServerFilecreate"));
    }

    /// <summary>Metadata file chỉ có tên/thư mục/dung lượng/chủ sở hữu/máy/kho, không có nội dung hay đường dẫn lưu trữ.</summary>
    [Fact]
    public async Task File_metadata_lists_descriptive_fields_only()
    {
        var (admin, employee, userId, assetId, fileId) = await Setup();
        using var a = admin; using var e = employee;

        var page = await Data(await a.GetAsync($"/api/admin/files?userId={userId}&assetId={assetId}"));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        var item = page.GetProperty("items")[0];
        Assert.Equal(fileId, item.GetProperty("id").GetGuid());
        Assert.Equal("Desktop", item.GetProperty("folder").GetString());
        Assert.Equal("bao_cao.txt", item.GetProperty("name").GetString());
        Assert.Equal(".txt", item.GetProperty("extension").GetString());
        Assert.Equal("Server", item.GetProperty("storageBackend").GetString());
        Assert.Equal("Audit Employee", item.GetProperty("userName").GetString());
        Assert.Equal("Audit PC", item.GetProperty("assetName").GetString());
        Assert.Equal(Encoding.UTF8.GetByteCount("Báo cáo tháng 11"), item.GetProperty("sizeBytes").GetInt64());

        var raw = await (await a.GetAsync($"/api/admin/files?userId={userId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("contentBase64", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profilePath", raw, StringComparison.OrdinalIgnoreCase);

        // Dữ liệu trống và bộ lọc không khớp trả trang rỗng, không lỗi.
        var empty = await Data(await a.GetAsync($"/api/admin/files?userId={Guid.NewGuid()}"));
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, empty.GetProperty("items").GetArrayLength());
        var otherFolder = await Data(await a.GetAsync($"/api/admin/files?userId={userId}&folder=Documents"));
        Assert.Equal(0, otherFolder.GetProperty("totalCount").GetInt32());
        var clamped = await Data(await a.GetAsync("/api/admin/files?page=0&pageSize=0"));
        Assert.Equal(1, clamped.GetProperty("page").GetInt32());
        Assert.Equal(1, clamped.GetProperty("pageSize").GetInt32());
    }
}
