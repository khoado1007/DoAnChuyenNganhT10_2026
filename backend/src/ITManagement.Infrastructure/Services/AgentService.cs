using System.Security.Cryptography;
using System.Text;
using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Contracts.Agents;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Agents;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ đăng ký, xác thực và quản lý vòng đời agent.</summary>
public sealed class AgentService(AppDbContext db, TimeProvider clock) : IAgentService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    // Tạo bí mật ngẫu nhiên dùng làm token credential cho agent.
    private static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    // Băm bí mật trước khi lưu hoặc so sánh credential của agent.
    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    // Tạo lỗi xác thực thống nhất cho token hoặc credential agent không hợp lệ.
    private static AuthenticationRequiredException Invalid() => new("Thông tin đăng ký hoặc credential Agent không hợp lệ.");

    // Khóa đồng bộ thao tác cập nhật trạng thái tài sản của agent.
    private async Task<Asset> LockAsset(Guid id, CancellationToken ct)
    {
        var asset = await db.Assets.FromSqlInterpolated($"SELECT * FROM assets WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}").SingleOrDefaultAsync(ct);
        if (asset is null) throw new NotFoundException("Asset", id);
        if (asset.Status is AssetStatus.Disabled or AssetStatus.Retired)
            throw new UnauthorizedException("Tài sản đã vô hiệu hóa hoặc ngừng sử dụng.");
        return asset;
    }

    /// <summary>Tạo token đăng ký agent mới và lưu bản băm token.</summary>
    public async Task<EnrollmentIssued> IssueAsync(IssueEnrollmentRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsset(request.AssetId, ct);
        var activeAgent = await db.Agents.SingleOrDefaultAsync(a => a.AssetId == request.AssetId && a.RevokedAt == null, ct);
        if (activeAgent is not null)
        {
            if (!request.ReplaceExisting)
                throw new BusinessRuleException("Tài sản đã có Agent; không tự động thay credential.");

            var nowUtc = Now;
            activeAgent.RevokedAt = nowUtc;
            activeAgent.RevokedReason = string.IsNullOrWhiteSpace(request.Reason) ? "Thay thế bởi quản trị viên" : request.Reason;
            if (activeAgent.DisabledAt is null) activeAgent.DisabledAt = nowUtc;
        }

        var now = Now;
        await db.EnrollmentTokens.Where(t => t.AssetId == request.AssetId && t.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, now), ct);
        var secret = Secret();
        var token = new EnrollmentToken { AssetId = request.AssetId, Mode = request.Mode, TokenHash = Hash(secret), ExpiresAt = now.AddMinutes(15) };
        db.EnrollmentTokens.Add(token);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(secret, token.ExpiresAt);
    }

    /// <summary>Tạo token khôi phục và vô hiệu hóa thông tin agent cũ.</summary>
    public Task<EnrollmentIssued> IssueRecoveryAsync(IssueRecoveryRequest request, CancellationToken ct)
        => IssueAsync(new IssueEnrollmentRequest(request.AssetId, request.Mode, ReplaceExisting: true, Reason: request.Reason), ct);

    /// <summary>Xác thực token đăng ký và tạo bản ghi agent.</summary>
    public async Task<AgentEnrolled> EnrollAsync(EnrollRequest request, CancellationToken ct)
    {
        if (request.InstanceId == Guid.Empty) throw new BusinessRuleException("Instance ID không hợp lệ.");
        var hash = Hash(request.Token);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var token = await db.EnrollmentTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) throw Invalid();
        await LockAsset(token.AssetId, ct);
        var now = Now;
        var consumed = await db.EnrollmentTokens.Where(t => t.Id == token.Id && t.ConsumedAt == null && t.ExpiresAt > now && t.Mode == request.Mode)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), ct);
        if (consumed != 1) throw Invalid();
        if (await db.Agents.AnyAsync(a => (a.AssetId == token.AssetId && a.RevokedAt == null) || (a.InstanceId == request.InstanceId && a.RevokedAt == null), ct))
            throw new BusinessRuleException("Tài sản hoặc instance đã có Agent đang hoạt động.");
        var credential = Secret();
        var agent = new RegisteredAgent { AssetId = token.AssetId, InstanceId = request.InstanceId, Mode = token.Mode, CredentialHash = Hash(credential), CreatedAt = now };
        db.Agents.Add(agent);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BusinessRuleException("Tài sản hoặc instance đã được đăng ký."); }
        await tx.CommitAsync(ct);
        return new(agent.Id, credential);
    }

    /// <summary>Xác thực agent và cập nhật trạng thái cùng dữ liệu kiểm kê.</summary>
    public async Task<HeartbeatAccepted> HeartbeatAsync(string credential, HeartbeatRequest request, CancellationToken ct)
    {
        if (credential.Length != 64) throw Invalid();
        var hash = Hash(credential);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var agent = await db.Agents.FromSqlInterpolated($"SELECT * FROM agents WITH (UPDLOCK, ROWLOCK) WHERE \"CredentialHash\" = {hash}").SingleOrDefaultAsync(ct);
        if (agent is null) throw Invalid();
        if (agent.RevokedAt is not null) throw new UnauthorizedException("Agent đã bị thu hồi hoặc thay thế.");
        if (agent.DisabledAt is not null) throw new UnauthorizedException("Agent đã bị vô hiệu hóa.");
        if (!await db.Assets.AnyAsync(a => a.Id == agent.AssetId && a.Status != AssetStatus.Disabled && a.Status != AssetStatus.Retired, ct))
            throw new UnauthorizedException("Tài sản không cho phép heartbeat.");
        // Replays do not advance last-seen or overwrite a newer inventory snapshot.
        if (request.Sequence > agent.LastSequence)
        {
            agent.LastSequence = request.Sequence;
            agent.LastSeenAt = Now;
            agent.Hostname = request.Hostname;
            agent.Os = request.Os;
            agent.Cpu = request.Cpu;
            agent.RamBytes = request.RamBytes;
            agent.AgentVersion = request.AgentVersion;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new(agent.LastSeenAt, agent.LastSequence);
    }

    /// <summary>Liệt kê các agent đã đăng ký.</summary>
    public async Task<IReadOnlyList<AgentResponse>> ListAsync(CancellationToken ct)
    {
        var threshold = Now.AddSeconds(-90);
        return await (from a in db.Agents.AsNoTracking()
                      join asset in db.Assets.IgnoreQueryFilters() on a.AssetId equals asset.Id
                      orderby a.CreatedAt descending
                      select new AgentResponse(a.Id, a.AssetId, a.InstanceId, asset.AssetCode, asset.Status.ToString(), a.Mode,
                          a.RevokedAt != null ? "revoked" : (a.DisabledAt == null ? "active" : "disabled"),
                          a.RevokedAt == null && a.LastSeenAt != null && a.LastSeenAt > threshold ? "online" : "offline", a.LastSeenAt,
                          a.Hostname, a.Os, a.Cpu, a.RamBytes, a.AgentVersion,
                          a.RevokedAt, a.RevokedReason)).ToListAsync(ct);
    }

    /// <summary>Vô hiệu hóa agent và chấm dứt credential hiện hành.</summary>
    public async Task DisableAsync(Guid id, CancellationToken ct)
    {
        var now = Now;
        // UPDATE takes the same row lock as heartbeat; no heartbeat can commit after disable completes.
        var count = await db.Agents.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.DisabledAt, a => a.DisabledAt ?? now), ct);
        if (count == 0) throw new NotFoundException("Agent", id);
    }
}
