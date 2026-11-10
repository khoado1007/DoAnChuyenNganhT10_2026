using ITManagement.Domain.Entities.Storage;
using ITManagement.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Persistence.Repositories;

public class StorageRepository : IStorageRepository
{
    private readonly AppDbContext _db;

    /// <summary>Khởi tạo repository lưu trữ với ngữ cảnh dữ liệu.</summary>
    public StorageRepository(AppDbContext db) => _db = db;

    /// <summary>Tìm hạn mức lưu trữ của người dùng.</summary>
    public Task<StorageQuota?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _db.StorageQuotas.Include(q => q.Files).FirstOrDefaultAsync(q => q.UserId == userId, ct);

    /// <summary>Lấy hạn mức lưu trữ hoặc tạo bản ghi mặc định.</summary>
    public async Task<StorageQuota> GetOrCreateAsync(Guid userId, CancellationToken ct = default)
    {
        var quota = await GetByUserIdAsync(userId, ct);
        if (quota != null) return quota;

        quota = new StorageQuota { UserId = userId };
        await _db.StorageQuotas.AddAsync(quota, ct);
        return quota;
    }

    /// <summary>Tìm metadata tệp theo mã định danh.</summary>
    public Task<FileMetadata?> GetFileByIdAsync(Guid fileId, CancellationToken ct = default)
        => _db.FileMetadata.Include(f => f.StorageQuota).FirstOrDefaultAsync(f => f.Id == fileId, ct);

    /// <summary>Liệt kê metadata tệp thuộc người dùng.</summary>
    public async Task<IEnumerable<FileMetadata>> GetFilesByUserAsync(Guid userId, string? folder = null, CancellationToken ct = default)
    {
        var query = _db.FileMetadata
            .Include(f => f.StorageQuota)
            .Where(f => f.StorageQuota.UserId == userId);

        if (!string.IsNullOrWhiteSpace(folder))
            query = query.Where(f => f.VirtualFolder == folder);

        return await query.OrderBy(f => f.DisplayName).ToListAsync(ct);
    }

    /// <summary>Thêm metadata tệp vào ngữ cảnh dữ liệu.</summary>
    public async Task AddFileAsync(FileMetadata file, CancellationToken ct = default)
        => await _db.FileMetadata.AddAsync(file, ct);

    /// <summary>Cập nhật hạn mức và dung lượng đã sử dụng.</summary>
    public Task UpdateQuotaAsync(StorageQuota quota, CancellationToken ct = default)
    {
        _db.StorageQuotas.Update(quota);
        return Task.CompletedTask;
    }

    /// <summary>Đánh dấu metadata tệp cần xóa.</summary>
    public async Task DeleteFileAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await _db.FileMetadata.FindAsync([fileId], ct);
        if (file != null) _db.FileMetadata.Remove(file);
    }
}
