using ITManagement.Application.Common.Interfaces;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ITManagement.IntegrationTests;

public class AgentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;
    /// <summary>Khởi tạo bộ kiểm thử agent với máy chủ API dùng chung.</summary>
    public AgentTests(ApiFactory factory) => this.factory = factory;

    // Tạo hoặc lấy client đã xác thực với quyền quản trị viên.
    private async Task<HttpClient> Admin()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "Admin@12345" });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }

    // Đọc dữ liệu phản hồi API sau khi kiểm tra trạng thái thành công.
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    /// <summary>Kiểm thử người dùng ẩn danh bị từ chối yêu cầu token đăng ký agent.</summary>
    [Fact]
    public async Task Anonymous_cannot_issue_tokens()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/agents/enrollment-tokens", new { assetId = Guid.NewGuid(), mode = "real" })).StatusCode);
    }

    /// <summary>Kiểm thử token đăng ký chỉ dùng một lần, heartbeat đúng phạm vi và agent bị vô hiệu hóa không thể gửi dữ liệu tiếp.</summary>
    [Fact]
    public async Task Enrollment_is_single_use_heartbeat_is_scoped_and_disable_blocks_it()
    {
        using var admin = await Admin();
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = "TEST-" + Guid.NewGuid().ToString("N"), name = "Test PC" }));
        var issued = await Data(await admin.PostAsJsonAsync("/api/agents/enrollment-tokens", new { assetId = asset.GetProperty("id").GetGuid(), mode = "simulated" }));
        using var agent = factory.CreateClient();
        var enrollment = new { token = issued.GetProperty("token").GetString(), instanceId = Guid.NewGuid(), mode = "simulated", protocolVersion = 1 };
        var identity = await Data(await agent.PostAsJsonAsync("/api/agents/enroll", enrollment));
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.PostAsJsonAsync("/api/agents/enroll", enrollment)).StatusCode);
        agent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Agent", identity.GetProperty("credential").GetString());
        var payload = new { sequence = 1L, protocolVersion = 1, hostname = "SIM-PC", os = "Synthetic OS", cpu = "Synthetic CPU", ramBytes = 1024L, agentVersion = "1.0.0" };
        await Data(await agent.PostAsJsonAsync("/api/agents/heartbeat", payload));
        await Data(await agent.PostAsJsonAsync("/api/agents/heartbeat", payload));
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsJsonAsync("/api/agents/heartbeat", payload)).StatusCode);
        var list = await Data(await admin.GetAsync("/api/agents"));
        var row = list.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == identity.GetProperty("agentId").GetGuid());
        Assert.Equal("simulated", row.GetProperty("mode").GetString());
        Assert.Equal("online", row.GetProperty("connectivity").GetString());
        Assert.Equal("Available", row.GetProperty("assetStatus").GetString());
        Assert.False(row.TryGetProperty("credentialHash", out _));
        await Data(await admin.PostAsJsonAsync($"/api/agents/{identity.GetProperty("agentId").GetGuid()}/disable", new { }));
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/agents/heartbeat", payload)).StatusCode);
    }
    // Tạo token đăng ký cho tài sản kiểm thử và trả về mã tài sản cùng token.
    private async Task<(Guid AssetId, string Token)> Issue(HttpClient admin, string mode = "real")
    {
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = "TEST-" + Guid.NewGuid().ToString("N"), name = "Enrollment PC" }));
        var id = asset.GetProperty("id").GetGuid();
        var token = await Data(await admin.PostAsJsonAsync("/api/agents/enrollment-tokens", new { assetId = id, mode }));
        return (id, token.GetProperty("token").GetString()!);
    }

    /// <summary>Kiểm thử người không phải quản trị viên không thể quản lý agent.</summary>
    [Theory]
    [InlineData("Employee")]
    [InlineData("Technician")]
    public async Task Non_admin_cannot_manage_agents(string role)
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ITManagement.Domain.Entities.User.AppUser
        {
            Username = "rbac" + Guid.NewGuid().ToString("N"), Email = Guid.NewGuid() + "@example.test",
            FullName = "RBAC test", Role = Enum.Parse<ITManagement.Domain.Enums.UserRole>(role)
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>().GenerateAccessToken(user.Id, user.Username, role, user.Email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/agents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/agents/enrollment-tokens", new { assetId = Guid.NewGuid(), mode = "real" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/agents/{Guid.NewGuid()}/disable", new { })).StatusCode);
    }

    /// <summary>Kiểm thử đăng ký đồng thời chỉ tiêu thụ token một lần và cơ sở dữ liệu chỉ lưu bản băm.</summary>
    [Fact]
    public async Task Concurrent_enrollment_consumes_token_exactly_once_and_persists_only_hashes()
    {
        using var admin = await Admin();
        var issued = await Issue(admin);
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/api/agents/enroll",
            new { token = issued.Token, instanceId = Guid.NewGuid(), mode = "real", protocolVersion = 1 })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
        var enrolled = await Data(responses.Single(r => r.IsSuccessStatusCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agent = await db.Agents.SingleAsync(a => a.AssetId == issued.AssetId);
        Assert.NotEqual(enrolled.GetProperty("credential").GetString(), agent.CredentialHash);
        var token = await db.EnrollmentTokens.SingleAsync(t => t.AssetId == issued.AssetId);
        Assert.NotEqual(issued.Token, token.TokenHash);
        Assert.NotNull(token.ConsumedAt);
    }

    /// <summary>Kiểm thử token hết hạn, sai mục đích hoặc không tồn tại đều bị từ chối.</summary>
    [Fact]
    public async Task Expired_wrong_mode_and_unknown_tokens_are_rejected()
    {
        using var admin = await Admin();
        var issued = await Issue(admin);
        using var client = factory.CreateClient();
        async Task<HttpStatusCode> Enroll(string token, string mode, int version = 1) =>
            (await client.PostAsJsonAsync("/api/agents/enroll", new { token, instanceId = Guid.NewGuid(), mode, protocolVersion = version })).StatusCode;
        Assert.Equal(HttpStatusCode.Unauthorized, await Enroll(issued.Token, "simulated"));
        Assert.Equal(HttpStatusCode.Unauthorized, await Enroll(new string('0', 64), "real"));
        Assert.Equal(HttpStatusCode.BadRequest, await Enroll(issued.Token, "real", 2));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.EnrollmentTokens.Where(t => t.AssetId == issued.AssetId).ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, await Enroll(issued.Token, "real"));
        Assert.Null((await db.EnrollmentTokens.SingleAsync(t => t.AssetId == issued.AssetId)).ConsumedAt);
    }

    /// <summary>Kiểm thử heartbeat phát lại không thể đưa agent ngoại tuyến trở lại trực tuyến hoặc thay đổi kiểm kê và vòng đời tài sản.</summary>
    [Fact]
    public async Task Replays_cannot_make_offline_agent_online_or_change_inventory_and_asset_lifecycle()
    {
        using var admin = await Admin();
        var issued = await Issue(admin);
        using var client = factory.CreateClient();
        var identity = await Data(await client.PostAsJsonAsync("/api/agents/enroll", new { token = issued.Token, instanceId = Guid.NewGuid(), mode = "real", protocolVersion = 1 }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Agent", identity.GetProperty("credential").GetString());
        object Payload(long sequence, string hostname) => new { sequence, protocolVersion = 1, hostname, os = "OS", cpu = "CPU", ramBytes = 2048L, agentVersion = "1" };
        await Data(await client.PostAsJsonAsync("/api/agents/heartbeat", Payload(2, "LATEST")));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Agents.Where(a => a.AssetId == issued.AssetId).ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, DateTime.UtcNow.AddMinutes(-2)));
        await Data(await client.PostAsJsonAsync("/api/agents/heartbeat", Payload(1, "STALE")));
        var rows = await Data(await admin.GetAsync("/api/agents"));
        var row = rows.EnumerateArray().Single(a => a.GetProperty("assetId").GetGuid() == issued.AssetId);
        Assert.Equal("offline", row.GetProperty("connectivity").GetString());
        Assert.Equal("active", row.GetProperty("policy").GetString());
        Assert.Equal("LATEST", row.GetProperty("hostname").GetString());
        Assert.Equal("Available", row.GetProperty("assetStatus").GetString());
        await Data(await client.PostAsJsonAsync("/api/agents/heartbeat", Payload(3, "RECONNECTED")));
        await db.Assets.Where(a => a.Id == issued.AssetId).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDeleted, true));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/agents/heartbeat", Payload(4, "BLOCKED"))).StatusCode);
        Assert.Equal("RECONNECTED", (await db.Agents.SingleAsync(a => a.AssetId == issued.AssetId)).Hostname);
    }    [Fact]
    /// <summary>Kiểm thử vô hiệu hóa đồng thời với heartbeat vẫn bảo toàn telemetry và chặn cập nhật tiếp theo.</summary>
    public async Task Concurrent_disable_and_heartbeat_preserve_telemetry_and_block_future_updates()
    {
        using var admin = await Admin();
        var issued = await Issue(admin);
        using var client = factory.CreateClient();
        var identity = await Data(await client.PostAsJsonAsync("/api/agents/enroll", new { token = issued.Token, instanceId = Guid.NewGuid(), mode = "real", protocolVersion = 1 }));
        var id = identity.GetProperty("agentId").GetGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Agent", identity.GetProperty("credential").GetString());
        object Payload(long sequence) => new { sequence, protocolVersion = 1, hostname = "RACE", os = "OS", cpu = "CPU", ramBytes = 2048L, agentVersion = "1" };
        await Data(await client.PostAsJsonAsync("/api/agents/heartbeat", Payload(1)));
        var heartbeat = client.PostAsJsonAsync("/api/agents/heartbeat", Payload(2));
        var disable = admin.PostAsJsonAsync($"/api/agents/{id}/disable", new { });
        await Task.WhenAll(heartbeat, disable);
        Assert.Contains((await heartbeat).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden });
        Assert.Equal(HttpStatusCode.OK, (await disable).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/agents/heartbeat", Payload(3))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Agents.SingleAsync(a => a.Id == id);
        Assert.NotNull(saved.DisabledAt);
        Assert.Equal("RACE", saved.Hostname);
        Assert.InRange(saved.LastSequence, 1, 2);
    }

    /// <summary>Kiểm thử cấp token khôi phục sẽ thu hồi agent cũ và từ chối credential cũ.</summary>
    [Fact]
    public async Task Admin_can_reissue_recovery_token_which_revokes_old_agent_and_rejects_old_credential()
    {
        using var admin = await Admin();
        var issued1 = await Issue(admin, "real");
        using var agent1 = factory.CreateClient();
        var id1 = Guid.NewGuid();
        var enrolled1 = await Data(await agent1.PostAsJsonAsync("/api/agents/enroll", new { token = issued1.Token, instanceId = id1, mode = "real", protocolVersion = 1 }));
        agent1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Agent", enrolled1.GetProperty("credential").GetString());
        var payload1 = new { sequence = 1L, protocolVersion = 1, hostname = "HOST-REAL", os = "Linux", cpu = "Intel", ramBytes = 8192L, agentVersion = "1.0.0" };
        await Data(await agent1.PostAsJsonAsync("/api/agents/heartbeat", payload1));

        // Normal enrollment-token request without ReplaceExisting fails
        var failIssue = await admin.PostAsJsonAsync("/api/agents/enrollment-tokens", new { assetId = issued1.AssetId, mode = "simulated" });
        Assert.Equal(HttpStatusCode.BadRequest, failIssue.StatusCode);

        // Recovery token succeeds and issues new single-use token
        var recoveryResponse = await admin.PostAsJsonAsync("/api/agents/recovery-tokens", new { assetId = issued1.AssetId, mode = "simulated", reason = "Machine reinstalled" });
        var recoveryTokenData = await Data(recoveryResponse);
        var recoveryToken = recoveryTokenData.GetProperty("token").GetString()!;
        Assert.Equal(64, recoveryToken.Length);

        // Old agent heartbeat is immediately rejected
        var staleHeartbeat = await agent1.PostAsJsonAsync("/api/agents/heartbeat", payload1 with { sequence = 2L });
        Assert.Equal(HttpStatusCode.Forbidden, staleHeartbeat.StatusCode);

        // New agent enrolls with the recovery token
        using var agent2 = factory.CreateClient();
        var id2 = Guid.NewGuid();
        var enrolled2 = await Data(await agent2.PostAsJsonAsync("/api/agents/enroll", new { token = recoveryToken, instanceId = id2, mode = "simulated", protocolVersion = 1 }));
        agent2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Agent", enrolled2.GetProperty("credential").GetString());
        var payload2 = new { sequence = 1L, protocolVersion = 1, hostname = "HOST-SIM", os = "Windows", cpu = "AMD", ramBytes = 16384L, agentVersion = "1.0.0" };
        await Data(await agent2.PostAsJsonAsync("/api/agents/heartbeat", payload2));

        // Verify list returns both agents with distinct policies, modes, and connectivity
        var list = await Data(await admin.GetAsync("/api/agents"));
        var rows = list.EnumerateArray().Where(a => a.GetProperty("assetId").GetGuid() == issued1.AssetId).ToList();
        Assert.Equal(2, rows.Count);

        var oldRow = rows.Single(r => r.GetProperty("instanceId").GetGuid() == id1);
        Assert.Equal("revoked", oldRow.GetProperty("policy").GetString());
        Assert.Equal("offline", oldRow.GetProperty("connectivity").GetString());
        Assert.Equal("real", oldRow.GetProperty("mode").GetString());
        Assert.Equal("Machine reinstalled", oldRow.GetProperty("revokedReason").GetString());
        Assert.True(oldRow.GetProperty("revokedAt").GetString() is not null);

        var newRow = rows.Single(r => r.GetProperty("instanceId").GetGuid() == id2);
        Assert.Equal("active", newRow.GetProperty("policy").GetString());
        Assert.Equal("online", newRow.GetProperty("connectivity").GetString());
        Assert.Equal("simulated", newRow.GetProperty("mode").GetString());
        Assert.Null(newRow.GetProperty("revokedAt").GetString());
    }

    /// <summary>Kiểm thử token khôi phục chỉ dùng một lần và đăng ký đồng thời chỉ tạo một agent.</summary>
    [Fact]
    public async Task Recovery_token_is_single_use_and_concurrent_enrollment_only_registers_one()
    {
        using var admin = await Admin();
        var issued1 = await Issue(admin, "real");
        using var agent1 = factory.CreateClient();
        await Data(await agent1.PostAsJsonAsync("/api/agents/enroll", new { token = issued1.Token, instanceId = Guid.NewGuid(), mode = "real", protocolVersion = 1 }));

        var recovery = await Data(await admin.PostAsJsonAsync("/api/agents/recovery-tokens", new { assetId = issued1.AssetId, mode = "real", reason = "Recovery concurrency test" }));
        var recoveryToken = recovery.GetProperty("token").GetString()!;

        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/api/agents/enroll",
            new { token = recoveryToken, instanceId = Guid.NewGuid(), mode = "real", protocolVersion = 1 })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activeAgents = await db.Agents.Where(a => a.AssetId == issued1.AssetId && a.RevokedAt == null).ToListAsync();
        Assert.Single(activeAgents);
    }

    /// <summary>Kiểm thử Swagger mô tả credential agent tách biệt với Bearer token người dùng.</summary>
    [Fact]
    public async Task Swagger_documents_agent_credential_separately_from_user_bearer()
    {
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/agents/heartbeat").GetProperty("post").GetProperty("security")[0].TryGetProperty("Agent", out _));
        Assert.Empty(paths.GetProperty("/api/agents/enroll").GetProperty("post").GetProperty("security")[0].EnumerateObject());
        Assert.True(paths.TryGetProperty("/api/agents/recovery-tokens", out _));
    }}

public class AgentRateLimitTests
{
    /// <summary>Kiểm thử giới hạn tần suất yêu cầu đăng ký agent.</summary>
    [Fact]
    public async Task Enrollment_attempts_are_rate_limited()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = new { token = new string('0', 64), instanceId = Guid.NewGuid(), mode = "real", protocolVersion = 1 };
        for (var attempt = 0; attempt < 20; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/agents/enroll", request)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/agents/enroll", request)).StatusCode);
    }
}
