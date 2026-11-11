using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ITManagement.IntegrationTests;

/// <summary>Khởi tạo bộ kiểm thử tải tệp lên với máy chủ API dùng chung.</summary>
public class FileUploadTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Tạo và đăng nhập người dùng kiểm thử theo vai trò được chỉ định.
    private async Task<(HttpClient client, Guid id)> User(string role = "Employee")
    {
        using var admin = factory.CreateClient();
        var auth = await (await admin.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "Admin@12345" })).Content.ReadFromJsonAsync<JsonElement>();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("data").GetProperty("accessToken").GetString());
        var name = "up" + Guid.NewGuid().ToString("N");
        var response = await admin.PostAsJsonAsync("/api/users", new { username = name, email = name + "@test.local", fullName = name, department = "IT", role, password = "Test@12345" });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        var client = factory.CreateClient();
        auth = await (await client.PostAsJsonAsync("/api/auth/login", new { username = name, password = "Test@12345" })).Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("data").GetProperty("accessToken").GetString());
        return (client, id);
    }
    // Tạo yêu cầu tải tệp lên phục vụ kiểm thử.
    private static async Task<HttpResponseMessage> Upload(HttpClient client, byte[] bytes, string name = "report.txt")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return await client.PostAsync("/api/files", form);
    }
    /// <summary>Kiểm thử tải lên lưu đúng số byte thực tế và giữ tệp ở chế độ riêng tư.</summary>
    [Fact]
    public async Task Upload_persists_actual_bytes_and_is_private()
    {
        var a = await User(); var b = await User(); using var owner = a.client; using var other = b.client;
        var bytes = new byte[] { 1, 2, 3, 255 };
        var response = await Upload(owner, bytes, "../../report.txt");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("report.txt", data.GetProperty("displayName").GetString());
        Assert.Equal(4, data.GetProperty("sizeBytes").GetInt64());
        Assert.False(data.TryGetProperty("storagePath", out _));
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal(bytes, await owner.GetByteArrayAsync($"/api/files/{id}/download"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/files/{id}/download")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, (await db.StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.Equal(4, (await db.FileMetadata.SingleAsync(f => f.Id == id)).SizeBytes);
    }
    /// <summary>Kiểm thử tải lên đồng thời không vượt hạn mức và xóa tệp giải phóng dung lượng.</summary>
    [Fact]
    public async Task Concurrent_uploads_cannot_exceed_quota_and_delete_releases_usage()
    {
        var a = await User(); using var owner = a.client;
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().StorageQuotas.Where(q => q.UserId == a.id).ExecuteUpdateAsync(s => s.SetProperty(q => q.QuotaBytes, 10));
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Upload(owner, new byte[6])));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.RequestEntityTooLarge));
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(6, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.Single(Directory.GetFiles(Path.Combine(factory.StorageRoot, a.id.ToString())));
        var success = responses.Single(r => r.StatusCode == HttpStatusCode.Created);
        var id = (await success.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/files/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Upload(owner, new byte[10])).StatusCode);
    }
    /// <summary>Kiểm thử từ chối tải lên bởi người không phải nhân viên và dữ liệu không hợp lệ.</summary>
    [Fact]
    public async Task Upload_rejects_non_employee_and_invalid_inputs()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(anonymous, [1])).StatusCode);
        var t = await User("Technician"); using var tech = t.client;
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(tech, [1])).StatusCode);
        var a = await User(); using var owner = a.client;
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, [1], new string('a', 256))).StatusCode);
        using var empty = new MultipartFormDataContent();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/files", empty)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload(owner, new byte[10 * 1024 * 1024 + 1])).StatusCode);
    }
    /// <summary>Kiểm thử lỗi sau khi lưu blob sẽ hoàn tác dung lượng và dọn blob.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_after_blob_save_rolls_back_usage_and_cleans_blob(bool cancel)
    {
        var a = await User(); using var owner = a.client;
        using var scope = factory.Services.CreateScope();
        var existing = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(existing.Database.GetConnectionString());
        if (!cancel) options.AddInterceptors(new FailSave());
        await using var db = new AppDbContext(options.Options);
        using var cts = new CancellationTokenSource();
        var storage = scope.ServiceProvider.GetRequiredService<ITManagement.Application.Common.Interfaces.IFileStorageService>();
        var wrapped = new AfterSave(storage, cancel ? cts : null);
        var service = new ITManagement.Infrastructure.Services.FileService(db, wrapped, new Current(a.id));
        using var content = new MemoryStream(new byte[4]);
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UploadAsync("a.txt", content, cts.Token));
        else
            await Assert.ThrowsAsync<DbUpdateException>(() => service.UploadAsync("a.txt", content, cts.Token));
        Assert.NotNull(wrapped.Key);
        Assert.False(await storage.ExistsAsync(wrapped.Key!));
        Assert.Equal(0, (await existing.StorageQuotas.AsNoTracking().SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.False(await existing.FileMetadata.AnyAsync(f => f.StorageQuota.UserId == a.id));
    }
    /// <summary>Kiểm thử lỗi lưu trữ khi tải lên không làm lộ đường dẫn riêng tư.</summary>
    [Fact]
    public async Task Upload_storage_failure_does_not_expose_private_paths()
    {
        var a = await User(); using var owner = a.client;
        var storage = factory.Services.GetRequiredService<ITManagement.Application.Common.Interfaces.IFileStorageService>();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<ITManagement.Application.Common.Interfaces.IFileStorageService>(new AfterSave(storage, null, fail: true))));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        var response = await Upload(client, [1, 2]);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("private-disk", await response.Content.ReadAsStringAsync());
    }
    // Khởi tạo dịch vụ người dùng hiện tại giả lập cho các ca kiểm thử.
    private sealed class Current(Guid id) : ITManagement.Application.Common.Interfaces.ICurrentUserService
    {
        public Guid? UserId => id;
        public string? Username => "test";
        public ITManagement.Domain.Enums.UserRole? Role => ITManagement.Domain.Enums.UserRole.Employee;
        public bool IsAuthenticated => true;
    }
    private sealed class FailSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        /// <summary>Can thiệp vào giai đoạn lưu thay đổi để mô phỏng lỗi kiểm thử.</summary>
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
            => throw new DbUpdateException("Injected metadata failure");
    }
    // Khởi tạo bộ lưu trữ giả lập có thể hủy hoặc báo lỗi sau khi lưu.
    private sealed class AfterSave(ITManagement.Application.Common.Interfaces.IFileStorageService inner, CancellationTokenSource? cancel, bool fail = false)
        : ITManagement.Application.Common.Interfaces.IFileStorageService
    {
        public string? Key { get; private set; }
        /// <summary>Lưu luồng giả lập trong giới hạn kích thước được yêu cầu.</summary>
        public async Task<ITManagement.Application.Common.Interfaces.StoredFile> SaveLimitedAsync(Guid owner, string name, Stream content, long max, CancellationToken ct = default)
        {
            if (fail) throw new IOException("C:/private-disk/secret");
            var result = await inner.SaveLimitedAsync(owner, name, content, max, ct);
            Key = result.StoragePath; cancel?.Cancel(); return result;
        }
        /// <summary>Lưu dữ liệu theo hợp đồng của dịch vụ.</summary>
        public Task<string> SaveAsync(Guid owner, string name, Stream content, CancellationToken ct = default) => inner.SaveAsync(owner, name, content, ct);
        /// <summary>Đọc dữ liệu từ luồng.</summary>
        public Task<Stream> ReadAsync(string key, CancellationToken ct = default) => inner.ReadAsync(key, ct);
        /// <summary>Kiểm tra đối tượng có tồn tại hay không.</summary>
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => inner.ExistsAsync(key, ct);
        /// <summary>Xóa đối tượng theo quy tắc của dịch vụ.</summary>
        public Task DeleteAsync(string key, CancellationToken ct = default) => inner.DeleteAsync(key, ct);
    }
    /// <summary>Kiểm thử lỗi xác nhận commit vẫn bảo toàn blob đã commit và số liệu dung lượng.</summary>
    [Fact]
    public async Task Commit_ack_failure_keeps_committed_blob_and_accounting()
    {
        var a = await User(); using var owner = a.client;
        using var scope = factory.Services.CreateScope();
        var existing = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(existing.Database.GetConnectionString())
            .AddInterceptors(new LostCommitAck()).Options;
        await using var db = new AppDbContext(options);
        var storage = scope.ServiceProvider.GetRequiredService<ITManagement.Application.Common.Interfaces.IFileStorageService>();
        var wrapped = new AfterSave(storage, null);
        var service = new ITManagement.Infrastructure.Services.FileService(db, wrapped, new Current(a.id));
        using var bytes = new MemoryStream(new byte[4]);
        await Assert.ThrowsAsync<ITManagement.Domain.Exceptions.BusinessRuleException>(() => service.UploadAsync("a", bytes, default));
        Assert.True(await storage.ExistsAsync(wrapped.Key!));
        Assert.Equal(4, (await existing.StorageQuotas.AsNoTracking().SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.Equal(4, (await existing.FileMetadata.SingleAsync(f => f.StorageQuota.UserId == a.id)).SizeBytes);
    }
    private sealed class LostCommitAck : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        /// <summary>Mô phỏng callback giao dịch đã commit để kiểm thử lỗi xác nhận.</summary>
        public override Task TransactionCommittedAsync(System.Data.Common.DbTransaction transaction,
            Microsoft.EntityFrameworkCore.Diagnostics.TransactionEndEventData eventData, CancellationToken ct = default)
            => Task.FromException(new IOException("Injected lost commit acknowledgement"));
    }
    /// <summary>Kiểm thử đua giữa tải lên và xóa vẫn giữ dung lượng bằng tổng các tệp còn hiệu lực.</summary>
    [Fact]
    public async Task Upload_and_delete_race_keeps_usage_equal_to_live_files()
    {
        var a = await User(); using var owner = a.client;
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().StorageQuotas.Where(q => q.UserId == a.id)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.QuotaBytes, 10));
        var first = await Upload(owner, new byte[6]); first.EnsureSuccessStatusCode();
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        var upload = Upload(owner, new byte[6]);
        var delete = owner.DeleteAsync($"/api/files/{id}");
        await Task.WhenAll(upload, delete);
        Assert.Equal(HttpStatusCode.NoContent, (await delete).StatusCode);
        Assert.Contains((await upload).StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.RequestEntityTooLarge });
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var used = (await db.StorageQuotas.AsNoTracking().SingleAsync(q => q.UserId == a.id)).UsedBytes;
        var actual = await db.FileMetadata.Where(f => f.StorageQuota.UserId == a.id && !f.IsDeleted).SumAsync(f => f.SizeBytes);
        Assert.Equal(actual, used); Assert.InRange(used, 0, 10);
    }
    /// <summary>Kiểm thử Kestrel từ chối yêu cầu vượt 11 MiB trước khi lưu tệp.</summary>
    [Fact]
    public async Task Kestrel_rejects_request_over_11_MiB_without_saving()
    {
        var a = await User(); using var owner = a.client;
        using var app = factory.WithWebHostBuilder(_ => { }); app.UseKestrel();
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.ExpectContinue = true;
        var result = await Upload(client, new byte[12 * 1024 * 1024]);
        // MVC maps body-read errors into the existing 400 validation envelope.
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, (await db.StorageQuotas.SingleAsync(q => q.UserId == a.id)).UsedBytes);
        Assert.False(await db.FileMetadata.AnyAsync(f => f.StorageQuota.UserId == a.id));
    }
    /// <summary>Kiểm thử Swagger mô tả đúng yêu cầu tải tệp dạng multipart.</summary>
    [Fact]
    public async Task Swagger_describes_multipart_upload()
    {
        using var client = factory.CreateClient();
        factory.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("paths").GetProperty("/api/files").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
    }
    /// <summary>Kiểm thử hạn mức cá nhân bỏ qua chủ sở hữu do client yêu cầu và theo dõi tải lên, xóa.</summary>
    [Fact]
    public async Task Personal_quota_ignores_requested_owner_and_tracks_upload_delete()
    {
        var a = await User(); var b = await User(); using var owner = a.client; using var other = b.client;
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().StorageQuotas.Where(q => q.UserId == a.id)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.QuotaBytes, 10));
        async Task<JsonElement> Quota()
        {
            var response = await owner.GetAsync($"/api/me/storage-quota?userId={b.id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        }
        var quota = await Quota();
        Assert.Equal(10, quota.GetProperty("quotaBytes").GetInt64());
        Assert.Equal(10, quota.GetProperty("availableBytes").GetInt64());
        Assert.Equal(10 * 1024 * 1024, quota.GetProperty("maxFileBytes").GetInt64());
        Assert.False(quota.TryGetProperty("userId", out _));
        Assert.False(quota.TryGetProperty("storagePath", out _));
        var result = await Upload(owner, new byte[4]); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var id = (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
        quota = await Quota(); Assert.Equal(4, quota.GetProperty("usedBytes").GetInt64()); Assert.Equal(6, quota.GetProperty("availableBytes").GetInt64());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/files/{id}")).StatusCode);
        Assert.Equal(0, (await Quota()).GetProperty("usedBytes").GetInt64());
    }
    /// <summary>Kiểm thử hạn mức cá nhân yêu cầu tài khoản nhân viên và báo không tìm thấy khi thiếu hạn mức.</summary>
    [Fact]
    public async Task Personal_quota_requires_employee_and_missing_quota_is_not_found()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/storage-quota")).StatusCode);
        var tech = await User("Technician"); using var tc = tech.client;
        Assert.Equal(HttpStatusCode.Forbidden, (await tc.GetAsync("/api/me/storage-quota")).StatusCode);
        var a = await User(); using var employee = a.client;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.StorageQuotas.Where(q => q.UserId == a.id).ExecuteUpdateAsync(s => s.SetProperty(q => q.IsDeleted, true));
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync("/api/me/storage-quota")).StatusCode);
    }
    /// <summary>Kiểm thử hạn mức báo tình trạng vượt mức mà không trả dung lượng khả dụng âm.</summary>
    [Fact]
    public async Task Personal_quota_reports_overuse_without_negative_available_bytes()
    {
        var a = await User(); using var employee = a.client;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.StorageQuotas.Where(q => q.UserId == a.id).ExecuteUpdateAsync(s => s.SetProperty(q => q.QuotaBytes, 2).SetProperty(q => q.UsedBytes, 5));
        var response = await employee.GetAsync("/api/me/storage-quota");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(5, data.GetProperty("usedBytes").GetInt64());
        Assert.Equal(0, data.GetProperty("availableBytes").GetInt64());
    }
}
