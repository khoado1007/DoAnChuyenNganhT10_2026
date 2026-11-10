using ITManagement.Domain.Entities.Asset;

namespace ITManagement.Domain.Interfaces.Repositories;

public interface IAssetRepository
{
    /// <summary>Tìm tài sản theo mã định danh.</summary>
    Task<Asset?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Tìm tài sản theo mã tài sản.</summary>
    Task<Asset?> GetByCodeAsync(string assetCode, CancellationToken ct = default);
    /// <summary>Truy vấn danh sách tài sản theo điều kiện lọc.</summary>
    Task<IEnumerable<Asset>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Truy vấn các tài sản có thể được giao.</summary>
    Task<IEnumerable<Asset>> GetAvailableAsync(CancellationToken ct = default);
    /// <summary>Thêm tài sản mới.</summary>
    Task AddAsync(Asset asset, CancellationToken ct = default);
    /// <summary>Cập nhật tài sản hiện có.</summary>
    Task UpdateAsync(Asset asset, CancellationToken ct = default);
    /// <summary>Kiểm tra mã tài sản đã được sử dụng.</summary>
    Task<bool> ExistsByCodeAsync(string assetCode, CancellationToken ct = default);
}
