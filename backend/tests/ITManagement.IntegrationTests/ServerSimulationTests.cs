using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class ServerApiFactory : ApiFactory { protected override bool UseAgent => false; }

public sealed class ServerSimulationTests(ServerApiFactory factory) : IClassFixture<ServerApiFactory>
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }
    private async Task<HttpClient> Login(string username = "admin", string password = "Admin@12345")
    {
        var c = factory.CreateClient();
        var auth = await Data(await c.PostAsJsonAsync("/api/auth/login", new { username, password }));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return c;
    }
    private async Task<(HttpClient admin, HttpClient employee, Guid asset, Guid session, Guid user)> Setup()
    {
        var admin = await Login(); var name = "web" + Guid.NewGuid().ToString("N");
        var user = await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = "Web Employee", department = "IT", password = "Test@12345", role = "Employee" }));
        var asset = await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = Guid.NewGuid().ToString("N"), name = "Web PC" }));
        var id = asset.GetProperty("id").GetGuid();
        var employee = await Login(name, "Test@12345");
        var session = await Data(await employee.PostAsJsonAsync($"/api/machines/{id}/sessions", new { }));
        var sid = session.GetProperty("sessionId").GetGuid();
        employee.DefaultRequestHeaders.Add("X-Device-Session", sid.ToString());
        return (admin, employee, id, sid, user.GetProperty("id").GetGuid());
    }
    private static Task<HttpResponseMessage> Op(HttpClient c, Guid asset, object op) => c.PostAsJsonAsync($"/api/machines/{asset}/files/operations", op);

    [Fact]
    public async Task Tab_departure_reserves_machine_until_deadline_and_a_newer_resume_cancels_it()
    {
        var (admin, employee, asset, session, _) = await Setup();
        using var a = admin;
        using var e = employee;
        var route = $"/api/machines/{asset}";
        Task<HttpResponseMessage> Page(long sequence, bool leaving) =>
            e.PostAsJsonAsync(route + "/page", new { sequence, leaving });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = db.DeviceSessions.Where(s => s.Id == session);
        Assert.Equal(HttpStatusCode.NoContent, (await Page(1, true)).StatusCode);
        var deadline = (await row.AsNoTracking().SingleAsync()).TabClosesAt;
        Assert.InRange(deadline!.Value, DateTime.UtcNow.AddSeconds(50), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(HttpStatusCode.Conflict, (await e.PostAsJsonAsync(route + "/sessions", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Page(3, false)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Page(2, true)).StatusCode);
        Assert.Null((await row.AsNoTracking().SingleAsync()).TabClosesAt);
        Assert.Equal(HttpStatusCode.OK, (await e.GetAsync(route + "/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Page(4, true)).StatusCode);
        await row.ExecuteUpdateAsync(s => s.SetProperty(x => x.TabClosesAt, DateTime.UtcNow.AddSeconds(-1)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Page(5, false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.GetAsync(route + "/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await e.PostAsJsonAsync(route + "/sessions", new { })).StatusCode);
    }

    [Fact]
    public async Task Active_employee_uses_machine_without_agent_or_grant_and_files_survive_new_session()
    {
        var (admin, employee, asset, session, _) = await Setup(); using var a = admin; using var e = employee;
        var machines = await Data(await factory.CreateClient().GetAsync("/api/machines"));
        Assert.Contains(machines.EnumerateArray(), m => m.GetProperty("id").GetGuid() == asset);
        var requestId = Guid.NewGuid(); var content = Convert.ToBase64String(Encoding.UTF8.GetBytes("Xin chào 🌱"));
        var create = new { kind = "create", requestId, folder = "Desktop", name = "bài.txt", contentBase64 = content };
        var first = await Data(await Op(e, asset, create));
        var file = first.GetProperty("files")[0].GetProperty("fileId").GetGuid();
        var replay = await Data(await Op(e, asset, create));
        Assert.Equal(1, replay.GetProperty("quota").GetProperty("usedFiles").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Op(e, asset, new { kind = "create", requestId, folder = "Desktop", name = "different.txt" })).StatusCode);
        await Data(await e.PostAsJsonAsync($"/api/machines/{asset}/logout", new { }));
        Assert.Equal(HttpStatusCode.Forbidden, (await e.GetAsync($"/api/machines/{asset}/profile")).StatusCode);
        var next = await Data(await e.PostAsJsonAsync($"/api/machines/{asset}/sessions", new { }));
        e.DefaultRequestHeaders.Remove("X-Device-Session"); e.DefaultRequestHeaders.Add("X-Device-Session", next.GetProperty("sessionId").GetString());
        var read = await Data(await Op(e, asset, new { kind = "read", requestId = Guid.NewGuid(), fileId = file }));
        Assert.Equal(content, read.GetProperty("contentBase64").GetString());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Agents.AnyAsync(x => x.AssetId == asset));
        Assert.False(await db.DeviceAccessGrants.AnyAsync(x => x.AssetId == asset));
    }

    [Fact]
    public async Task Server_file_mutations_enforce_quota_scope_and_preserve_binary_content()
    {
        var (admin, employee, asset, _, _) = await Setup(); using var a = admin; using var e = employee;
        var snapshot = await Data(await Op(e, asset, new { kind = "ensure", requestId = Guid.NewGuid() }));
        var profile = snapshot.GetProperty("profileId").GetGuid();
        await Data(await a.PutAsJsonAsync($"/api/profiles/{profile}/quota", new { maxFiles = 1 }));
        var bytes = Convert.ToBase64String(new byte[] { 0, 255, 12, 99, 10 });
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => Op(e, asset, new { kind = "create", requestId = Guid.NewGuid(), folder = "Documents", name = $"file{i}.pdf", contentBase64 = bytes })));
        Assert.Single(results, r => r.IsSuccessStatusCode); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        snapshot = await Data(await e.GetAsync($"/api/machines/{asset}/profile"));
        var file = snapshot.GetProperty("files")[0].GetProperty("fileId").GetGuid();
        await Data(await Op(e, asset, new { kind = "rename", requestId = Guid.NewGuid(), fileId = file, newName = "renamed.pdf" }));
        await Data(await Op(e, asset, new { kind = "move", requestId = Guid.NewGuid(), fileId = file, targetFolder = "Desktop" }));
        Assert.Equal(HttpStatusCode.Conflict, (await Op(e, asset, new { kind = "copy", requestId = Guid.NewGuid(), fileId = file, targetFolder = "Documents" })).StatusCode);
        await Data(await Op(e, asset, new { kind = "delete", requestId = Guid.NewGuid(), fileId = file }));
        var restored = await Data(await Op(e, asset, new { kind = "restore", requestId = Guid.NewGuid(), fileId = file, targetFolder = "Documents" }));
        Assert.Equal(file, restored.GetProperty("files")[0].GetProperty("fileId").GetGuid());
        Assert.Equal(bytes, (await Data(await Op(e, asset, new { kind = "read", requestId = Guid.NewGuid(), fileId = file }))).GetProperty("contentBase64").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await Op(e, asset, new { kind = "read", requestId = Guid.NewGuid(), fileId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync($"/api/machines/{asset}/profile")).StatusCode);
    }

    [Theory]
    [InlineData("../escape.txt")][InlineData("C:\\secret.txt")][InlineData("bad.exe")][InlineData("CON.txt")][InlineData("a/b.txt")]
    public async Task Unsafe_names_never_become_server_paths(string name)
    {
        var (admin, employee, asset, _, _) = await Setup(); using var a = admin; using var e = employee;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Op(e, asset, new { kind = "create", requestId = Guid.NewGuid(), folder = "Desktop", name })).StatusCode);
        var snapshot = await Data(await e.GetAsync($"/api/machines/{asset}/profile"));
        Assert.Empty(snapshot.GetProperty("files").EnumerateArray());
    }

    [Fact]
    public async Task Writes_and_copies_are_independent_and_scoped_to_user_and_machine()
    {
        var (admin, employee, asset, _, user) = await Setup(); using var a = admin; using var e = employee;
        var first = await Data(await Op(e, asset, new { kind = "create", requestId = Guid.NewGuid(), folder = "Desktop", name = "note.txt", contentBase64 = "b2xk" }));
        var file = first.GetProperty("files")[0].GetProperty("fileId").GetGuid();
        var copied = await Data(await Op(e, asset, new { kind = "copy", requestId = Guid.NewGuid(), fileId = file, targetFolder = "Documents" }));
        var copy = copied.GetProperty("files").EnumerateArray().Single(f => f.GetProperty("folder").GetString() == "Documents").GetProperty("fileId").GetGuid();
        await Data(await Op(e, asset, new { kind = "write", requestId = Guid.NewGuid(), fileId = file, contentBase64 = "bmV3" }));
        Assert.Equal("b2xk", (await Data(await Op(e, asset, new { kind = "read", requestId = Guid.NewGuid(), fileId = copy }))).GetProperty("contentBase64").GetString());
        Assert.Equal("bmV3", (await Data(await Op(e, asset, new { kind = "read", requestId = Guid.NewGuid(), fileId = file }))).GetProperty("contentBase64").GetString());
        var other = await Data(await a.PostAsJsonAsync("/api/assets", new { assetCode = Guid.NewGuid().ToString("N"), name = "Other PC" }));
        var otherId = other.GetProperty("id").GetGuid();
        var next = await Data(await e.PostAsJsonAsync($"/api/machines/{otherId}/sessions", new { }));
        e.DefaultRequestHeaders.Remove("X-Device-Session"); e.DefaultRequestHeaders.Add("X-Device-Session", next.GetProperty("sessionId").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await Op(e, otherId, new { kind = "read", requestId = Guid.NewGuid(), fileId = file })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.GetAsync($"/api/machines/{asset}/profile")).StatusCode);
        var anotherName = "another" + Guid.NewGuid().ToString("N");
        await Data(await a.PostAsJsonAsync("/api/users", new { username = anotherName, email = anotherName+"@test.local", fullName = "Another", department = "IT", password = "Test@12345", role = "Employee" }));
        using var otherEmployee = await Login(anotherName, "Test@12345");
        otherEmployee.DefaultRequestHeaders.Add("X-Device-Session", next.GetProperty("sessionId").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await otherEmployee.GetAsync($"/api/machines/{otherId}/profile")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Assets.Where(x => x.Id == otherId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ITManagement.Domain.Enums.AssetStatus.Disabled));
        Assert.Equal(HttpStatusCode.Forbidden, (await Op(e, otherId, new { kind = "list", requestId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.PostAsJsonAsync($"/api/machines/{otherId}/sessions", new { })).StatusCode);
        await db.AppUsers.Where(x => x.Id == user).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ITManagement.Domain.Enums.UserStatus.Locked));
        Assert.Equal(HttpStatusCode.Unauthorized, (await e.GetAsync($"/api/machines/{otherId}/profile")).StatusCode);
    }

    [Fact]
    public async Task Missing_content_does_not_drop_metadata_and_legacy_profile_is_not_silently_converted()
    {
        var (admin, employee, asset, _, _) = await Setup(); using var a = admin; using var e = employee;
        var snapshot = await Data(await Op(e, asset, new { kind = "create", requestId = Guid.NewGuid(), folder = "Desktop", name = "keep.txt", contentBase64 = "a2VlcA==" }));
        var fileId = snapshot.GetProperty("files")[0].GetProperty("fileId").GetGuid(); var profileId = snapshot.GetProperty("profileId").GetGuid();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ProfileFiles.AsNoTracking().SingleAsync(f => f.Id == fileId);
        Assert.NotNull(row.StorageKey);
        var file = Directory.GetFiles(Path.Combine(factory.StorageRoot,"simulation"), row.StorageKey!, SearchOption.AllDirectories).Single();
        Assert.Equal("keep", await File.ReadAllTextAsync(file));
        File.Move(file, file + ".unavailable");
        Assert.Equal(HttpStatusCode.Conflict, (await Op(e, asset, new { kind = "read", requestId = Guid.NewGuid(), fileId })).StatusCode);
        Assert.True(await db.ProfileFiles.AnyAsync(f => f.Id == fileId));
        Assert.Equal(1, (await db.LocalProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId)).UsedFiles);
        await Data(await e.PostAsJsonAsync($"/api/machines/{asset}/logout", new { }));
        await db.LocalProfiles.Where(p => p.Id == profileId).ExecuteUpdateAsync(s => s.SetProperty(p => p.StorageBackend, "Agent"));
        var newSession = await Data(await e.PostAsJsonAsync($"/api/machines/{asset}/sessions", new { }));
        Assert.NotEqual(profileId, newSession.GetProperty("profileId").GetGuid());
        Assert.True(await db.ProfileFiles.AnyAsync(f => f.Id == fileId));
        Assert.Equal("Agent", (await db.LocalProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId)).StorageBackend);
        var serverProfile = await db.LocalProfiles.AsNoTracking().SingleAsync(p => p.Id == newSession.GetProperty("profileId").GetGuid());
        Assert.Equal("Server", serverProfile.StorageBackend);
        Assert.Equal(0, serverProfile.UsedFiles);
    }

    [Fact]
    public async Task Failed_blob_write_never_commits_file_quota_or_operation()
    {
        var (admin, employee, asset, _, user) = await Setup(); using var a = admin; using var e = employee;
        var snapshot = await Data(await e.GetAsync($"/api/machines/{asset}/profile"));
        var profileId = snapshot.GetProperty("profileId").GetGuid();
        var parent = Path.Combine(factory.StorageRoot, "simulation", asset.ToString("N"));
        Directory.CreateDirectory(parent);
        await File.WriteAllTextAsync(Path.Combine(parent, user.ToString("N")), "block directory creation");
        var requestId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.InternalServerError, (await Op(e, asset, new { kind = "create", requestId, folder = "Desktop", name = "no.txt" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ProfileFiles.AnyAsync(f => f.ProfileId == profileId));
        Assert.False(await db.ProfileOperations.AnyAsync(o => o.ProfileId == profileId && o.RequestId == requestId));
        Assert.Equal(0, (await db.LocalProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId)).UsedFiles);
    }
}
