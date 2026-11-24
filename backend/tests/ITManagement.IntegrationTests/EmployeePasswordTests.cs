using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class EmployeePasswordTests(ServerApiFactory factory) : IClassFixture<ServerApiFactory>
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    private async Task<HttpClient> Login(string name, string password)
    {
        var client = factory.CreateClient();
        var login = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username = name, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        return client;
    }
    private static object Change(string current = "Test@12345", string next = "Changed@12345", string confirm = "Changed@12345")
        => new { currentPassword = current, newPassword = next, confirmPassword = confirm };

    [Fact]
    public async Task Change_password_is_private_validated_and_revokes_tokens_and_device_sessions()
    {
        using var admin = await Login("admin", "Admin@12345");
        var name = "pw" + Guid.NewGuid().ToString("N");
        await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@example.test", fullName = "Password Test", department = "IT", role = "Employee", password = "Test@12345" }));
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = name, name = "Password machine" }));
        var device = asset.GetProperty("id").GetGuid();
        using var employee = await Login(name, "Test@12345");
        using var otherTab = await Login(name, "Test@12345");
        using var anonymous = factory.CreateClient();
        var session = await Data(await employee.PostAsJsonAsync($"/api/machines/{device}/sessions", new {}));
        var sessionId = session.GetProperty("sessionId").GetString();
        employee.DefaultRequestHeaders.Add("X-Device-Session", sessionId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/change-password", Change())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/auth/change-password", Change())).StatusCode);
        foreach (var invalid in new[] { Change(current: "wrong"), Change(confirm: "Different@123"), Change(next: "short", confirm: "short"), Change(next: "Test@12345", confirm: "Test@12345") })
            Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync("/api/auth/change-password", invalid)).StatusCode);
        await Data(await employee.GetAsync("/api/auth/me"));
        await Data(await employee.PostAsJsonAsync("/api/auth/change-password", Change()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherTab.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { username = name, password = "Test@12345" })).StatusCode);
        using var fresh = await Login(name, "Changed@12345");
        fresh.DefaultRequestHeaders.Add("X-Device-Session", sessionId);
        Assert.Equal(HttpStatusCode.Forbidden, (await fresh.GetAsync($"/api/machines/{device}/profile")).StatusCode);
        await Data(await fresh.PostAsJsonAsync($"/api/machines/{device}/sessions", new {}));
        await Data(await admin.GetAsync("/api/auth/me"));
    }
}
