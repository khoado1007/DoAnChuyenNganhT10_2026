using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ITManagement.Application.Common.Interfaces;
using ITManagement.Domain.Entities.Storage;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ITManagement.IntegrationTests;

/// <summary>Khởi tạo bộ kiểm thử truy cập và lưu trữ tệp với máy chủ API dùng chung.</summary>
public class FileTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Đọc dữ liệu phản hồi API sau khi kiểm tra trạng thái thành công.
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }


    // Tạo và đăng nhập nhân viên kiểm thử, đồng thời trả về mã tài khoản.
    private async Task<(HttpClient client, Guid id)> Employee()
    {
        using var admin = factory.CreateClient();
        var login = await Data(await admin.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "Admin@12345" }));
        admin.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        var name = "file" + Guid.NewGuid().ToString("N");
        var user = await Data(await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = name, department = "IT", role = "Employee", password = "Test@12345" }));
        var client = factory.CreateClient();
        var session = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username = name, password = "Test@12345" }));
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("accessToken").GetString());
        return (client, user.GetProperty("id").GetGuid());
    }
    // Khởi tạo dữ liệu kiểm thử cần thiết.
    private async Task<FileMetadata> Seed(Guid owner, string name = "report.txt")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quota = await db.StorageQuotas.SingleAsync(q => q.UserId == owner);
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("private data"));
        var key = await storage.SaveAsync(owner, name, bytes);
        var file = new FileMetadata { StorageQuotaId = quota.Id, DisplayName = name, StoragePath = key, ContentType = "text/html", SizeBytes = bytes.Length };
        db.FileMetadata.Add(file); quota.UsedBytes += file.SizeBytes; await db.SaveChangesAsync(); return file;
    }
    /// <summary>Kiểm thử chủ sở hữu có thể liệt kê và tải tệp, còn người khác và agent không thể truy cập.</summary>
    [Fact]
    public async Task Owner_can_list_and_download_but_other_user_and_agent_cannot_access_private_file()
    {
        var a = await Employee(); var b = await Employee(); using var owner = a.client; using var other = b.client;
        var file = await Seed(a.id, "../../secret.html");
        var page = await Data(await owner.GetAsync("/api/files"));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.False(page.GetProperty("items")[0].TryGetProperty("storagePath", out _));
        Assert.False(page.GetProperty("items")[0].TryGetProperty("storageQuotaId", out _));
        Assert.Equal(0, (await Data(await other.GetAsync($"/api/files?userId={a.id}"))).GetProperty("totalCount").GetInt32());
        var download = await owner.GetAsync($"/api/files/{file.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("private data", await download.Content.ReadAsStringAsync());
        Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/files/{file.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/files/{file.Id}")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/files")).StatusCode);
        anonymous.DefaultRequestHeaders.Authorization = new("Agent", new string('a', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/files/{file.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/files?page=0")).StatusCode);
    }
    /// <summary>Kiểm thử xóa đồng thời chỉ giải phóng hạn mức một lần và ẩn metadata đã xóa.</summary>
    [Fact]
    public async Task Concurrent_delete_releases_quota_once_and_hides_tombstone()
    {
        var a = await Employee(); using var owner = a.client; var file = await Seed(a.id);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => owner.DeleteAsync($"/api/files/{file.Id}")));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/files/{file.Id}/download")).StatusCode);
        Assert.Equal(0, (await Data(await owner.GetAsync("/api/files"))).GetProperty("totalCount").GetInt32());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, (await db.StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.True((await db.FileMetadata.SingleAsync(f => f.Id == file.Id)).IsDeleted);
        Assert.False(await scope.ServiceProvider.GetRequiredService<IFileStorageService>().ExistsAsync(file.StoragePath));
    }
    /// <summary>Kiểm thử blob thiếu hoặc khóa sai chủ sở hữu không làm lộ dữ liệu hay xóa tệp của người khác.</summary>
    [Fact]
    public async Task Missing_blob_and_corrupt_cross_owner_key_do_not_leak_or_delete_other_files()
    {
        var a = await Employee(); var b = await Employee(); using var owner = a.client; using var other = b.client;
        var file = await Seed(a.id); var otherFile = await Seed(b.id);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        await storage.DeleteAsync(file.StoragePath);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/files/{file.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/files/{file.Id}")).StatusCode);
        var bad = await Seed(a.id);
        await db.FileMetadata.Where(f => f.Id == bad.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.StoragePath, otherFile.StoragePath));
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"/api/files/{bad.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync($"/api/files/{bad.Id}")).StatusCode);
        Assert.True(await storage.ExistsAsync(otherFile.StoragePath));
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/files/{otherFile.Id}/download")).StatusCode);
    }
    /// <summary>Kiểm thử vùng lưu trữ từ chối khóa không opaque hoặc có thể duyệt đường dẫn.</summary>
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/tmp/outside")]
    [InlineData("C:\\outside.txt")]
    [InlineData("a/../../outside.txt")]
    public async Task Storage_rejects_nonopaque_or_traversal_keys(string key)
    {
        using var scope = factory.Services.CreateScope(); var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.ExistsAsync(key));
    }
    // Khởi tạo bộ lưu trữ giả lập lỗi xóa một lần để kiểm thử thao tác thử lại.
    private sealed class FailedDeleteOnce(IFileStorageService inner, bool failValidation = false) : IFileStorageService
    {
        private int remaining = 1;
        /// <summary>Lưu dữ liệu theo hợp đồng của dịch vụ.</summary>
        public Task<string> SaveAsync(Guid userId, string name, Stream content, CancellationToken ct = default) => inner.SaveAsync(userId, name, content, ct);
        /// <summary>Đọc dữ liệu từ luồng.</summary>
        public Task<Stream> ReadAsync(string key, CancellationToken ct = default) => inner.ReadAsync(key, ct);
        /// <summary>Kiểm tra đối tượng có tồn tại hay không.</summary>
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => failValidation ? throw new IOException("Private disk path C:/private-storage/secret") : inner.ExistsAsync(key, ct);
        /// <summary>Xóa đối tượng theo quy tắc của dịch vụ.</summary>
        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref remaining, 0) == 1) throw new IOException("Test-only simulated disk failure");
            return inner.DeleteAsync(key, ct);
        }
    }
    /// <summary>Kiểm thử có thể thử lại xóa vật lý thất bại mà không giải phóng hạn mức hai lần.</summary>
    [Fact]
    public async Task Failed_physical_delete_can_be_retried_without_releasing_quota_twice()
    {
        var a = await Employee(); using var original = a.client; var file = await Seed(a.id);
        var storage = factory.Services.GetRequiredService<IFileStorageService>();
        using var failingApp = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IFileStorageService>(new FailedDeleteOnce(storage))));
        using var client = failingApp.CreateClient(); client.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        var failure = await client.DeleteAsync($"/api/files/{file.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, failure.StatusCode);
        Assert.DoesNotContain("simulated disk failure", await failure.Content.ReadAsStringAsync());
        Assert.True(await storage.ExistsAsync(file.StoragePath));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/files/{file.Id}/download")).StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/files/{file.Id}")).StatusCode);
        Assert.False(await storage.ExistsAsync(file.StoragePath));
        using var finalScope = factory.Services.CreateScope();
        Assert.Equal(0, (await finalScope.ServiceProvider.GetRequiredService<AppDbContext>().StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
    }
    /// <summary>Kiểm thử lưu trữ bỏ qua tên hiển thị và dọn tệp ghi dở khi bị hủy.</summary>
    [Fact]
    public async Task Storage_ignores_display_filename_and_removes_cancelled_partial_save()
    {
        using var scope = factory.Services.CreateScope(); var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        var owner = Guid.NewGuid();
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("payload"));
        var key = await storage.SaveAsync(owner, "../../bad.exe:stream", content);
        Assert.Matches(@"^[0-9a-f-]{36}/[0-9a-f]{32}$", key);
        await using (var read = await storage.ReadAsync(key)) Assert.Equal(7, read.Length);
        await storage.DeleteAsync(key);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); content.Position = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync(owner, "file.txt", content, cancelled.Token));
        Assert.Empty(Directory.GetFiles(Path.Combine(factory.StorageRoot, owner.ToString())));
    }
    /// <summary>Kiểm thử lỗi kiểm tra lưu trữ không làm lộ đường dẫn hay thay đổi số liệu dung lượng.</summary>
    [Fact]
    public async Task Storage_validation_failure_does_not_disclose_paths_or_change_accounting()
    {
        var a = await Employee(); using var original = a.client; var file = await Seed(a.id);
        var storage = factory.Services.GetRequiredService<IFileStorageService>();
        using var failingApp = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IFileStorageService>(new FailedDeleteOnce(storage, failValidation: true))));
        using var client = failingApp.CreateClient(); client.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        var response = await client.DeleteAsync($"/api/files/{file.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("private-storage", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.FileMetadata.SingleAsync(f => f.Id == file.Id)).IsDeleted);
        Assert.Equal(file.SizeBytes, (await db.StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
    }
}


