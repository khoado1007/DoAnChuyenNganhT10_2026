using ITManagement.Domain.Common;

namespace ITManagement.Domain.Entities.Storage;

/// <summary>
/// Quota lưu trữ của từng tài khoản nhân viên.
/// </summary>
public class StorageQuota : BaseEntity
{
    public Guid UserId { get; set; }
    public User.AppUser User { get; set; } = null!;

    public long QuotaBytes { get; set; } = 10L * 1024 * 1024 * 1024; // Default 10 GB
    public long UsedBytes { get; set; } = 0;

    public long AvailableBytes => QuotaBytes - UsedBytes;
    public bool IsExceeded => UsedBytes >= QuotaBytes;

    // Navigation
    public ICollection<FileMetadata> Files { get; set; } = [];
}
