using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Audit;
using ITManagement.Application.Services;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

/// <summary>
/// Đọc nhật ký và metadata file cho Admin. Nhật ký chỉ phạm vi thao tác file (7 thao tác ghi/xóa/khôi phục);
/// dữ liệu lịch sử khác vẫn giữ trong database nhưng không trả qua API. Không trả nội dung file hay token.
/// </summary>
public sealed class AuditService(AppDbContext db) : IAuditService
{
    /// <summary>Các sự kiện thao tác file được hiển thị, tên chuẩn PascalCase.</summary>
    private static readonly string[] FileActions =
    [
        "ServerFileCreate", "ServerFileWrite", "ServerFileRename", "ServerFileMove",
        "ServerFileCopy", "ServerFileDelete", "ServerFileRestore"
    ];

    private static readonly string[] FileActionKeys = FileActions.Select(action => action.ToLowerInvariant()).ToArray();

    /// <summary>Đưa tên sự kiện cũ (ví dụ "ServerFilecreate") về tên chuẩn để hiển thị và lọc.</summary>
    private static string CanonicalAction(string action) =>
        FileActions.FirstOrDefault(known => string.Equals(known, action, StringComparison.OrdinalIgnoreCase)) ?? action;

    public async Task<PagedResult<AuditEventResponse>> ListAuditEventsAsync(AuditLogQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        // Chỉ sự kiện thao tác file; so sánh chữ thường để khớp dữ liệu cũ ghi sai kiểu tên.
        var q = db.AppAuditEvents.AsNoTracking().Where(e => FileActionKeys.Contains(e.Action.ToLower()));

        if (query.From.HasValue)
            q = q.Where(e => e.CreatedAt >= query.From.Value.UtcDateTime);
        if (query.To.HasValue)
            q = q.Where(e => e.CreatedAt <= query.To.Value.UtcDateTime);
        if (query.ActorId.HasValue)
            q = q.Where(e => e.ActorId == query.ActorId.Value);
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToLowerInvariant();
            q = q.Where(e => e.Action.ToLower() == action);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Lấy tên actor (AppUser) cho các sự kiện có ActorId
        var actorIds = items.Where(e => e.ActorId.HasValue).Select(e => e.ActorId!.Value).Distinct().ToList();
        var actors = await db.AppUsers.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var responses = items.Select(e => new AuditEventResponse(
            e.Id,
            e.ActorId,
            e.ActorId.HasValue && actors.TryGetValue(e.ActorId.Value, out var name) ? name : null,
            CanonicalAction(e.Action),
            e.ResourceId,
            DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc)
        )).ToList();

        return PagedResult<AuditEventResponse>.Create(responses, page, pageSize, total);
    }

    public async Task<PagedResult<AdminFileResponse>> ListFilesAdminAsync(AdminFileQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var q = from f in db.ProfileFiles.AsNoTracking()
                join p in db.LocalProfiles.AsNoTracking() on f.ProfileId equals p.Id
                select new { f, p };

        if (query.UserId.HasValue)
            q = q.Where(x => x.p.UserId == query.UserId.Value);
        if (query.AssetId.HasValue)
            q = q.Where(x => x.p.AssetId == query.AssetId.Value);
        if (!string.IsNullOrWhiteSpace(query.Folder))
            q = q.Where(x => x.f.Folder == query.Folder.Trim());

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.f.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Lấy tên user và asset
        var userIds = rows.Select(r => r.p.UserId).Distinct().ToList();
        var assetIds = rows.Select(r => r.p.AssetId).Distinct().ToList();

        var users = await db.AppUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var assets = await db.Assets.AsNoTracking()
            .Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var responses = rows.Select(r => new AdminFileResponse(
            r.f.Id,
            r.f.ProfileId,
            r.p.UserId,
            users.TryGetValue(r.p.UserId, out var un) ? un : null,
            r.p.AssetId,
            assets.TryGetValue(r.p.AssetId, out var an) ? an : null,
            r.f.Folder,
            r.f.Name,
            r.f.Extension,
            r.f.SizeBytes,
            r.p.StorageBackend
        )).ToList();

        return PagedResult<AdminFileResponse>.Create(responses, page, pageSize, total);
    }
}
