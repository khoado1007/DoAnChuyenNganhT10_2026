using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Assets;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ tìm kiếm và quản lý tài sản.</summary>
public sealed class AssetService(AppDbContext db) : IAssetService
{
    // Chuyển thực thể tài sản thành dữ liệu phản hồi API.
    private static AssetResponse Response(Asset a) => new(a.Id, a.AssetCode, a.Name, a.Type, a.Status,
        a.SerialNumber, a.Manufacturer, a.Model, a.Location, a.Notes, a.CreatedAt, a.UpdatedAt);
    /// <summary>Lấy danh sách rút gọn các tài sản phục vụ tra cứu.</summary>
    public async Task<IReadOnlyList<AssetLookupResponse>> LookupAsync(CancellationToken ct) =>
        await db.Assets.AsNoTracking().OrderBy(a => a.AssetCode)
            .Select(a => new AssetLookupResponse(a.Id, a.AssetCode, a.Name, a.Status.ToString())).ToListAsync(ct);
    /// <summary>Tìm kiếm tài sản theo từ khóa và phân trang.</summary>
    public async Task<PagedResult<AssetResponse>> SearchAsync(AssetQuery query, CancellationToken ct)
    {
        var assets = db.Assets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            assets = assets.Where(a => a.AssetCode.ToLower().Contains(search) || a.Name.ToLower().Contains(search) || a.SerialNumber.ToLower().Contains(search));
        }
        if (query.Status is not null) assets = assets.Where(a => a.Status == query.Status);
        var count = await assets.CountAsync(ct);
        var rows = await assets.OrderBy(a => a.AssetCode).ThenBy(a => a.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return PagedResult<AssetResponse>.Create(rows.Select(Response), query.Page, query.PageSize, count);
    }
    /// <summary>Lấy chi tiết tài sản theo mã định danh.</summary>
    public async Task<AssetResponse> GetAsync(Guid id, CancellationToken ct) =>
        Response(await db.Assets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Asset", id));
    /// <summary>Kiểm tra dữ liệu rồi tạo tài sản mới.</summary>
    public async Task<AssetResponse> CreateAsync(CreateAssetRequest request, CancellationToken ct)
    {
        var asset = new Asset { AssetCode = request.AssetCode.Trim(), Name = request.Name.Trim(), Type = request.Type,
            SerialNumber = request.SerialNumber?.Trim() ?? "", Manufacturer = request.Manufacturer?.Trim() ?? "",
            Model = request.Model?.Trim() ?? "", Location = request.Location?.Trim() ?? "", Notes = request.Notes?.Trim() ?? "" };
        db.Assets.Add(asset);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new BusinessRuleException("Mã tài sản đã tồn tại."); }
        return Response(asset);
    }
    /// <summary>Kiểm tra quyền và cập nhật thông tin tài sản.</summary>
    public async Task<AssetResponse> UpdateAsync(Guid id, UpdateAssetRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await db.Assets.FromSqlInterpolated($"SELECT * FROM assets WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Asset", id);
        if (asset.Status == AssetStatus.Retired && request.Status != AssetStatus.Retired)
            throw new BusinessRuleException("Tài sản đã ngừng sử dụng không thể kích hoạt lại.");
        if (request.Status != asset.Status && (asset.Status == AssetStatus.Assigned || request.Status == AssetStatus.Assigned
            || await db.AssetAssignments.AnyAsync(a => a.AssetId == id && a.Status == AssignmentStatus.Active, ct)))
            throw new BusinessRuleException("Thay đổi cấp phát phải qua chức năng cấp/trả tài sản.");
        asset.Name = request.Name.Trim(); asset.Type = request.Type; asset.Status = request.Status;
        asset.SerialNumber = request.SerialNumber?.Trim() ?? ""; asset.Manufacturer = request.Manufacturer?.Trim() ?? "";
        asset.Model = request.Model?.Trim() ?? ""; asset.Location = request.Location?.Trim() ?? ""; asset.Notes = request.Notes?.Trim() ?? "";
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(asset);
    }
}
