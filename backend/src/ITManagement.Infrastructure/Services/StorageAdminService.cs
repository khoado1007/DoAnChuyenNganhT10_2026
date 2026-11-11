using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Files;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Storage;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ quản trị hạn mức và đối soát vùng lưu trữ.</summary>
public sealed class StorageAdminService(AppDbContext db, IFileStorageService storage, ICurrentUserService current) : IStorageAdminService
{
    // Chuẩn hóa khóa lưu trữ để so sánh nhất quán.
    private static string Canonical(string key) => key.Replace('\\', '/').ToLowerInvariant();
    // Chuyển hạn mức lưu trữ thành dữ liệu phản hồi API.
    private static QuotaResponse Response(StorageQuota q, string name) => new(q.UserId, name, q.QuotaBytes, q.UsedBytes, q.QuotaBytes - q.UsedBytes);
    // Lấy khóa đồng bộ thao tác quản trị vùng lưu trữ.
    private async Task<StorageQuota> LockAsync(Guid userId, CancellationToken ct)
    {
        var quota = await db.StorageQuotas.FromSqlInterpolated($"SELECT * FROM StorageQuotas WITH (UPDLOCK, ROWLOCK) WHERE \"UserId\" = {userId}")
            .SingleOrDefaultAsync(q => !q.IsDeleted, ct);
        if (quota is null || !await db.AppUsers.AnyAsync(u => u.Id == userId, ct)) throw new NotFoundException("Quota", userId);
        return quota;
    }
    // Tính tổng dung lượng của metadata tệp còn hiệu lực.
    private static long SumUsage(IEnumerable<FileMetadata> files)
    {
        long result = 0;
        foreach (var file in files.Where(f => !f.IsDeleted))
        {
            if (file.SizeBytes < 0 || file.SizeBytes > long.MaxValue - result)
                throw new BusinessRuleException("Metadata dung lượng không hợp lệ; cần kiểm tra thủ công.");
            result += file.SizeBytes;
        }
        return result;
    }
    /// <summary>Liệt kê hạn mức lưu trữ và mức sử dụng của người dùng.</summary>
    public async Task<PagedResult<QuotaResponse>> ListAsync(QuotaQuery query, CancellationToken ct)
    {
        var quotas = db.StorageQuotas.AsNoTracking().Where(q => !q.IsDeleted && !q.User.IsDeleted);
        var count = await quotas.CountAsync(ct);
        var rows = await quotas.OrderBy(q => q.User.Username).ThenBy(q => q.UserId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(q => new QuotaResponse(q.UserId, q.User.Username, q.QuotaBytes, q.UsedBytes, q.QuotaBytes - q.UsedBytes)).ToListAsync(ct);
        return PagedResult<QuotaResponse>.Create(rows, query.Page, query.PageSize, count);
    }
    /// <summary>Cập nhật hạn mức sau khi kiểm tra dung lượng đang sử dụng.</summary>
    public async Task<QuotaResponse> UpdateAsync(Guid userId, long quotaBytes, CancellationToken ct)
    {
        if (quotaBytes < 0 || quotaBytes > UpdateQuotaRequest.MaxQuotaBytes) throw new BusinessRuleException("Quota phải từ 0 đến 1 TiB.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var quota = await LockAsync(userId, ct);
        var usage = SumUsage(await db.FileMetadata.AsNoTracking().Where(f => f.StorageQuotaId == quota.Id).ToListAsync(ct));
        if (quotaBytes < Math.Max(quota.UsedBytes, usage)) throw new BusinessRuleException("Quota không được nhỏ hơn dung lượng đang sử dụng.");
        quota.QuotaBytes = quotaBytes; quota.UpdatedAt = DateTime.UtcNow; quota.UpdatedBy = current.Username;
        await db.SaveChangesAsync(ct);
        var name = await db.AppUsers.Where(u => u.Id == userId).Select(u => u.Username).SingleAsync(ct);
        await tx.CommitAsync(ct);
        return Response(quota, name);
    }
    /// <summary>Đối soát dung lượng và dọn các đối tượng lưu trữ không còn tham chiếu.</summary>
    public async Task<ReconcileStorageResponse> ReconcileAsync(Guid userId, bool apply, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var quota = await LockAsync(userId, ct);
            var files = await db.FileMetadata.AsNoTracking().Where(f => f.StorageQuotaId == quota.Id).ToListAsync(ct);
            var used = SumUsage(files);
            var recorded = quota.UsedBytes;
            var inventory = await storage.InspectAsync(userId, ct);
            var invalid = 0; var missing = 0;
            foreach (var file in files)
            {
                var parts = file.StoragePath.Replace('\\', '/').Split('/');
                if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "D", out var owner) || owner != userId)
                { invalid++; continue; }
                try { if (!await storage.ExistsAsync(file.StoragePath, ct) && !file.IsDeleted) missing++; }
                catch (ArgumentException) { invalid++; }
            }
            // Protect references even when another account has corrupt metadata pointing here.
            var prefix = userId.ToString("D");
            var references = await db.FileMetadata.AsNoTracking()
                .Where(f => f.StoragePath.ToLower().StartsWith(prefix))
                .Select(f => new { f.StoragePath, f.StorageQuotaId, f.IsDeleted }).ToListAsync(ct);
            var protectedKeys = references.Where(f => !f.IsDeleted || f.StorageQuotaId != quota.Id)
                .Select(f => Canonical(f.StoragePath)).ToHashSet(StringComparer.Ordinal);
            var tombstones = files.Where(f => f.IsDeleted).Select(f => Canonical(f.StoragePath)).ToHashSet(StringComparer.Ordinal);
            var candidates = inventory.Keys.Where(k => !protectedKeys.Contains(Canonical(k))).ToList();
            var tombCount = candidates.Count(k => tombstones.Contains(Canonical(k)));
            var deleted = 0; var failures = 0;
            if (apply)
            {
                if (invalid == 0)
                    foreach (var key in candidates)
                    {
                        ct.ThrowIfCancellationRequested();
                        try { await storage.DeleteAsync(key, ct); deleted++; }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { failures++; }
                    }
                quota.UsedBytes = used; quota.UpdatedAt = DateTime.UtcNow; quota.UpdatedBy = current.Username;
                await db.SaveChangesAsync(ct);
                ct.ThrowIfCancellationRequested();
                await tx.CommitAsync(CancellationToken.None);
            }
            return new(apply, recorded, used, candidates.Count - tombCount, tombCount, missing, invalid,
                inventory.SkippedEntries, invalid != 0, deleted, failures);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { throw new BusinessRuleException("Không kiểm tra được storage. Vui lòng thử lại hoặc kiểm tra quyền lưu trữ."); }
    }
}
