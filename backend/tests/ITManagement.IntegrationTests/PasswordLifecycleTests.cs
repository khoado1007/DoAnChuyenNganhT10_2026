using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ITManagement.IntegrationTests;
public sealed class PasswordClock : TimeProvider
{
    public DateTimeOffset Now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class PasswordFactory : ApiFactory
{
    public PasswordClock Clock { get; } = new();
    protected override bool UseAgent => false;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Clock); });
    }
}
public sealed class PasswordLifecycleTests(PasswordFactory factory) : IClassFixture<PasswordFactory>
{
    private static async Task<JsonElement> Data(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{r.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }
    private async Task<HttpClient> Login(string name, string password)
    {
        var c = factory.CreateClient();
        var a = await Data(await c.PostAsJsonAsync("/api/auth/login", new { username = name, password }));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.GetProperty("accessToken").GetString());
        return c;
    }
    [Fact]
    public async Task Default_reset_reminders_expiry_reactivation_and_change_follow_one_account_cycle()
    {
        using var admin = await Login("admin", "Admin@12345");
        var name = "cycle" + Guid.NewGuid().ToString("N");
        var u = await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = "Cycle", department = "IT", role = "Employee" }));
        var id = u.GetProperty("id").GetGuid();
        factory.Clock.Now += TimeSpan.FromDays(10); // No login, no deadline yet.
        using var employee = await Login(name, "12345Az@");
        var started = factory.Clock.Now;
        async Task<JsonElement> Policy() => await Data(await employee.GetAsync("/api/auth/password-policy"));
        Assert.Equal(1, (await Policy()).GetProperty("reminderDay").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync($"/api/users/{id}/reset-password", new {})).StatusCode);
        await Data(await employee.PostAsJsonAsync("/api/auth/password-reminder", new { day = 1 }));
        Assert.Equal(JsonValueKind.Null, (await Policy()).GetProperty("reminderDay").ValueKind);
        factory.Clock.Now = started.AddDays(4).AddTicks(-1);
        Assert.Equal(JsonValueKind.Null, (await Policy()).GetProperty("reminderDay").ValueKind);
        foreach (var day in new[] { 5, 6, 7 })
        {
            factory.Clock.Now = started.AddDays(day - 1);
            Assert.Equal(day, (await Policy()).GetProperty("reminderDay").GetInt32());
            await Data(await employee.PostAsJsonAsync("/api/auth/password-reminder", new { day }));
        }
        factory.Clock.Now = started.AddDays(7);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/auth/me")).StatusCode);
        using var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/login", new { username = name, password = "12345Az@" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Locked", (await db.AppUsers.SingleAsync(x => x.Id == id)).Status.ToString());
        }
        await Data(await admin.PutAsJsonAsync($"/api/users/{id}", new { email = name + "@test.local", fullName = "Cycle", department = "IT", role = "Employee", status = "Active" }));
        using var fresh = await Login(name, "12345Az@");
        Assert.Equal(1, (await Data(await fresh.GetAsync("/api/auth/password-policy"))).GetProperty("reminderDay").GetInt32());
        await Data(await fresh.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "12345Az@", newPassword = "Changed@12345", confirmPassword = "Changed@12345" }));
        using var changed = await Login(name, "Changed@12345");
        Assert.False((await Data(await changed.GetAsync("/api/auth/password-policy"))).GetProperty("required").GetBoolean());
        await Data(await admin.PostAsJsonAsync($"/api/users/{id}/reset-password", new {}));
        Assert.Equal(HttpStatusCode.Unauthorized, (await changed.GetAsync("/api/auth/me")).StatusCode);
        factory.Clock.Now += TimeSpan.FromDays(8);
        using var reset = await Login(name, "12345Az@");
        Assert.Equal(1, (await Data(await reset.GetAsync("/api/auth/password-policy"))).GetProperty("reminderDay").GetInt32());
    }
}
