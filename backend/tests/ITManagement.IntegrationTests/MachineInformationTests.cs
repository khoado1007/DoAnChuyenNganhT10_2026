using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class MachineInformationTests(ServerApiFactory factory) : IClassFixture<ServerApiFactory>
{
    [Fact]
    public async Task Specification_roundtrips_and_requires_current_users_live_machine_session()
    {
        using var admin = await Login();
        var assetId = await Asset(admin);
        var unknown = await Data(await admin.GetAsync($"/api/assets/{assetId}/specification"));
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("memoryGb").ValueKind);
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("collectorVersion").ValueKind);
        Assert.Empty(unknown.GetProperty("software").EnumerateArray());
        var request = new
        {
            operatingSystem = "Windows 11 (simulated)", processor = "Sample CPU", memoryGb = 16.5m,
            diskTotalGb = 512m, diskFreeGb = 123.25m, diskEncryption = true,
            collectorVersion = "manual-demo", privateIp = "192.168.1.25", publicIp = "203.0.113.4",
            lastRestartedAt = "2026-11-15T10:00:00Z",
            software = new[] { new { name = "Editor", version = "1.2", type = "Utility", vulnerabilities = "Demo note",
                lastUsedAt = "2026-11-15T11:00:00Z", filePath = @"C:\Apps\Editor.exe" } }
        };
        await Data(await admin.PutAsJsonAsync($"/api/assets/{assetId}/specification", request));
        using var employee = await Employee(admin);
        using var otherEmployee = await Employee(admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/assets/{assetId}/specification")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync($"/api/assets/{assetId}/specification", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/machines/{assetId}/information")).StatusCode);
        var session = await Data(await employee.PostAsJsonAsync($"/api/machines/{assetId}/sessions", new { }));
        var sessionId = session.GetProperty("sessionId").GetGuid();
        employee.DefaultRequestHeaders.Add("X-Device-Session", sessionId.ToString());
        otherEmployee.DefaultRequestHeaders.Add("X-Device-Session", sessionId.ToString());
        var info = await Data(await employee.GetAsync($"/api/machines/{assetId}/information"));
        Assert.Equal("simulated", info.GetProperty("status").GetString());
        Assert.Equal(16.5m, info.GetProperty("memoryGb").GetDecimal());
        Assert.Equal(123.25m, info.GetProperty("diskFreeGb").GetDecimal());
        Assert.Equal("Editor", info.GetProperty("software")[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.String, info.GetProperty("updatedAt").ValueKind);
        Assert.False(info.TryGetProperty("softwareJson", out _));
        Assert.False(info.TryGetProperty("userId", out _));
        Assert.False(info.TryGetProperty("credential", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await otherEmployee.GetAsync($"/api/machines/{assetId}/information")).StatusCode);
        var anotherAsset = await Asset(admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/machines/{anotherAsset}/information")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.MachineSpecifications.AsNoTracking().SingleAsync(x => x.AssetId == assetId);
            Assert.Equal("Sample CPU", saved.Processor);
            Assert.Contains("Editor", saved.SoftwareJson);
            (await db.DeviceSessions.SingleAsync(x => x.Id == sessionId)).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync($"/api/machines/{assetId}/information")).StatusCode);
    }

    [Fact]
    public async Task Specification_rejects_invalid_bounds_nested_software_and_missing_assets()
    {
        using var admin = await Login();
        var assetId = await Asset(admin);
        var route = $"/api/assets/{assetId}/specification";
        foreach (var invalid in new object[]
        {
            new { diskTotalGb = 20, diskFreeGb = 21 }, new { diskFreeGb = 1 }, new { memoryGb = -1 },
            new { operatingSystem = new string('x', 201) }, new { publicIp = "invalid-ip" },
            new { software = new[] { new { name = "" } } },
            new { software = new[] { new { name = "Editor", filePath = new string('x', 1001) } } },
            new { software = new object?[] { null } },
            new { software = Enumerable.Range(0, 201).Select(x => new { name = "App" + x }).ToArray() }
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(route, invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/assets/{Guid.NewGuid()}/specification")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/assets/{Guid.NewGuid()}/specification", new { })).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/machines/{assetId}/information")).StatusCode);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }

    private async Task<HttpClient> Login(string username = "admin", string password = "Admin@12345")
    {
        var client = factory.CreateClient();
        var data = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<HttpClient> Employee(HttpClient admin)
    {
        var username = "inventory" + Guid.NewGuid().ToString("N");
        await Data(await admin.PostAsJsonAsync("/api/users", new
        {
            username, email = username + "@test.local", fullName = "Inventory employee", department = "Engineering",
            password = "Test@12345", role = "Employee"
        }));
        return await Login(username, "Test@12345");
    }

    private static async Task<Guid> Asset(HttpClient admin)
        => (await Data(await admin.PostAsJsonAsync("/api/assets", new
        {
            assetCode = "INFO-" + Guid.NewGuid().ToString("N"), name = "Simulated workstation",
            serialNumber = "SIM-123", manufacturer = "Demo", model = "PC"
        }))).GetProperty("id").GetGuid();
}
