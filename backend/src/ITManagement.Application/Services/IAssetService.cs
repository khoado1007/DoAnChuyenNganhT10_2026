using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Assets;
namespace ITManagement.Application.Services;
public interface IAssetService
{
    /// <summary>Lấy danh sách rút gọn tài sản phục vụ tra cứu.</summary>
    Task<IReadOnlyList<AssetLookupResponse>> LookupAsync(CancellationToken ct);
    /// <summary>Tìm kiếm và phân trang danh sách tài sản.</summary>
    Task<PagedResult<AssetResponse>> SearchAsync(AssetQuery query, CancellationToken ct);
    /// <summary>Lấy chi tiết tài sản theo mã định danh.</summary>
    Task<AssetResponse> GetAsync(Guid id, CancellationToken ct);
    /// <summary>Tạo tài sản mới.</summary>
    Task<AssetResponse> CreateAsync(CreateAssetRequest request, CancellationToken ct);
    /// <summary>Cập nhật thông tin tài sản.</summary>
    Task<AssetResponse> UpdateAsync(Guid id, UpdateAssetRequest request, CancellationToken ct);
}
