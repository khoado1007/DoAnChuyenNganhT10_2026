using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Files;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Storage;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ tải lên, truy cập và xóa tệp theo chủ sở hữu.</summary>
public sealed class FileService(AppDbContext db, IFileStorageService storage, ICurrentUserService current) : IFileService
{
    private IQueryable<FileMetadata> Owned => db.FileMetadata.Where(f => f.StorageQuota.UserId == current.UserId && !f.StorageQuota.IsDeleted);
    // Kiểm tra khóa lưu trữ thuộc đúng chủ sở hữu tệp.
    private void CheckOwnerKey(string key)
    {
        var parts = key.Replace('\\', '/').Split('/');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "D", out var owner) || owner != current.UserId)
            throw new BusinessRuleException("Metadata file không hợp lệ; cần quản trị kiểm tra.");
    }

    /// <summary>Liệt kê metadata tệp của người dùng hiện tại.</summary>
    public async Task<PagedResult<FileResponse>> ListAsync(FileQuery query, CancellationToken ct)
    {
        var files = Owned.AsNoTracking().Where(f => !f.IsDeleted);
        var count = await files.CountAsync(ct);
        var rows = await files.OrderByDescending(f => f.UploadedAt).ThenBy(f => f.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(f => new FileResponse(f.Id, f.DisplayName, f.SizeBytes, f.UploadedAt, f.VirtualFolder)).ToListAsync(ct);
        return PagedResult<FileResponse>.Create(rows, query.Page, query.PageSize, count);
    }
    /// <summary>Tính hạn mức, dung lượng sử dụng và dung lượng còn lại.</summary>
    public async Task<PersonalQuotaResponse> GetQuotaAsync(CancellationToken ct)
    {
        var quota = await db.StorageQuotas.AsNoTracking()
            .SingleOrDefaultAsync(q => q.UserId == current.UserId && !q.IsDeleted, ct)
            ?? throw new NotFoundException("Quota", current.UserId ?? Guid.Empty);
        if (quota.QuotaBytes < 0 || quota.UsedBytes < 0)
            throw new BusinessRuleException("Dung lượng lưu trữ không nhất quán; cần quản trị kiểm tra.");
        return new(quota.QuotaBytes, quota.UsedBytes, Math.Max(0, quota.QuotaBytes - quota.UsedBytes), FileUploadLimits.MaxFileBytes);
    }
    /// <summary>Kiểm tra quyền sở hữu rồi mở nội dung tệp.</summary>
    public async Task<FileDownload> DownloadAsync(Guid id, CancellationToken ct)
    {
        var file = await Owned.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id && !f.IsDeleted, ct) ?? throw new NotFoundException("File", id);
        CheckOwnerKey(file.StoragePath);
        var name = file.DisplayName.Replace('\\', '/').Split('/').Last();
        name = string.Concat(name.Where(c => !char.IsControl(c)));
        if (string.IsNullOrWhiteSpace(name)) name = "download";
        try { return new(await storage.ReadAsync(file.StoragePath, ct), name); }
        catch (FileNotFoundException) { throw new NotFoundException("File", id); }
        catch (DirectoryNotFoundException) { throw new NotFoundException("File", id); }
        catch (ArgumentException) { throw new BusinessRuleException("Metadata file không hợp lệ; cần quản trị kiểm tra."); }
        catch (IOException) { throw new BusinessRuleException("Không đọc được file. Vui lòng thử lại."); }
        catch (UnauthorizedAccessException) { throw new BusinessRuleException("Không đọc được file. Vui lòng thử lại."); }
    }
    /// <summary>Kiểm tra hạn mức rồi lưu tệp và metadata.</summary>
    public async Task<FileResponse> UploadAsync(string fileName, Stream content, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not Guid owner)
            throw new UnauthorizedException();
        var name = fileName.Replace('\\', '/').Split('/').Last();
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Length > 255 || name.Any(char.IsControl))
            throw new BusinessRuleException("Tên file không hợp lệ (tối đa 255 ký tự). ");

        StoredFile? stored = null;
        var commitStarted = false;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var quota = await db.StorageQuotas.FromSqlInterpolated($"SELECT * FROM \"StorageQuotas\" WHERE \"UserId\" = {owner} FOR UPDATE")
                .SingleOrDefaultAsync(q => !q.IsDeleted, ct)
                ?? throw new BusinessRuleException("Tài khoản chưa có quota lưu trữ.");
            if (quota.UsedBytes < 0 || quota.QuotaBytes < quota.UsedBytes)
                throw new BusinessRuleException("Dung lượng lưu trữ không nhất quán; cần quản trị kiểm tra.");
            stored = await storage.SaveLimitedAsync(owner, name, content,
                Math.Min(FileUploadLimits.MaxFileBytes, quota.QuotaBytes - quota.UsedBytes), ct);
            var file = new FileMetadata
            {
                StorageQuotaId = quota.Id, DisplayName = name, StoragePath = stored.StoragePath,
                SizeBytes = stored.SizeBytes, ContentType = "application/octet-stream",
                FileExtension = Path.GetExtension(name), CreatedBy = current.Username
            };
            db.FileMetadata.Add(file);
            quota.UsedBytes += stored.SizeBytes;
            quota.UpdatedAt = DateTime.UtcNow; quota.UpdatedBy = current.Username;
            await db.SaveChangesAsync(ct);
            ct.ThrowIfCancellationRequested();
            // Past this point do not cancel commit: its result could otherwise be ambiguous.
            commitStarted = true;
            await tx.CommitAsync(CancellationToken.None);
            return new(file.Id, file.DisplayName, file.SizeBytes, file.UploadedAt, file.VirtualFolder);
        }
        catch (Exception error)
        {
            // A failed commit may have succeeded on the server. Never delete its blob blindly.
            // Known-uncommitted uploads are cleaned with a token independent of the request.
            if (stored is not null && !commitStarted)
            {
                try { await storage.DeleteAsync(stored.StoragePath, CancellationToken.None); }
                catch (IOException) { /* P07B reconciliation can recover orphan blobs. */ }
                catch (UnauthorizedAccessException) { /* Retain for reconciliation. */ }
            }
            if (error is IOException or UnauthorizedAccessException)
                throw new BusinessRuleException("Không lưu được file. Vui lòng thử lại.");
            throw;
        }
    }
    /// <summary>Xóa tệp và giải phóng dung lượng đã sử dụng.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        string key;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // All quota mutations must use this row lock (including future uploads).
            var quota = await db.StorageQuotas.FromSqlInterpolated($"SELECT * FROM \"StorageQuotas\" WHERE \"UserId\" = {current.UserId} FOR UPDATE")
                .SingleOrDefaultAsync(q => !q.IsDeleted, ct) ?? throw new NotFoundException("File", id);
            var file = await db.FileMetadata.SingleOrDefaultAsync(f => f.Id == id && f.StorageQuotaId == quota.Id, ct)
                ?? throw new NotFoundException("File", id);
            key = file.StoragePath; CheckOwnerKey(key);
            try { await storage.ExistsAsync(key, ct); } // Validate private key before changing accounting.
            catch (ArgumentException) { throw new BusinessRuleException("Metadata file không hợp lệ; cần quản trị kiểm tra."); }
            catch (IOException) { throw new BusinessRuleException("Không kiểm tra được storage. Vui lòng thử lại."); }
            catch (UnauthorizedAccessException) { throw new BusinessRuleException("Không kiểm tra được storage. Vui lòng thử lại."); }
            if (!file.IsDeleted)
            {
                if (file.SizeBytes < 0 || quota.UsedBytes < file.SizeBytes)
                    throw new BusinessRuleException("Dung lượng lưu trữ không nhất quán; cần quản trị kiểm tra.");
                file.IsDeleted = true; file.UpdatedAt = DateTime.UtcNow; file.UpdatedBy = current.Username;
                quota.UsedBytes -= file.SizeBytes;
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        // Keep tombstone/key: failure or crash here is recoverable by retrying DELETE.
        try { await storage.DeleteAsync(key, ct); }
        catch (IOException) { throw new BusinessRuleException("File đã ẩn; hãy thử xóa lại để hoàn tất dọn storage."); }
        catch (UnauthorizedAccessException) { throw new BusinessRuleException("File đã ẩn; hãy thử xóa lại để hoàn tất dọn storage."); }
        catch (ArgumentException) { throw new BusinessRuleException("Metadata file không hợp lệ; cần quản trị kiểm tra."); }
    }
}

