using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class SimulationTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    private static async Task<(Guid id, string username)> Employee(HttpClient admin)
    {
        var username = "sim" + Guid.NewGuid().ToString("N");
        var user = await Data(await admin.PostAsJsonAsync("/api/users", new { username, email = username + "@test.local", fullName = "Demo", password = "Test@12345", department = "IT", role = "Employee" }));
        return (user.GetProperty("id").GetGuid(), username);
    }

    [Fact]
    public async Task Domain_alias_uses_existing_identity_and_rejects_other_domains()
    {
        using var admin = await Login();
        var user = await Employee(admin);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { username = "company\\" + user.username, password = "Test@12345" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = "OTHER\\" + user.username, password = "Test@12345" })).StatusCode);
    }

    [Fact]
    public async Task Grants_are_independent_of_assignment_and_can_be_revoked()
    {
        using var admin = await Login();
        var user = await Employee(admin);
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = "S" + Guid.NewGuid().ToString("N"), name = "Shared PC" }));
        var id = asset.GetProperty("id").GetGuid();
        using var employee = await Login(user.username, "Test@12345");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { })).StatusCode);
        await Data(await admin.PutAsJsonAsync($"/api/assets/{id}/access/{user.id}", new { }));
        var grants = await Data(await admin.GetAsync($"/api/assets/{id}/access"));
        Assert.Contains(grants.EnumerateArray(), x => x.GetProperty("userId").GetGuid() == user.id);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync($"/api/assets/{id}/access/{user.id}", new { })).StatusCode);
        await Data(await admin.DeleteAsync($"/api/assets/{id}/access/{user.id}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { })).StatusCode);
    }

    [Fact]
    public async Task Machine_selection_is_public_but_never_exposes_agent_credentials()
    {
        using var client = factory.CreateClient();
        var result = await client.GetAsync("/api/machines");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var text = await result.Content.ReadAsStringAsync();
        Assert.DoesNotContain("credential", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastSeen", text, StringComparison.OrdinalIgnoreCase);
    }
}
