using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Persistence.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _db;

    /// <summary>Khởi tạo repository tài sản với ngữ cảnh dữ liệu.</summary>
    public AssetRepository(AppDbContext db) => _db = db;

    /// <summary>Tìm tài sản theo mã định danh.</summary>
    public Task<Asset?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Assets.Include(a => a.Assignments).FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>Tìm tài sản theo mã tài sản.</summary>
    public Task<Asset?> GetByCodeAsync(string assetCode, CancellationToken ct = default)
        => _db.Assets.FirstOrDefaultAsync(a => a.AssetCode == assetCode, ct);

    /// <summary>Truy vấn danh sách tài sản theo điều kiện lọc và phân trang.</summary>
    public async Task<IEnumerable<Asset>> GetAllAsync(CancellationToken ct = default)
        => await _db.Assets.ToListAsync(ct);

    /// <summary>Truy vấn các tài sản hiện có thể được giao.</summary>
    public async Task<IEnumerable<Asset>> GetAvailableAsync(CancellationToken ct = default)
        => await _db.Assets.Where(a => a.Status == Domain.Enums.AssetStatus.Available).ToListAsync(ct);

    /// <summary>Thêm tài sản mới vào ngữ cảnh dữ liệu.</summary>
    public async Task AddAsync(Asset asset, CancellationToken ct = default)
        => await _db.Assets.AddAsync(asset, ct);

    /// <summary>Đánh dấu tài sản cần cập nhật trong ngữ cảnh dữ liệu.</summary>
    public Task UpdateAsync(Asset asset, CancellationToken ct = default)
    {
        _db.Assets.Update(asset);
        return Task.CompletedTask;
    }

    /// <summary>Kiểm tra mã tài sản đã được sử dụng hay chưa.</summary>
    public Task<bool> ExistsByCodeAsync(string assetCode, CancellationToken ct = default)
        => _db.Assets.AnyAsync(a => a.AssetCode == assetCode, ct);
}
