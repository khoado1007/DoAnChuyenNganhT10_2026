namespace ITManagement.Application.Common.Interfaces;

/// <summary>
/// Abstraction cho service lưu trữ tệp thực tế trên Docker volume.
/// Infrastructure layer sẽ implement.
/// </summary>
public sealed record StoredFile(string StoragePath, long SizeBytes);

public sealed record StorageInventory(IReadOnlyList<string> Keys, int SkippedEntries);

public interface IFileStorageService
{
    /// <summary>
    /// Lưu file lên volume, trả về đường dẫn nội bộ (StoragePath).
    /// </summary>
    Task<StoredFile> SaveLimitedAsync(Guid userId, string fileName, Stream content, long maxBytes, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>Liệt kê đối tượng trong vùng lưu trữ của người dùng.</summary>
    Task<StorageInventory> InspectAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>Lưu luồng dữ liệu và trả về khóa lưu trữ.</summary>
    Task<string> SaveAsync(Guid userId, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Đọc nội dung file từ StoragePath (private path).
    /// </summary>
    Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default);

    /// <summary>
    /// Xóa file khỏi volume.
    /// </summary>
    Task DeleteAsync(string storagePath, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra file có tồn tại không.
    /// </summary>
    Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default);
}
