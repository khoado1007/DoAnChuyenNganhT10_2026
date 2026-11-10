using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class DeviceSessionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }

    private async Task<HttpClient> Login(string name = "admin", string password = "Admin@12345")
    {
        var client = factory.CreateClient();
        var data = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username = name, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task Different_employees_cannot_share_or_end_each_others_session()
    {
        using var admin = await Login();
        var firstName = "first" + Guid.NewGuid().ToString("N");
        var secondName = "second" + Guid.NewGuid().ToString("N");
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = Guid.NewGuid().ToString("N"), name = "Shared PC" }));
        var id = asset.GetProperty("id").GetGuid();
        foreach (var name in new[] { firstName, secondName })
        {
            var user = await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = name, department = "IT", password = "Test@12345", role = "Employee" }));
            await Data(await admin.PutAsJsonAsync($"/api/assets/{id}/access/{user.GetProperty("id").GetGuid()}", new { }));
        }
        using var first = await Login(firstName, "Test@12345");
        using var second = await Login(secondName, "Test@12345");
        var results = await Task.WhenAll(first.PostAsJsonAsync($"/api/machines/{id}/sessions", new { }), second.PostAsJsonAsync($"/api/machines/{id}/sessions", new { }));
        var winner = results[0].IsSuccessStatusCode ? first : second;
        var loser = results[0].IsSuccessStatusCode ? second : first;
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        var session = await Data(results.Single(r => r.IsSuccessStatusCode));
        var sessionId = session.GetProperty("sessionId").GetString();
        loser.DefaultRequestHeaders.Add("X-Device-Session", sessionId);
        Assert.Equal(HttpStatusCode.Forbidden, (await loser.PostAsJsonAsync($"/api/machines/{id}/logout", new { })).StatusCode);
        winner.DefaultRequestHeaders.Add("X-Device-Session", sessionId);
        await Data(await winner.PostAsJsonAsync($"/api/machines/{id}/logout", new { }));
        var other = await Data(await loser.PostAsJsonAsync($"/api/machines/{id}/sessions", new { }));
        Assert.NotEqual(session.GetProperty("profileId").GetGuid(), other.GetProperty("profileId").GetGuid());
    }

    [Fact]
    public async Task Concurrent_sessions_are_exclusive_and_revoke_preserves_profile()
    {
        using var admin = await Login();
        var name = "session" + Guid.NewGuid().ToString("N");
        var user = await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = "Session test", department = "IT", password = "Test@12345", role = "Employee" }));
        var userId = user.GetProperty("id").GetGuid();
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = Guid.NewGuid().ToString("N"), name = "Session PC" }));
        var id = asset.GetProperty("id").GetGuid();
        await Data(await admin.PutAsJsonAsync($"/api/assets/{id}/access/{userId}", new { }));
        using var employee = await Login(name, "Test@12345");
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var session = await Data(responses.Single(r => r.StatusCode == HttpStatusCode.OK));
        var profileId = session.GetProperty("profileId").GetGuid();
        employee.DefaultRequestHeaders.Add("X-Device-Session", session.GetProperty("sessionId").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/machines/{Guid.NewGuid()}/logout", new { })).StatusCode);
        await Data(await employee.PostAsJsonAsync($"/api/machines/{id}/logout", new { }));
        var next = await Data(await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { }));
        Assert.Equal(profileId, next.GetProperty("profileId").GetGuid());
        Assert.NotEqual(session.GetProperty("sessionId").GetGuid(), next.GetProperty("sessionId").GetGuid());
        await Data(await admin.DeleteAsync($"/api/assets/{id}/access/{userId}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { })).StatusCode);
        await Data(await admin.PutAsJsonAsync($"/api/assets/{id}/access/{userId}", new { }));
        var restored = await Data(await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { }));
        Assert.Equal(profileId, restored.GetProperty("profileId").GetGuid());
        // Phiên cũ không hồi sinh khi cấp lại grant.
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/machines/{id}/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/machines/{id}/sessions", new { })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var revokedGrant = await db.DeviceAccessGrants.SingleAsync(g => g.AssetId == id && g.UserId == userId && g.RevokedAt != null);
        Assert.True(await db.AppAuditEvents.AnyAsync(a => a.Action == "DeviceAccessRevoked" && a.ResourceId == revokedGrant.Id));
        var restoredId = restored.GetProperty("sessionId").GetGuid();
        await db.DeviceSessions.Where(s => s.Id == restoredId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        var afterExpiry = await Data(await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { }));
        Assert.Equal(profileId, afterExpiry.GetProperty("profileId").GetGuid());
        Assert.NotNull((await db.DeviceSessions.AsNoTracking().SingleAsync(s => s.Id == restoredId)).EndedAt);
        var profile = await db.LocalProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        Assert.Equal(10, profile.MaxFiles);
        Assert.False(profile.Ready); // Session metadata chưa chứng minh provisioning trên Agent.
        Assert.Empty(await db.AssetAssignments.Where(a => a.AssetId == id).ToListAsync());

        // Cùng user trên máy khác nhận profile riêng, không sao chép dữ liệu.
        var otherAsset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = Guid.NewGuid().ToString("N"), name = "Other PC" }));
        var otherId = otherAsset.GetProperty("id").GetGuid();
        await Data(await admin.PutAsJsonAsync($"/api/assets/{otherId}/access/{userId}", new { }));
        var otherSession = await Data(await employee.PostAsJsonAsync($"/api/machines/{otherId}/sessions", new { }));
        Assert.NotEqual(profileId, otherSession.GetProperty("profileId").GetGuid());

        // Thay SecurityVersion khiến phiên cũ không giữ chỗ máy mãi sau khóa/mở user.
        await db.AppUsers.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { })).StatusCode);
        using var fresh = await Login(name, "Test@12345");
        Assert.Equal(HttpStatusCode.OK, (await fresh.PostAsJsonAsync($"/api/machines/{id}/sessions", new { })).StatusCode);
    }
}
