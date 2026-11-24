using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Enums;
using ITManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class ManagedSoftwareTests(ServerApiFactory factory) : IClassFixture<ServerApiFactory>
{
    [Fact]
    public async Task Legacy_inventory_get_is_read_only_and_ids_survive_edits_and_reordering()
    {
        using var admin = await Login(); var asset = await Asset(admin);
        const string legacy = "[{\"Name\":\"Legacy app\",\"Type\":\"Utility\",\"FilePath\":\"C:\\\\Apps\\\\old.exe\"},{\"Name\":\"Second\"}]";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MachineSpecifications.Add(new MachineSpecification { AssetId = asset, SoftwareJson = legacy, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var first = await Spec(admin, asset); var again = await Spec(admin, asset);
        var id = first.GetProperty("software")[0].GetProperty("id").GetGuid();
        var otherId = first.GetProperty("software")[1].GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id); Assert.NotEqual(id, otherId);
        Assert.Equal(id, again.GetProperty("software")[0].GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("software")[0].GetProperty("kind").ValueKind);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(legacy, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().MachineSpecifications.SingleAsync(x => x.AssetId == asset)).SoftwareJson);
        var changed = await Data(await admin.PutAsJsonAsync(Route(asset), new { revision = Revision(first), software = new object[] {
            first.GetProperty("software")[1], new { id, name = "Legacy renamed", type = "Utility", kind = "App" } } }));
        Assert.Equal(otherId, changed.GetProperty("software")[0].GetProperty("id").GetGuid());
        Assert.Equal(id, changed.GetProperty("software")[1].GetProperty("id").GetGuid());
        Assert.Equal("Utility", changed.GetProperty("software")[1].GetProperty("type").GetString());
        Assert.Equal("App", changed.GetProperty("software")[1].GetProperty("kind").GetString());
        Assert.NotEqual(Revision(first), Revision(changed));
    }

    [Fact]
    public async Task Inventory_rejects_duplicate_unknown_empty_ids_invalid_kind_and_missing_revision()
    {
        using var admin = await Login(); var asset = await Asset(admin); var spec = await Populate(admin, asset);
        var item = spec.GetProperty("software")[0];
        foreach (var software in new object[] { new object[] { item, item }, new[] { new { id = Guid.NewGuid(), name = "Forged" } },
            new[] { new { id = Guid.Empty, name = "Empty" } }, new[] { new { name = "Invalid", kind = "Driver" } } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Route(asset), new { revision = Revision(spec), software })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(Route(asset), new { software = Array.Empty<object>() })).StatusCode);
        Assert.Equal(2, (await Spec(admin, asset)).GetProperty("software").GetArrayLength());
    }

    [Fact]
    public async Task Admin_approval_removes_only_selected_item_and_preserves_employee_files_quota_session_and_audit()
    {
        using var admin = await Login(); var asset = await Asset(admin); var spec = await Populate(admin, asset);
        var (employee, _, _) = await Employee(admin, asset); using var e = employee;
        var beforeProfile = await Data(await e.GetAsync($"/api/machines/{asset}/profile"));
        int beforeAudit;
        using (var scope = factory.Services.CreateScope()) beforeAudit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppAuditEvents.CountAsync();
        var id = spec.GetProperty("software")[0].GetProperty("id").GetGuid();
        var removed = await Data(await Remove(e, asset, id, Revision(spec)));
        Assert.Single(removed.GetProperty("software").EnumerateArray());
        Assert.Equal("Background helper", removed.GetProperty("software")[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("software")[0].GetProperty("filePath").ValueKind);
        Assert.False(removed.TryGetProperty("accessToken", out _));
        Assert.Equal(HttpStatusCode.OK, (await e.GetAsync("/api/auth/me")).StatusCode);
        var afterProfile = await Data(await e.GetAsync($"/api/machines/{asset}/profile"));
        Assert.Equal(beforeProfile.GetProperty("quota").GetRawText(), afterProfile.GetProperty("quota").GetRawText());
        Assert.Equal(beforeProfile.GetProperty("files").GetRawText(), afterProfile.GetProperty("files").GetRawText());
        using (var scope = factory.Services.CreateScope()) Assert.Equal(beforeAudit, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppAuditEvents.CountAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Remove(e, asset, id, Revision(spec))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Remove(e, asset, id, Revision(removed))).StatusCode);
    }

    [Fact]
    public async Task Approval_rejects_wrong_password_non_admin_disabled_and_identity_locked_admin_without_ending_employee_session()
    {
        using var admin = await Login(); var asset = await Asset(admin); var spec = await Populate(admin, asset);
        var (employee, username, _) = await Employee(admin, asset); using var e = employee;
        var id = spec.GetProperty("software")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(e, asset, id, Revision(spec), "admin", "incorrect")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(e, asset, id, Revision(spec), username, "Test@12345")).StatusCode);
        var adminName = "softwareadmin" + Guid.NewGuid().ToString("N");
        var created = await Data(await admin.PostAsJsonAsync("/api/users", new { username = adminName, email = adminName + "@test.local", fullName = "Approval admin", department = "IT", password = "Test@12345", role = "Employee" }));
        var userId = created.GetProperty("id").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.AppUsers.Where(x => x.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Role, UserRole.Admin).SetProperty(x => x.Status, UserStatus.Locked));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(e, asset, id, Revision(spec), adminName, "Test@12345")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.AppUsers.Where(x => x.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, UserStatus.Active));
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var identity = (await manager.FindByNameAsync(adminName))!;
            await manager.SetLockoutEnabledAsync(identity, true); await manager.SetLockoutEndDateAsync(identity, DateTimeOffset.UtcNow.AddMinutes(10));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(e, asset, id, Revision(spec), adminName, "Test@12345")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await e.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(2, (await Spec(admin, asset)).GetProperty("software").GetArrayLength());
    }

    [Fact]
    public async Task Approval_requires_current_employee_machine_and_live_session()
    {
        using var admin = await Login(); var asset = await Asset(admin); var spec = await Populate(admin, asset);
        var (employee, _, session) = await Employee(admin, asset); using var e = employee;
        var (other, _, _) = await Employee(admin, await Asset(admin)); using var o = other;
        o.DefaultRequestHeaders.Remove("X-Device-Session"); o.DefaultRequestHeaders.Add("X-Device-Session", session.ToString());
        var id = spec.GetProperty("software")[0].GetProperty("id").GetGuid();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Remove(anonymous, asset, id, Revision(spec))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(admin, asset, id, Revision(spec))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(o, asset, id, Revision(spec))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(e, await Asset(admin), id, Revision(spec))).StatusCode);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().DeviceSessions.Where(x => x.Id == session).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Remove(e, asset, id, Revision(spec))).StatusCode);
    }

    [Fact]
    public async Task Stale_specification_cannot_resurrect_removed_items_or_overwrite_new_items()
    {
        using var admin = await Login(); var asset = await Asset(admin); var spec = await Populate(admin, asset);
        var (employee, _, _) = await Employee(admin, asset); using var e = employee;
        var id = spec.GetProperty("software")[0].GetProperty("id").GetGuid();
        var removed = await Data(await Remove(e, asset, id, Revision(spec)));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(Route(asset), new { revision = Revision(spec), software = spec.GetProperty("software") })).StatusCode);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => admin.PutAsJsonAsync(Route(asset), new { revision = Revision(removed), software = new object[] { removed.GetProperty("software")[0], new { name = "New" + i, kind = "App" } } })));
        Assert.Single(responses, r => r.IsSuccessStatusCode); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var current = await Spec(admin, asset);
        Assert.Equal(2, current.GetProperty("software").GetArrayLength());
        Assert.DoesNotContain(current.GetProperty("software").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.Conflict, (await Remove(e, asset, current.GetProperty("software")[0].GetProperty("id").GetGuid(), Revision(removed))).StatusCode);
    }

    [Fact]
    public async Task Approval_attempts_are_limited_per_employee_without_locking_other_employees()
    {
        using var admin = await Login(); var asset = await Asset(admin); var spec = await Populate(admin, asset);
        var (employee, _, _) = await Employee(admin, asset); using var e = employee;
        var id = spec.GetProperty("software")[0].GetProperty("id").GetGuid();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Forbidden, (await Remove(e, asset, id, Revision(spec), "admin", "incorrect")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Remove(e, asset, id, Revision(spec))).StatusCode);
        var otherAsset = await Asset(admin); var otherSpec = await Populate(admin, otherAsset);
        var (other, _, _) = await Employee(admin, otherAsset); using var o = other;
        Assert.Equal(HttpStatusCode.OK, (await Remove(o, otherAsset, otherSpec.GetProperty("software")[0].GetProperty("id").GetGuid(), Revision(otherSpec))).StatusCode);
    }

    [Fact]
    public async Task Bulk_append_skips_duplicates_and_preserves_existing_metadata_and_configuration()
    {
        using var admin = await Login(); var first = await Asset(admin); var second = await Asset(admin);
        var before = await Populate(admin, first); var originalId = before.GetProperty("software")[0].GetProperty("id").GetGuid();
        var result = await Data(await admin.PostAsJsonAsync("/api/assets/software/bulk", new { assetIds = new[] { first, second }, software = new[] {
            new { name = " editor ", kind = "App", filePath = "must-not-replace" }, new { name = "EDITOR", kind = "App", filePath = "another" },
            new { name = "Editor", kind = "Service", filePath = "service" }, new { name = "New app", kind = "App", filePath = "new" } } }));
        Assert.Equal(5, result.GetProperty("added").GetInt32()); Assert.Equal(3, result.GetProperty("skipped").GetInt32());
        Assert.Equal(2, result.GetProperty("machines").GetArrayLength());
        var current = await Spec(admin, first);
        Assert.Equal(4, current.GetProperty("software").GetArrayLength());
        var preserved = current.GetProperty("software").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == originalId);
        Assert.Equal(@"C:\Apps\Editor.exe", preserved.GetProperty("filePath").GetString());
        Assert.Equal("Editor", preserved.GetProperty("name").GetString());
        Assert.Equal(3, (await Spec(admin, second)).GetProperty("software").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(Route(first), new { revision = Revision(before), software = before.GetProperty("software") })).StatusCode);
    }

    [Fact]
    public async Task Bulk_validates_roles_input_missing_asset_and_capacity_atomically()
    {
        using var admin = await Login(); var first = await Asset(admin); var second = await Asset(admin);
        var initial = await Data(await admin.PutAsJsonAsync(Route(first), new { software = Enumerable.Range(0, 200).Select(i => new { name = "Existing" + i, kind = "App" }).ToArray() }));
        var software = new[] { new { name = "One new", kind = "Service" } };
        var (employee, _, _) = await Employee(admin, second); using var e = employee; using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await e.PostAsJsonAsync("/api/assets/software/bulk", new { assetIds = new[] { second }, software })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/assets/software/bulk", new { assetIds = new[] { second }, software })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/assets/software/bulk", new { assetIds = new[] { second, Guid.NewGuid() }, software })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/assets/software/bulk", new { assetIds = new[] { second, first }, software })).StatusCode);
        Assert.Empty((await Spec(admin, second)).GetProperty("software").EnumerateArray());
        Assert.Equal(Revision(initial), Revision(await Spec(admin, first)));
        foreach (var invalid in new object[] { new { assetIds = Array.Empty<Guid>(), software }, new { assetIds = new[] { second, second }, software },
            new { assetIds = new[] { second }, software = new[] { new { name = "No kind" } } },
            new { assetIds = new[] { second }, software = new[] { new { id = Guid.NewGuid(), name = "Forged", kind = "App" } } },
            new { assetIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray(), software },
            new { assetIds = new[] { second }, software = Enumerable.Range(0, 201).Select(i => new { name = "New" + i, kind = "App" }).ToArray() } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/assets/software/bulk", invalid)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_bulk_adds_once_without_changing_files_quota_or_file_history()
    {
        using var admin = await Login(); var asset = await Asset(admin);
        var (employee, _, _) = await Employee(admin, asset); using var e = employee;
        var beforeProfile = await Data(await e.GetAsync($"/api/machines/{asset}/profile")); int beforeAudit;
        using (var scope = factory.Services.CreateScope()) beforeAudit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppAuditEvents.CountAsync();
        var request = new { assetIds = new[] { asset }, software = new[] { new { name = "Concurrent app", kind = "App" } } };
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => await Data(await admin.PostAsJsonAsync("/api/assets/software/bulk", request))));
        Assert.Equal(1, results.Sum(result => result.GetProperty("added").GetInt32()));
        Assert.Equal(1, results.Sum(result => result.GetProperty("skipped").GetInt32()));
        Assert.Single((await Spec(admin, asset)).GetProperty("software").EnumerateArray());
        var afterProfile = await Data(await e.GetAsync($"/api/machines/{asset}/profile"));
        Assert.Equal(beforeProfile.GetRawText(), afterProfile.GetRawText());
        using (var scope = factory.Services.CreateScope()) Assert.Equal(beforeAudit, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppAuditEvents.CountAsync());
    }

    private static string Route(Guid asset) => $"/api/assets/{asset}/specification";
    private static string Revision(JsonElement spec) => spec.GetProperty("revision").GetString()!;
    private static async Task<JsonElement> Spec(HttpClient admin, Guid asset) => await Data(await admin.GetAsync(Route(asset)));
    private static Task<HttpResponseMessage> Remove(HttpClient employee, Guid asset, Guid id, string revision, string username = "admin", string password = "Admin@12345")
        => employee.PostAsJsonAsync($"/api/machines/{asset}/software/{id}/remove", new { adminUsername = username, adminPassword = password, revision });
    private static async Task<JsonElement> Populate(HttpClient admin, Guid asset) => await Data(await admin.PutAsJsonAsync(Route(asset), new {
        software = new[] { new { name = "Editor", kind = "App", filePath = @"C:\Apps\Editor.exe" }, new { name = "Background helper", kind = "Service", filePath = @"C:\Services\helper.exe" } } }));
    private async Task<(HttpClient client, string username, Guid session)> Employee(HttpClient admin, Guid asset)
    {
        var name = "software" + Guid.NewGuid().ToString("N");
        await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = "Software employee", department = "IT", password = "Test@12345", role = "Employee" }));
        var client = await Login(name, "Test@12345");
        var session = (await Data(await client.PostAsJsonAsync($"/api/machines/{asset}/sessions", new { }))).GetProperty("sessionId").GetGuid();
        client.DefaultRequestHeaders.Add("X-Device-Session", session.ToString()); return (client, name, session);
    }
    private async Task<HttpClient> Login(string username = "admin", string password = "Admin@12345")
    {
        var client = factory.CreateClient(); var auth = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString()); return client;
    }
    private static async Task<Guid> Asset(HttpClient admin) => (await Data(await admin.PostAsJsonAsync("/api/assets", new { assetCode = Guid.NewGuid().ToString("N"), name = "Software PC" }))).GetProperty("id").GetGuid();
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(); Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }
}
