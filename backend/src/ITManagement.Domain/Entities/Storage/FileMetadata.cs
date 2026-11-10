using ITManagement.Domain.Common;

namespace ITManagement.Domain.Entities.Storage;

/// <summary>
/// Metadata của file được lưu trong PostgreSQL.
/// Nội dung thực tế được lưu trên Docker volume, truy cập qua StoragePath.
/// </summary>
public class FileMetadata : BaseEntity
{
    public Guid StorageQuotaId { get; set; }
    public StorageQuota StorageQuota { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;    // Tên file hiển thị
    public string StoragePath { get; set; } = string.Empty;    // Đường dẫn thực trên volume (PRIVATE)
    public string ContentType { get; set; } = string.Empty;    // MIME type
    public long SizeBytes { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thư mục ảo phân cấp của người dùng, vd: "/documents/reports"
    /// </summary>
    public string VirtualFolder { get; set; } = "/";
}
