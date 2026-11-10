using ITManagement.Application.Contracts.Assets;
namespace ITManagement.Application.Services;
public interface IAssignmentService
{
    /// <summary>Giao tài sản cho người dùng.</summary>
    Task<AssignmentResponse> AssignAsync(Guid assetId, AssignAssetRequest request, CancellationToken ct);
    /// <summary>Đóng lượt giao tài sản bằng hoàn trả hoặc thu hồi.</summary>
    Task<AssignmentResponse> CloseAsync(Guid assetId, CloseAssignmentRequest request, bool revoke, CancellationToken ct);
    /// <summary>Lấy lịch sử giao nhận tài sản.</summary>
    Task<IReadOnlyList<AssignmentResponse>> HistoryAsync(Guid assetId, CancellationToken ct);
    /// <summary>Liệt kê các tài sản đang được giao cho người dùng hiện tại.</summary>
    Task<IReadOnlyList<AssignmentResponse>> MineAsync(CancellationToken ct);
}
