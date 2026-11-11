using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Contracts.Assets;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ giao tài sản và theo dõi lịch sử giao nhận.</summary>
public sealed class AssignmentService(AppDbContext db, ICurrentUserService current) : IAssignmentService
{
    // Chuyển truy vấn lượt giao thành các phản hồi API.
    private IQueryable<AssignmentResponse> Responses(IQueryable<AssetAssignment> rows) => rows.Select(a => new AssignmentResponse(
        a.Id, a.AssetId, a.Asset.AssetCode, a.Asset.Name, a.UserId, a.User.Username,
        a.AssignedAt, a.ReturnedAt, a.Status, a.AssignedBy, a.Notes, a.ClosedBy, a.CloseNotes));
    // Khóa hàng tài sản trong giao dịch để đồng bộ các thao tác cấp phát.
    private async Task<Asset> LockAsset(Guid id, CancellationToken ct) =>
        await db.Assets.FromSqlInterpolated($"SELECT * FROM assets WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}").SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Asset", id);
    // Lấy phản hồi cho một lượt giao theo mã định danh.
    private Task<AssignmentResponse> Response(Guid id, CancellationToken ct) =>
        Responses(db.AssetAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.Id == id)).SingleAsync(ct);

    /// <summary>Kiểm tra điều kiện rồi giao tài sản cho người dùng.</summary>
    public async Task<AssignmentResponse> AssignAsync(Guid assetId, AssignAssetRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await LockAsset(assetId, ct);
        if (asset.Status != AssetStatus.Available || await db.AssetAssignments.AnyAsync(a => a.AssetId == assetId && a.Status == AssignmentStatus.Active, ct))
            throw new BusinessRuleException("Tài sản không sẵn sàng cấp phát.");
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {request.UserId}").SingleOrDefaultAsync(ct);
        if (user is null || user.Role != UserRole.Employee || user.Status != UserStatus.Active)
            throw new BusinessRuleException("Người nhận phải là nhân viên đang hoạt động.");
        var row = new AssetAssignment { AssetId = assetId, UserId = user.Id, AssignedBy = current.Username!, CreatedBy = current.Username,
            Notes = request.Notes?.Trim() ?? "" };
        db.AssetAssignments.Add(row); asset.Status = AssetStatus.Assigned; asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await Response(row.Id, ct);
    }
    /// <summary>Đóng lượt giao tài sản bằng thao tác hoàn trả hoặc thu hồi.</summary>
    public async Task<AssignmentResponse> CloseAsync(Guid assetId, CloseAssignmentRequest request, bool revoke, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await LockAsset(assetId, ct);
        var row = await db.AssetAssignments.SingleOrDefaultAsync(a => a.Id == request.AssignmentId && a.AssetId == assetId && a.Status == AssignmentStatus.Active, ct);
        if (row is null || asset.Status != AssetStatus.Assigned)
            throw new BusinessRuleException("Lần cấp phát không còn hoạt động. Hãy làm mới danh sách.");
        row.Status = revoke ? AssignmentStatus.Revoked : AssignmentStatus.Returned;
        row.ReturnedAt = DateTime.UtcNow; row.ClosedBy = current.Username; row.CloseNotes = request.Notes?.Trim() ?? "";
        row.UpdatedAt = row.ReturnedAt; row.UpdatedBy = current.Username;
        asset.Status = AssetStatus.Available; asset.UpdatedAt = row.ReturnedAt;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await Response(row.Id, ct);
    }
    /// <summary>Lấy lịch sử giao tài sản theo phạm vi người dùng hiện tại.</summary>
    public async Task<IReadOnlyList<AssignmentResponse>> HistoryAsync(Guid assetId, CancellationToken ct)
    {
        if (!await db.Assets.IgnoreQueryFilters().AnyAsync(a => a.Id == assetId, ct)) throw new NotFoundException("Asset", assetId);
        // Historical rows remain visible even if a related account is later soft deleted.
        return await Responses(db.AssetAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.AssetId == assetId)
            .OrderByDescending(a => a.AssignedAt).ThenBy(a => a.Id)).ToListAsync(ct);
    }
    /// <summary>Liệt kê các tài sản đang được giao cho người dùng hiện tại.</summary>
    public async Task<IReadOnlyList<AssignmentResponse>> MineAsync(CancellationToken ct) =>
        await Responses(db.AssetAssignments.AsNoTracking().Where(a => a.UserId == current.UserId && a.Status == AssignmentStatus.Active)
            .OrderByDescending(a => a.AssignedAt)).ToListAsync(ct);
}
