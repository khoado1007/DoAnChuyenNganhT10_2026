using ITManagement.Domain.Entities.Storage;

namespace ITManagement.Domain.Interfaces.Repositories;

public interface IStorageRepository
{
    /// <summary>Tìm hạn mức lưu trữ theo mã người dùng.</summary>
    Task<StorageQuota?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Lấy hoặc tạo hạn mức lưu trữ của người dùng.</summary>
    Task<StorageQuota> GetOrCreateAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Tìm metadata tệp theo mã định danh.</summary>
    Task<FileMetadata?> GetFileByIdAsync(Guid fileId, CancellationToken ct = default);
    /// <summary>Liệt kê metadata tệp của người dùng.</summary>
    Task<IEnumerable<FileMetadata>> GetFilesByUserAsync(Guid userId, string? folder = null, CancellationToken ct = default);
    /// <summary>Thêm metadata cho tệp mới.</summary>
    Task AddFileAsync(FileMetadata file, CancellationToken ct = default);
    /// <summary>Cập nhật hạn mức và dung lượng đã sử dụng.</summary>
    Task UpdateQuotaAsync(StorageQuota quota, CancellationToken ct = default);
    /// <summary>Xóa metadata tệp.</summary>
    Task DeleteFileAsync(Guid fileId, CancellationToken ct = default);
}
