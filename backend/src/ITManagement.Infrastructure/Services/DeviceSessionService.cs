using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Contracts.Simulation;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Entities.Simulation;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

/// <summary>Grant độc lập với assignment; phiên mô phỏng không phải Windows logon.</summary>
public sealed class DeviceSessionService(AppDbContext db, ICurrentUserService current, TimeProvider clock)
    : IDeviceSessionService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static SimulationException Forbidden() => new(403, "DEVICE_ACCESS_DENIED", "Bạn không có phiên hoặc quyền sử dụng máy này.");
    private void RequireAdmin()
    {
        if (current.Role != UserRole.Admin) throw Forbidden();
    }
    private async Task<Asset> LockAsset(Guid id, CancellationToken ct) =>
        await db.Assets.FromSqlInterpolated($"SELECT * FROM assets WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}")
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Asset", id);

    public async Task<IReadOnlyList<MachineResponse>> MachinesAsync(CancellationToken ct) =>
        await (from asset in db.Assets.AsNoTracking()
               join agent in db.Agents.AsNoTracking() on asset.Id equals agent.AssetId
               where agent.RevokedAt == null && agent.DisabledAt == null
                   && (asset.Status == AssetStatus.Available || asset.Status == AssetStatus.Assigned)
               orderby asset.AssetCode
               select new MachineResponse(asset.Id, asset.AssetCode, asset.Name, agent.Mode)).ToListAsync(ct);

    public async Task<IReadOnlyList<DeviceAccessResponse>> AccessAsync(Guid assetId, CancellationToken ct)
    {
        RequireAdmin();
        if (!await db.Assets.AnyAsync(x => x.Id == assetId, ct)) throw new NotFoundException("Asset", assetId);
        return await (from grant in db.DeviceAccessGrants.AsNoTracking()
                      join user in db.AppUsers.AsNoTracking() on grant.UserId equals user.Id
                      where grant.AssetId == assetId && grant.RevokedAt == null
                      orderby user.Username
                      select new DeviceAccessResponse(user.Id, user.Username, user.FullName)).ToListAsync(ct);
    }

    public async Task SetAccessAsync(Guid assetId, Guid userId, bool grant, CancellationToken ct)
    {
        RequireAdmin();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var asset = await LockAsset(assetId, ct);
        var active = await db.DeviceAccessGrants.SingleOrDefaultAsync(x => x.AssetId == assetId && x.UserId == userId && x.RevokedAt == null, ct);
        if (grant)
        {
            var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {userId}").SingleOrDefaultAsync(ct);
            if (user is null || user.Role != UserRole.Employee || user.Status != UserStatus.Active
                || asset.Status is not (AssetStatus.Available or AssetStatus.Assigned)) throw Forbidden();
            if (active is null)
            {
                var added = new DeviceAccessGrant { AssetId = assetId, UserId = userId, GrantedAt = Now };
                db.DeviceAccessGrants.Add(added);
                Audit("DeviceAccessGranted", added.Id);
            }
        }
        else
        {
            if (active is not null) { active.RevokedAt = Now; Audit("DeviceAccessRevoked", active.Id); }
            // Kết thúc phiên nhưng giữ profile/file và operation chưa rõ kết quả.
            await db.DeviceSessions.Where(x => x.AssetId == assetId && x.UserId == userId && x.EndedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, Now), ct);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<DeviceSessionResponse> StartAsync(Guid assetId, CancellationToken ct)
    {
        if (current.Role != UserRole.Employee || current.UserId is not Guid userId) throw Forbidden();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Cùng lock order với grant/assignment: asset trước, user sau.
        var asset = await LockAsset(assetId, ct);
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {userId}").SingleOrDefaultAsync(ct);
        if (user is null || user.Status != UserStatus.Active || user.Role != UserRole.Employee
            || asset.Status is not (AssetStatus.Available or AssetStatus.Assigned)
            || !await db.DeviceAccessGrants.AnyAsync(x => x.AssetId == assetId && x.UserId == userId && x.RevokedAt == null, ct)) throw Forbidden();
        var existing = await db.DeviceSessions.SingleOrDefaultAsync(x => x.AssetId == assetId && x.EndedAt == null, ct);
        if (existing is not null)
        {
            var owner = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == existing.UserId, ct);
            if (existing.ExpiresAt > Now && owner is { Status: UserStatus.Active, Role: UserRole.Employee }
                && owner.SecurityVersion == existing.SecurityVersion
                && await db.DeviceAccessGrants.AnyAsync(x => x.AssetId == assetId && x.UserId == existing.UserId && x.RevokedAt == null, ct))
                throw new SimulationException(409, "DEVICE_SESSION_CONFLICT", "Máy đang có phiên mô phỏng hoạt động.");
            existing.EndedAt = Now;
            // Giải phóng partial unique index trước INSERT phiên mới.
            await db.SaveChangesAsync(ct);
        }
        var profile = await db.LocalProfiles.SingleOrDefaultAsync(x => x.AssetId == assetId && x.UserId == userId, ct);
        if (profile is null)
        {
            profile = new LocalProfile { AssetId = assetId, UserId = userId, CreatedAt = Now };
            db.LocalProfiles.Add(profile);
        }
        var session = new DeviceSession { AssetId = assetId, UserId = userId, ProfileId = profile.Id,
            SecurityVersion = user.SecurityVersion, ExpiresAt = Now.AddHours(8) };
        db.DeviceSessions.Add(session);
        Audit("DeviceSessionStarted", session.Id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(session.Id, asset.Id, profile.Id, "COMPANY\\" + user.Username, asset.Name, session.ExpiresAt);
    }

    public async Task EndAsync(Guid assetId, Guid sessionId, CancellationToken ct)
    {
        if (current.Role != UserRole.Employee || current.UserId is not Guid userId) throw Forbidden();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Tra cứu scoped trước để không tiết lộ sự tồn tại của máy khác.
        if (!await db.DeviceSessions.AnyAsync(x => x.Id == sessionId && x.AssetId == assetId && x.UserId == userId, ct)) throw Forbidden();
        await LockAsset(assetId, ct);
        var session = await db.DeviceSessions.SingleAsync(x => x.Id == sessionId, ct);
        if (session.EndedAt != null) throw Forbidden();
        session.EndedAt = Now;
        Audit("DeviceSessionEnded", session.Id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private void Audit(string action, Guid resourceId) => db.AppAuditEvents.Add(new AppAuditEvent
        { ActorId = current.UserId, Action = action, ResourceId = resourceId, CreatedAt = Now });
}
