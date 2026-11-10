using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Application.Common.Interfaces;
using ITManagement.Domain.Entities.Storage;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ITManagement.IntegrationTests;

/// <summary>Khởi tạo bộ kiểm thử quản trị hạn mức với máy chủ API dùng chung.</summary>
public class StorageAdminTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Đăng nhập người dùng kiểm thử và trả về client đã xác thực.
    private async Task<HttpClient> Login(string name = "admin", string password = "Admin@12345")
    {
        var client = factory.CreateClient();
        var result = await client.PostAsJsonAsync("/api/auth/login", new { username = name, password });
        result.EnsureSuccessStatusCode();
        var json = await result.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", json.GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }
    // Tạo nhân viên kiểm thử và trả về mã tài khoản cùng client đã đăng nhập.
    private async Task<(Guid id, HttpClient client)> Employee(HttpClient admin)
    {
        var name = "quota" + Guid.NewGuid().ToString("N");
        var result = await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = name, department = "IT", role = "Employee", password = "Test@12345" });
        result.EnsureSuccessStatusCode();
        var id = (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        return (id, await Login(name, "Test@12345"));
    }
    // Tạo URL quản trị hạn mức theo mã người dùng.
    private static string Url(Guid id) => $"/api/storage/quotas/{id}";
    // Đọc dữ liệu phản hồi API sau khi kiểm tra trạng thái thành công.
    private static async Task<JsonElement> Data(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    // Tạo blob và metadata tệp để kiểm thử đối soát lưu trữ.
    private async Task<(Guid fileId, string key)> Seed(Guid owner, int bytes, bool deleted = false)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var q = await db.StorageQuotas.SingleAsync(q => q.UserId == owner);
        using var stream = new MemoryStream(new byte[bytes]);
        var key = await scope.ServiceProvider.GetRequiredService<IFileStorageService>().SaveAsync(owner, "a", stream);
        var file = new FileMetadata { StorageQuotaId = q.Id, StoragePath = key, DisplayName = "a", SizeBytes = bytes, IsDeleted = deleted };
        db.FileMetadata.Add(file); if (!deleted) q.UsedBytes += bytes;
        await db.SaveChangesAsync(); return (file.Id, key);
    }
    /// <summary>Kiểm thử quản trị viên quản lý hạn mức nhưng không thể hạ thấp hơn dung lượng đang dùng.</summary>
    [Fact]
    public async Task Admin_can_manage_quota_but_cannot_reduce_below_usage()
    {
        using var admin = await Login(); var a = await Employee(admin); using var employee = a.client;
        await Seed(a.id, 4);
        var updated = await Data(await admin.PutAsJsonAsync(Url(a.id), new { quotaBytes = 10 }));
        Assert.Equal(10, updated.GetProperty("quotaBytes").GetInt64());
        Assert.Equal(4, updated.GetProperty("usedBytes").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Url(a.id), new { quotaBytes = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Url(a.id), new { quotaBytes = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Url(a.id), new { quotaBytes = long.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync(Url(Guid.NewGuid()), new { quotaBytes = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PutAsJsonAsync(Url(a.id), new { quotaBytes = 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/storage/quotas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true })).StatusCode);
        using var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/storage/quotas")).StatusCode);
        var list = await Data(await admin.GetAsync("/api/storage/quotas?page=1&pageSize=100"));
        Assert.True(list.GetProperty("totalCount").GetInt32() >= 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/storage/quotas?page=0")).StatusCode);
    }
    /// <summary>Kiểm thử đối soát xem trước rồi áp dụng sẽ sửa dung lượng và xử lý lại tombstone, đối tượng mồ côi.</summary>
    [Fact]
    public async Task Reconcile_dry_run_then_apply_repairs_usage_and_retries_tombstones_and_orphans()
    {
        using var admin = await Login(); var a = await Employee(admin); using var employee = a.client;
        var live = await Seed(a.id, 4); var tomb = await Seed(a.id, 2, deleted: true);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        using var bytes = new MemoryStream(new byte[3]); var orphan = await storage.SaveAsync(a.id, "orphan", bytes);
        await db.StorageQuotas.Where(q => q.UserId == a.id).ExecuteUpdateAsync(s => s.SetProperty(q => q.UsedBytes, 99));
        var dry = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { }));
        Assert.Equal(4, dry.GetProperty("calculatedUsedBytes").GetInt64());
        Assert.Equal(1, dry.GetProperty("orphanFiles").GetInt32());
        Assert.Equal(1, dry.GetProperty("tombstoneFiles").GetInt32());
        Assert.False(dry.GetProperty("applied").GetBoolean());
        Assert.True(await storage.ExistsAsync(orphan));
        Assert.Equal(99, (await db.StorageQuotas.AsNoTracking().SingleAsync(q => q.UserId == a.id)).UsedBytes);
        var applied = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.Equal(2, applied.GetProperty("deletedFiles").GetInt32());
        Assert.True(await storage.ExistsAsync(live.key)); Assert.False(await storage.ExistsAsync(tomb.key)); Assert.False(await storage.ExistsAsync(orphan));
        Assert.Equal(4, (await db.StorageQuotas.AsNoTracking().SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.True((await db.FileMetadata.SingleAsync(f => f.Id == tomb.fileId)).IsDeleted);
        var retry = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.Equal(0, retry.GetProperty("deletedFiles").GetInt32());
        Assert.DoesNotContain(live.key, applied.GetRawText());
    }
    /// <summary>Kiểm thử blob hiệu lực bị thiếu vẫn giữ metadata và số liệu, metadata sai chủ sở hữu chặn dọn dẹp.</summary>
    [Fact]
    public async Task Missing_live_blob_keeps_metadata_and_accounting_and_corrupt_owner_blocks_cleanup()
    {
        using var admin = await Login(); var a = await Employee(admin); var b = await Employee(admin);
        using var ac = a.client; using var bc = b.client;
        var missing = await Seed(a.id, 5); var other = await Seed(b.id, 3);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        await storage.DeleteAsync(missing.key);
        var report = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.Equal(1, report.GetProperty("missingLiveFiles").GetInt32());
        Assert.False((await db.FileMetadata.SingleAsync(f => f.Id == missing.fileId)).IsDeleted);
        Assert.Equal(5, report.GetProperty("calculatedUsedBytes").GetInt64());
        await db.FileMetadata.Where(f => f.Id == missing.fileId).ExecuteUpdateAsync(s => s.SetProperty(f => f.StoragePath, other.key));
        report = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.True(report.GetProperty("cleanupBlocked").GetBoolean());
        Assert.True(await storage.ExistsAsync(other.key));
    }
    // Khởi tạo bộ lưu trữ giả lập để điều khiển đồng bộ, lỗi xóa và hủy thao tác.
    private sealed class ControlledStorage(IFileStorageService inner, bool pauseUpload = false, bool failDelete = false, CancellationTokenSource? cancelAfterDelete = null) : IFileStorageService
    {
        public TaskCompletionSource Saved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int fail = failDelete ? 1 : 0;
        /// <summary>Lưu luồng giả lập trong giới hạn kích thước được yêu cầu.</summary>
        public async Task<StoredFile> SaveLimitedAsync(Guid owner, string name, Stream content, long limit, CancellationToken ct = default)
        {
            var saved = await inner.SaveLimitedAsync(owner, name, content, limit, ct);
            Saved.TrySetResult();
            if (pauseUpload) await Release.Task.WaitAsync(ct);
            return saved;
        }
        /// <summary>Trả về nội dung kiểm kê lưu trữ giả lập.</summary>
        public Task<StorageInventory> InspectAsync(Guid owner, CancellationToken ct = default) => inner.InspectAsync(owner, ct);
        /// <summary>Lưu dữ liệu theo hợp đồng của dịch vụ.</summary>
        public Task<string> SaveAsync(Guid owner, string name, Stream content, CancellationToken ct = default) => inner.SaveAsync(owner, name, content, ct);
        /// <summary>Đọc dữ liệu từ luồng.</summary>
        public Task<Stream> ReadAsync(string key, CancellationToken ct = default) => inner.ReadAsync(key, ct);
        /// <summary>Kiểm tra đối tượng có tồn tại hay không.</summary>
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => inner.ExistsAsync(key, ct);
        /// <summary>Xóa đối tượng theo quy tắc của dịch vụ.</summary>
        public async Task DeleteAsync(string key, CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref fail, 0) == 1) throw new IOException("C:/private-disk/test");
            await inner.DeleteAsync(key, ct); cancelAfterDelete?.Cancel();
        }
    }
    /// <summary>Kiểm thử đối soát và thay đổi hạn mức chờ khóa đang bảo vệ tải lên.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconcile_and_quota_change_wait_for_upload_lock(bool changeQuota)
    {
        using var admin = await Login(); var a = await Employee(admin); using var owner = a.client;
        var wrapper = new ControlledStorage(factory.Services.GetRequiredService<IFileStorageService>(), pauseUpload: true);
        using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IFileStorageService>(wrapper)));
        using var uploadClient = app.CreateClient(); uploadClient.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(new byte[6]), "file", "a.txt");
        var upload = uploadClient.PostAsync("/api/files", form);
        await wrapper.Saved.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var operation = changeQuota ? admin.PutAsJsonAsync(Url(a.id), new { quotaBytes = 5 })
            : admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true });
        try { await Assert.ThrowsAsync<TimeoutException>(() => operation.WaitAsync(TimeSpan.FromMilliseconds(250))); }
        finally { wrapper.Release.TrySetResult(); }
        Assert.Equal(HttpStatusCode.Created, (await upload).StatusCode);
        var result = await operation;
        if (changeQuota) Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        else Assert.Equal(0, (await Data(result)).GetProperty("deletedFiles").GetInt32());
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(6, (await db.StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
        var file = await db.FileMetadata.SingleAsync(f => f.StorageQuota.UserId == a.id);
        Assert.True(await wrapper.ExistsAsync(file.StoragePath));
    }
    /// <summary>Kiểm thử lỗi dọn dẹp được báo và có thể thử lại mà không làm lộ đường dẫn.</summary>
    [Fact]
    public async Task Cleanup_failure_is_reported_and_retryable_without_exposing_paths()
    {
        using var admin = await Login(); var a = await Employee(admin); using var owner = a.client;
        var tomb = await Seed(a.id, 3, deleted: true);
        var wrapper = new ControlledStorage(factory.Services.GetRequiredService<IFileStorageService>(), failDelete: true);
        using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IFileStorageService>(wrapper)));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
        var report = await Data(await client.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.Equal(1, report.GetProperty("cleanupFailures").GetInt32());
        Assert.DoesNotContain("private-disk", report.GetRawText());
        Assert.True(await wrapper.ExistsAsync(tomb.key));
        report = await Data(await client.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.Equal(1, report.GetProperty("deletedFiles").GetInt32());
        Assert.False(await wrapper.ExistsAsync(tomb.key));
    }
    /// <summary>Kiểm thử mục kiểm kê không an toàn và tham chiếu từ tài khoản khác được giữ nguyên.</summary>
    [Fact]
    public async Task Unsafe_inventory_entries_and_foreign_references_are_preserved()
    {
        using var admin = await Login(); var a = await Employee(admin); var b = await Employee(admin);
        using var ac = a.client; using var bc = b.client;
        var own = await Seed(a.id, 4, deleted: true); var foreign = await Seed(b.id, 4);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.FileMetadata.Where(f => f.Id == foreign.fileId).ExecuteUpdateAsync(s => s.SetProperty(f => f.StoragePath, own.key.ToUpper()));
        var ownerDir = Path.Combine(factory.StorageRoot, a.id.ToString());
        var ignored = Path.Combine(ownerDir, "notes.txt"); await File.WriteAllTextAsync(ignored, "keep");
        var nested = Path.Combine(ownerDir, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(nested);
        var report = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.Equal(2, report.GetProperty("skippedEntries").GetInt32());
        Assert.Equal(0, report.GetProperty("deletedFiles").GetInt32());
        Assert.True(File.Exists(ignored)); Assert.True(Directory.Exists(nested));
        Assert.True(await scope.ServiceProvider.GetRequiredService<IFileStorageService>().ExistsAsync(own.key));
    }
    private sealed class AdminIdentity : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Username => "admin-test";
        public ITManagement.Domain.Enums.UserRole? Role => ITManagement.Domain.Enums.UserRole.Admin;
        public bool IsAuthenticated => true;
    }
    /// <summary>Kiểm thử có thể thử lại thao tác dọn dẹp bị hủy giữa chừng.</summary>
    [Fact]
    public async Task Cancellation_after_partial_cleanup_can_be_retried()
    {
        using var admin = await Login(); var a = await Employee(admin); using var owner = a.client;
        var live = await Seed(a.id, 4); await Seed(a.id, 2, deleted: true); await Seed(a.id, 3, deleted: true);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.StorageQuotas.Where(q => q.UserId == a.id).ExecuteUpdateAsync(s => s.SetProperty(q => q.UsedBytes, 99));
        using var cts = new CancellationTokenSource();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var wrapped = new ControlledStorage(storage, cancelAfterDelete: cts);
        var service = new ITManagement.Infrastructure.Services.StorageAdminService(db, wrapped, new AdminIdentity());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReconcileAsync(a.id, true, cts.Token));
        Assert.True(await storage.ExistsAsync(live.key));
        Assert.Equal(2, (await storage.InspectAsync(a.id)).Keys.Count);
        Assert.Equal(99, (await db.StorageQuotas.AsNoTracking().SingleAsync(q => q.UserId == a.id)).UsedBytes);
        var retry = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
        Assert.Equal(1, retry.GetProperty("deletedFiles").GetInt32());
        Assert.Equal(4, (await db.StorageQuotas.AsNoTracking().SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.True(await storage.ExistsAsync(live.key));
    }
    /// <summary>Kiểm thử hạn mức tối thiểu dựa trên cả số liệu ghi nhận và metadata tệp.</summary>
    [Fact]
    public async Task Quota_floor_uses_both_recorded_and_metadata_usage()
    {
        using var admin = await Login(); var a = await Employee(admin); using var owner = a.client;
        await Seed(a.id, 5);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.StorageQuotas.Where(q => q.UserId == a.id).ExecuteUpdateAsync(s => s.SetProperty(q => q.UsedBytes, 0));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Url(a.id), new { quotaBytes = 4 })).StatusCode);
        await db.StorageQuotas.Where(q => q.UserId == a.id).ExecuteUpdateAsync(s => s.SetProperty(q => q.UsedBytes, 10));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Url(a.id), new { quotaBytes = 5 })).StatusCode);
    }
    // Tạo liên kết thư mục phục vụ kiểm tra an toàn đường dẫn.
    private static async Task LinkDirectory(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(link, target); return; }
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.WaitForExitAsync(); Assert.Equal(0, process.ExitCode);
    }
    /// <summary>Kiểm thử không đi theo mục reparse và từ chối liên kết thư mục của chủ sở hữu.</summary>
    [Fact]
    public async Task Reparse_entries_are_not_followed_and_owner_directory_link_is_rejected()
    {
        using var admin = await Login(); var a = await Employee(admin); var b = await Employee(admin); var c = await Employee(admin);
        using var ac = a.client; using var bc = b.client; using var cc = c.client;
        var target = await Seed(b.id, 7);
        var ownerDir = Path.Combine(factory.StorageRoot, a.id.ToString()); Directory.CreateDirectory(ownerDir);
        var entryLink = Path.Combine(ownerDir, Guid.NewGuid().ToString("N"));
        var ownerLink = Path.Combine(factory.StorageRoot, c.id.ToString());
        try
        {
            await LinkDirectory(entryLink, Path.Combine(factory.StorageRoot, b.id.ToString()));
            var report = await Data(await admin.PostAsJsonAsync(Url(a.id)+"/reconcile", new { apply = true }));
            Assert.Equal(1, report.GetProperty("skippedEntries").GetInt32());
            Assert.Equal(0, report.GetProperty("deletedFiles").GetInt32());
            await LinkDirectory(ownerLink, Path.Combine(factory.StorageRoot, b.id.ToString()));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url(c.id)+"/reconcile", new { apply = true })).StatusCode);
            Assert.True(await factory.Services.GetRequiredService<IFileStorageService>().ExistsAsync(target.key));
        }
        finally
        {
            // Remove the links themselves before the factory recursively cleans its temp root.
            if (Directory.Exists(entryLink)) Directory.Delete(entryLink, recursive: false);
            if (Directory.Exists(ownerLink)) Directory.Delete(ownerLink, recursive: false);
        }
    }
}
