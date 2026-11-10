using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Application.Contracts.Simulation;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class ProfileOperationTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
        var body = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<(HttpClient client, Guid userId, string username)> Employee(HttpClient admin)
    {
        var username = "user" + Guid.NewGuid().ToString("N");
        var user = await Data(await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            email = username + "@test.local",
            fullName = "Employee " + username,
            department = "Engineering",
            password = "Test@12345",
            role = "Employee"
        }));
        var userId = user.GetProperty("id").GetGuid();
        var client = await Login(username, "Test@12345");
        return (client, userId, username);
    }

    private async Task<(Guid assetId, HttpClient agentClient, string credential)> EnrolledAsset(HttpClient admin)
    {
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = "DEV-" + Guid.NewGuid().ToString("N"),
            name = "Workstation PC"
        }));
        var assetId = asset.GetProperty("id").GetGuid();

        var issued = await Data(await admin.PostAsJsonAsync("/api/agents/enrollment-tokens", new
        {
            assetId,
            mode = "simulated"
        }));

        var agentClient = factory.CreateClient();
        var identity = await Data(await agentClient.PostAsJsonAsync("/api/agents/enroll", new
        {
            token = issued.GetProperty("token").GetString(),
            instanceId = Guid.NewGuid(),
            mode = "simulated",
            protocolVersion = 1
        }));

        var credential = identity.GetProperty("credential").GetString()!;
        agentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Agent", credential);
        return (assetId, agentClient, credential);
    }

    private static async Task<JsonElement> PollOperationAsync(HttpClient agent)
    {
        for (var i = 0; i < 30; i++)
        {
            var pollResp = await Data(await agent.PostAsJsonAsync("/api/agents/operations/poll", new { }));
            if (pollResp.TryGetProperty("operation", out var op) && op.ValueKind == JsonValueKind.Object)
            {
                return op;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Agent did not receive operation within timeout.");
    }

    [Fact]
    public async Task Profile_snapshot_and_file_operation_flow_with_agent_handshake()
    {
        using var admin = await Login();
        var (employee, userId, username) = await Employee(admin);
        var (assetId, agent, credential) = await EnrolledAsset(admin);

        // Cấp quyền truy cập máy cho nhân viên
        await Data(await admin.PutAsJsonAsync($"/api/assets/{assetId}/access/{userId}", new { }));

        // Bắt đầu phiên làm việc
        var sessionData = await Data(await employee.PostAsJsonAsync($"/api/machines/{assetId}/sessions", new { }));
        var sessionId = sessionData.GetProperty("sessionId").GetString()!;
        var profileId = sessionData.GetProperty("profileId").GetGuid();
        employee.DefaultRequestHeaders.Add("X-Device-Session", sessionId);

        // Lấy snapshot profile ban đầu
        var snapshot = await Data(await employee.GetAsync($"/api/machines/{assetId}/profile"));
        Assert.Equal("COMPANY\\" + username, snapshot.GetProperty("domainId").GetString());
        var quota = snapshot.GetProperty("quota");
        Assert.Equal(10, quota.GetProperty("maxFiles").GetInt32());
        Assert.Equal(0, quota.GetProperty("usedFiles").GetInt32());
        Assert.Equal(10, quota.GetProperty("remainingFiles").GetInt32());
        Assert.False(quota.GetProperty("overQuota").GetBoolean());

        // Nhân viên gửi lệnh tạo file (create)
        var requestId = Guid.NewGuid();
        var fileOpTask = employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId,
            folder = "Desktop",
            name = "document.txt",
            contentBase64 = Convert.ToBase64String("Hello World"u8.ToArray())
        });

        // Agent poll lệnh với retry loop
        var op = await PollOperationAsync(agent);
        var operationId = op.GetProperty("operationId").GetGuid();
        Assert.Equal("create", op.GetProperty("kind").GetString());

        // Agent authorize
        var authResp = await Data(await agent.PostAsJsonAsync($"/api/agents/operations/{operationId}/authorize", new { }));
        Assert.True(authResp.GetProperty("allowed").GetBoolean());

        // Agent báo cáo kết quả (result)
        var fileId = Guid.NewGuid();
        var resultPayload = new
        {
            success = true,
            files = new[]
            {
                new { fileId, name = "document.txt", folder = "Desktop", sizeBytes = 11, extension = ".txt" }
            }
        };
        var submitResp = await Data(await agent.PostAsJsonAsync($"/api/agents/operations/{operationId}/result", resultPayload));
        Assert.True(submitResp.GetProperty("accepted").GetBoolean());

        // Client nhận phản hồi kết quả
        var opResult = await Data(await fileOpTask);
        var updatedQuota = opResult.GetProperty("quota");
        Assert.Equal(1, updatedQuota.GetProperty("usedFiles").GetInt32());
        Assert.Equal(9, updatedQuota.GetProperty("remainingFiles").GetInt32());
        var files = opResult.GetProperty("files");
        Assert.Single(files.EnumerateArray());

        // Idempotency: gửi lại cùng requestId không tạo thao tác mới
        var replay = await Data(await employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId,
            folder = "Desktop",
            name = "document.txt",
            contentBase64 = Convert.ToBase64String("Hello World"u8.ToArray())
        }));
        Assert.Equal(1, replay.GetProperty("quota").GetProperty("usedFiles").GetInt32());

        var reusedKey = await employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId,
            folder = "Desktop",
            name = "different.txt",
            contentBase64 = Convert.ToBase64String("Hello World"u8.ToArray())
        });
        Assert.Equal(HttpStatusCode.Conflict, reusedKey.StatusCode);
    }

    [Fact]
    public async Task Unknown_reservation_is_not_released_when_operation_deadline_passes()
    {
        using var admin = await Login();
        var (employee, userId, _) = await Employee(admin);
        var (assetId, agent, _) = await EnrolledAsset(admin);
        await Data(await admin.PutAsJsonAsync($"/api/assets/{assetId}/access/{userId}", new { }));
        var session = await Data(await employee.PostAsJsonAsync($"/api/machines/{assetId}/sessions", new { }));
        var sessionId = session.GetProperty("sessionId").GetString()!;
        var profileId = session.GetProperty("profileId").GetGuid();
        employee.DefaultRequestHeaders.Add("X-Device-Session", sessionId);
        await Data(await admin.PutAsJsonAsync($"/api/profiles/{profileId}/quota", new { maxFiles = 1 }));

        var requestId = Guid.NewGuid();
        var pending = employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId,
            folder = "Desktop",
            name = "pending.txt"
        });
        _ = await PollOperationAsync(agent);
        var timeout = await pending;
        Assert.Equal(HttpStatusCode.GatewayTimeout, timeout.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var operation = await db.ProfileOperations.SingleAsync(x => x.RequestId == requestId);
            operation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var snapshot = await Data(await employee.GetAsync($"/api/machines/{assetId}/profile"));
        Assert.Equal(1, snapshot.GetProperty("quota").GetProperty("reservedFiles").GetInt32());
        Assert.Equal(0, snapshot.GetProperty("quota").GetProperty("remainingFiles").GetInt32());

        var next = await employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId = Guid.NewGuid(),
            folder = "Desktop",
            name = "next.txt"
        });
        Assert.Equal(HttpStatusCode.Conflict, next.StatusCode);

        var replay = await employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId,
            folder = "Desktop",
            name = "pending.txt"
        });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task Quota_exceeded_rejects_addition_before_agent_dispatch()
    {
        using var admin = await Login();
        var (employee, userId, _) = await Employee(admin);
        var (assetId, agent, _) = await EnrolledAsset(admin);

        await Data(await admin.PutAsJsonAsync($"/api/assets/{assetId}/access/{userId}", new { }));
        var sessionData = await Data(await employee.PostAsJsonAsync($"/api/machines/{assetId}/sessions", new { }));
        var sessionId = sessionData.GetProperty("sessionId").GetString()!;
        var profileId = sessionData.GetProperty("profileId").GetGuid();
        employee.DefaultRequestHeaders.Add("X-Device-Session", sessionId);

        // Admin chỉnh Quota thành 1 file
        await Data(await admin.PutAsJsonAsync($"/api/profiles/{profileId}/quota", new { maxFiles = 1 }));

        // Tạo 1 file thành công
        var req1 = Guid.NewGuid();
        var t1 = employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId = req1,
            folder = "Desktop",
            name = "one.txt"
        });

        var op1Element = await PollOperationAsync(agent);
        var op1 = op1Element.GetProperty("operationId").GetGuid();
        await agent.PostAsJsonAsync($"/api/agents/operations/{op1}/result", new
        {
            success = true,
            files = new[] { new { fileId = Guid.NewGuid(), name = "one.txt", folder = "Desktop", sizeBytes = 1, extension = ".txt" } }
        });
        await Data(await t1);

        // Tạo file thứ 2 bị từ chối 409 FILE_COUNT_QUOTA_EXCEEDED ngay lập tức
        var req2 = Guid.NewGuid();
        var resp2 = await employee.PostAsJsonAsync($"/api/machines/{assetId}/files/operations", new
        {
            kind = "create",
            requestId = req2,
            folder = "Desktop",
            name = "two.txt"
        });
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_profiles_and_update_quota_with_audit()
    {
        using var admin = await Login();
        var (employee, userId, username) = await Employee(admin);
        var (assetId, _, _) = await EnrolledAsset(admin);

        await Data(await admin.PutAsJsonAsync($"/api/assets/{assetId}/access/{userId}", new { }));
        var sessionData = await Data(await employee.PostAsJsonAsync($"/api/machines/{assetId}/sessions", new { }));
        var profileId = sessionData.GetProperty("profileId").GetGuid();

        // Admin liệt kê danh sách profile
        var profiles = await Data(await admin.GetAsync("/api/profiles"));
        Assert.Contains(profiles.EnumerateArray(), p => p.GetProperty("profileId").GetGuid() == profileId);

        // Admin cập nhật quota
        var updateResp = await Data(await admin.PutAsJsonAsync($"/api/profiles/{profileId}/quota", new { maxFiles = 25 }));
        Assert.Equal(25, updateResp.GetProperty("maxFiles").GetInt32());

        // Kiểm tra audit và cơ sở dữ liệu
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.LocalProfiles.SingleAsync(p => p.Id == profileId);
        Assert.Equal(25, profile.MaxFiles);
        Assert.True(await db.AppAuditEvents.AnyAsync(a => a.Action == "ProfileQuotaUpdated" && a.ResourceId == profileId));

        // Nhân viên không có quyền gọi API Admin
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync($"/api/profiles/{profileId}/quota", new { maxFiles = 50 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/profiles")).StatusCode);
    }
}
