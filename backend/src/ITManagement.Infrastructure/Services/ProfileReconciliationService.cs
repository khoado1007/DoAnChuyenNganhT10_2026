using System.Text.Json;
using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Contracts.Simulation;
using ITManagement.Domain.Entities.Simulation;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

public sealed partial class ProfileOperationService
{
    // CommandJson stores control metadata only. Never store file content or credentials here.
    private sealed record ReconciliationAttempt(Guid Id, Guid ActorId, DateTime RequestedAt, int Version = 1);

    private static ReconciliationAttempt? ReadAttempt(ProfileOperation op)
    {
        if (string.IsNullOrEmpty(op.CommandJson)) return null;
        try
        {
            var attempt = JsonSerializer.Deserialize<ReconciliationAttempt>(op.CommandJson);
            return attempt is { Version: 1 } && attempt.Id != Guid.Empty && attempt.ActorId != Guid.Empty ? attempt : null;
        }
        catch (JsonException) { return null; }
    }

    public async Task ReconcileAsync(Guid profileId, CancellationToken ct)
    {
        if (current.Role != UserRole.Admin || current.UserId is not Guid actorId)
            throw Forbidden("Chỉ Quản trị viên mới có quyền yêu cầu đối soát.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var profile = await db.LocalProfiles
            .FromSqlInterpolated($"SELECT * FROM local_profiles WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {profileId}")
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("LocalProfile", profileId);
        var op = await db.ProfileOperations.SingleOrDefaultAsync(o => o.ProfileId == profileId && o.CompletedAt == null, ct)
            ?? throw new SimulationException(409, "NO_PENDING_OPERATION", "Profile không có thao tác cần đối soát.");
        var previous = ReadAttempt(op);
        if ((op.State == "Pending" && op.CreatedAt > Now.AddSeconds(-15)) ||
            (op.State == "Reconciling" && previous?.RequestedAt > Now.AddSeconds(-15)))
            throw new SimulationException(409, "OPERATION_IN_PROGRESS", "Đang chờ Agent; vui lòng thử lại sau ít nhất 15 giây.");
        var agent = await db.Agents.AsNoTracking().SingleOrDefaultAsync(a => a.Id == op.AgentId &&
            a.AssetId == profile.AssetId && a.DisabledAt == null && a.RevokedAt == null, ct)
            ?? throw new SimulationException(503, "AGENT_UNAVAILABLE", "Agent gốc chưa sẵn sàng để đối soát profile.");
        var attempt = new ReconciliationAttempt(Guid.NewGuid(), actorId, Now);
        op.CommandJson = JsonSerializer.Serialize(attempt);
        op.State = "Reconciling";
        db.AppAuditEvents.Add(new AppAuditEvent { ActorId = actorId, Action = "ProfileReconciliationRequested", ResourceId = profileId, CreatedAt = Now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var waiting = new TaskCompletionSource<AgentFileResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.RegisterWaiting(attempt.Id, waiting);
        dispatcher.Enqueue(agent.Id, new AgentFileCommand(op.Id, profile.AssetId, profile.UserId, profile.Id,
            "reconcile", null, null, null, null, null, null, profile.MaxFiles, Now.AddMinutes(5), true, attempt.Id));
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await waiting.Task.WaitAsync(deadline.Token);
            if (!result.Success)
                throw new SimulationException(502, "RECONCILIATION_FAILED", "Agent chưa đối soát được vùng lưu trữ. Quota đang giữ chỗ được giữ nguyên.");
        }
        catch (OperationCanceledException)
        {
            dispatcher.Cancel(attempt.Id);
            // Late acknowledgement may still safely complete this exact attempt.
            throw new SimulationException(504, "RECONCILIATION_TIMEOUT", "Chưa nhận kết quả đối soát. Quota vẫn được giữ chỗ; có thể thử lại sau 15 giây.");
        }
    }

    public async Task<IReadOnlyList<ProfileReconciliationEvent>> ReconciliationHistoryAsync(Guid profileId, CancellationToken ct)
    {
        if (current.Role != UserRole.Admin) throw Forbidden("Chỉ Quản trị viên được xem nhật ký đối soát.");
        if (!await db.LocalProfiles.AnyAsync(p => p.Id == profileId, ct)) throw new NotFoundException("LocalProfile", profileId);
        return await (from a in db.AppAuditEvents.AsNoTracking()
                      join u in db.AppUsers.AsNoTracking() on a.ActorId equals (Guid?)u.Id into actors
                      from u in actors.DefaultIfEmpty()
                      where a.ResourceId == profileId && a.Action.StartsWith("ProfileReconciliation")
                      orderby a.CreatedAt descending
                      select new ProfileReconciliationEvent(a.CreatedAt, a.Action, u == null ? null : u.Username))
                      .Take(20).ToListAsync(ct);
    }

    private async Task<bool> CanReconcileAsync(ProfileOperation op, Guid? attemptId, CancellationToken ct)
    {
        var attempt = ReadAttempt(op);
        return op.State == "Reconciling" && attemptId != null && attempt?.Id == attemptId &&
            attempt.RequestedAt.AddMinutes(5) > Now &&
            await db.AppUsers.AnyAsync(u => u.Id == attempt.ActorId && u.Status == UserStatus.Active && u.Role == UserRole.Admin, ct);
    }

    private async Task ApplyReconciliationAsync(ProfileOperation op, LocalProfile profile, AgentFileResult result,
        ReconciliationAttempt attempt, CancellationToken ct)
    {
        if (result.ContentBase64 != null || (result.Success && !IsValidInventory(result.Files)))
            throw new SimulationException(422, "INVALID_AGENT_INVENTORY", "Đối soát chỉ chấp nhận metadata hợp lệ, không nhận nội dung file.");
        if (result.Success)
        {
            await ReplaceInventoryAsync(profile, result.Files, ct);
            op.State = "Reconciled";
            op.ReservedFiles = 0;
            op.CompletedAt = Now;
            op.ErrorCode = "OPERATION_RECONCILED";
        }
        else
        {
            op.State = "Unknown";
            // Keep the original unresolved mutation and its quota reservation.
        }
        db.AppAuditEvents.Add(new AppAuditEvent { ActorId = attempt.ActorId, ResourceId = profile.Id,
            Action = result.Success ? "ProfileReconciliationCompleted" : "ProfileReconciliationFailed", CreatedAt = Now });
    }

    private async Task ReplaceInventoryAsync(LocalProfile profile, LocalFileDto[] files, CancellationToken ct)
    {
        profile.Ready = true;
        profile.UsedFiles = files.Count(f => f.Folder is "Desktop" or "Documents");
        db.ProfileFiles.RemoveRange(await db.ProfileFiles.Where(f => f.ProfileId == profile.Id).ToListAsync(ct));
        foreach (var file in files)
            db.ProfileFiles.Add(new ProfileFile { Id = file.FileId, ProfileId = profile.Id, Name = file.Name,
                Folder = file.Folder, Extension = file.Extension, SizeBytes = file.SizeBytes });
    }
}
