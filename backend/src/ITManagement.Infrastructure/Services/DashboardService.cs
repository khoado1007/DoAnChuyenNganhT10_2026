using ITManagement.Application.Contracts.Dashboard;
using ITManagement.Application.Services;
using ITManagement.Domain.Enums;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

public sealed class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<AdminOverviewResponse> GetOverviewAsync(CancellationToken ct)
    {
        // Global query filter loại soft-delete; không tải nội dung cá nhân để thống kê.
        var users = await db.AppUsers.CountAsync(ct);
        var assets = await db.Assets.CountAsync(ct);
        var assignedAssets = await db.Assets.CountAsync(a => a.Status == AssetStatus.Assigned, ct);

        // Đăng ký còn hiệu lực, không phải số Agent online/offline.
        var agents = await db.Agents.CountAsync(
            a => a.RevokedAt == null && db.Assets.Any(asset => asset.Id == a.AssetId), ct);
        var profiles = await db.LocalProfiles.CountAsync(
            p => db.AppUsers.Any(user => user.Id == p.UserId)
                && db.Assets.Any(asset => asset.Id == p.AssetId), ct);

        return new(users, assets, assignedAssets, agents, profiles);
    }
}
