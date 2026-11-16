using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Contracts.Simulation;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Entities.Simulation;
using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ITManagement.Infrastructure.Services;

/// <summary>
/// Quản lý hạn mức tệp (Quota Reservation), phiên Desktop/Documents mô phỏng và kênh giao tiếp Agent.
/// </summary>
public sealed partial class ProfileOperationService(
    AppDbContext db,
    ICurrentUserService current,
    IAgentOperationDispatcher dispatcher,
    TimeProvider clock, IConfiguration configuration) : IProfileOperationService
{
    private bool UseAgent => configuration.GetValue<bool>("Simulation:UseAgent");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    private static string RequestHash(FileActionRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private static SimulationException Forbidden(string msg = "Bạn không có quyền hoặc phiên sử dụng máy này.") =>
        new(403, "DEVICE_ACCESS_DENIED", msg);

    private async Task<(DeviceSession session, LocalProfile profile, AppUser user, Asset asset)> ValidateSessionAsync(
        Guid assetId, Guid sessionId, CancellationToken ct)
    {
        if (current.Role != UserRole.Employee || current.UserId is not Guid userId)
            throw Forbidden();

        var session = await db.DeviceSessions.SingleOrDefaultAsync(
            s => s.Id == sessionId && s.AssetId == assetId && s.UserId == userId && s.EndedAt == null, ct)
            ?? throw Forbidden("Phiên làm việc không tồn tại hoặc đã kết thúc.");

        if (session.ExpiresAt <= Now)
            throw Forbidden("Phiên làm việc đã hết hạn.");

        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw Forbidden();

        if (user.Status != UserStatus.Active || user.SecurityVersion != session.SecurityVersion)
            throw Forbidden("Tài khoản đã thay đổi trạng thái hoặc phiên bản bảo mật.");

        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == assetId, ct)
            ?? throw new NotFoundException("Asset", assetId);

        if (asset.Status is not (AssetStatus.Available or AssetStatus.Assigned))
            throw Forbidden("Thiết bị không khả dụng.");

        var hasGrant = await db.DeviceAccessGrants.AnyAsync(
            g => g.AssetId == assetId && g.UserId == userId && g.RevokedAt == null, ct);
        if (UseAgent && !hasGrant)
            throw Forbidden("Quyền truy cập thiết bị đã bị thu hồi.");

        var profile = await db.LocalProfiles.SingleOrDefaultAsync(
            p => p.Id == session.ProfileId, ct)
            ?? throw new NotFoundException("LocalProfile", session.ProfileId);

        if (profile.UserId != userId || profile.AssetId != assetId || profile.StorageBackend != (UseAgent ? "Agent" : "Server"))
            throw Forbidden("Vùng lưu trữ không thuộc phiên hiện tại hoặc cần chuyển dữ liệu Agent cũ.");
        if (!UseAgent && asset.Type is not (AssetType.Desktop or AssetType.Laptop or AssetType.Server)) throw Forbidden();

        return (session, profile, user, asset);
    }

    private async Task<ProfileSnapshot> BuildSnapshotAsync(
        DeviceSession session, LocalProfile profile, AppUser user, Asset asset, string? contentBase64, CancellationToken ct)
    {
        var files = await db.ProfileFiles.AsNoTracking()
            .Where(f => f.ProfileId == profile.Id)
            .OrderBy(f => f.Folder).ThenBy(f => f.Name)
            .Select(f => new LocalFileDto(f.Id, f.Name, f.Folder, f.SizeBytes, f.Extension))
            .ToListAsync(ct);

        var reservedCount = await db.ProfileOperations.AsNoTracking()
            .Where(o => o.ProfileId == profile.Id && o.ReservedFiles > 0 && o.CompletedAt == null)
            .SumAsync(o => o.ReservedFiles, ct);

        var usedFiles = profile.UsedFiles;
        var maxFiles = profile.MaxFiles;
        var remainingFiles = Math.Max(0, maxFiles - usedFiles - reservedCount);
        var overQuota = usedFiles > maxFiles;

        var quota = new QuotaSnapshot(maxFiles, usedFiles, reservedCount, remainingFiles, overQuota);
        return new ProfileSnapshot(session.Id, asset.Id, profile.Id, "COMPANY\\" + user.Username, asset.Name, files, quota, contentBase64);
    }

    public async Task<ProfileSnapshot> GetProfileSnapshotAsync(Guid assetId, Guid sessionId, CancellationToken ct)
    {
        var (session, profile, user, asset) = await ValidateSessionAsync(assetId, sessionId, ct);
        return await BuildSnapshotAsync(session, profile, user, asset, null, ct);
    }

    public async Task<ProfileSnapshot> ExecuteOperationAsync(
        Guid assetId, Guid sessionId, FileActionRequest request, CancellationToken ct)
    {
        if (!UseAgent) return await ExecuteServerOperationAsync(assetId, sessionId, request, ct);
        var (session, profile, user, asset) = await ValidateSessionAsync(assetId, sessionId, ct);
        if (request.Kind == "reconcile")
            throw Forbidden("Đối soát phải được Quản trị viên yêu cầu qua luồng riêng.");
        if (request.RequestId == Guid.Empty)
            throw new SimulationException(400, "INVALID_REQUEST_ID", "Request ID phải là GUID hợp lệ.");
        var requestHash = RequestHash(request);

        var isAdding = request.Kind is "create" or "copy" or "restore";
        var delta = isAdding ? 1 : 0;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Lock profile row để bảo vệ Quota Reservation trước các request đồng thời.
        var lockedProfile = await db.LocalProfiles
            .FromSqlInterpolated($"SELECT * FROM local_profiles WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {profile.Id}")
            .SingleAsync(ct);

        var existingOp = await db.ProfileOperations.SingleOrDefaultAsync(
            o => o.ProfileId == profile.Id && o.RequestId == request.RequestId, ct);
        if (existingOp is not null)
        {
            if (string.IsNullOrEmpty(existingOp.RequestHash) ||
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(existingOp.RequestHash), Convert.FromHexString(requestHash)))
                throw new SimulationException(409, "IDEMPOTENCY_KEY_REUSED", "Request ID đã được dùng cho nội dung thao tác khác.");

            if (existingOp.State == "Completed")
            {
                if (existingOp.Kind == "read")
                    throw new SimulationException(409, "READ_RESULT_EXPIRED", "Kết quả đọc không còn trong bộ nhớ. Hãy mở file lại.");
                await tx.CommitAsync(ct);
                return await BuildSnapshotAsync(session, lockedProfile, user, asset, null, ct);
            }
            if (existingOp.State == "Reconciled")
                throw new SimulationException(409, "OPERATION_RECONCILED", "Đã đối soát vùng lưu trữ. Kiểm tra danh sách file trước khi tạo thao tác mới.");
            if (existingOp.State is "Pending" or "Unknown" or "Reconciling")
                throw new SimulationException(409, "OPERATION_IN_PROGRESS", "Thao tác trước đó đang chờ Agent xác nhận; hãy thử lại bằng cùng Request ID.");
            if (existingOp.State == "Failed")
                throw FileOperationError(existingOp.ErrorCode);
        }

        var currentReserved = await db.ProfileOperations
            .Where(o => o.ProfileId == profile.Id && o.ReservedFiles > 0 && o.CompletedAt == null)
            .SumAsync(o => o.ReservedFiles, ct);

        if (isAdding && (lockedProfile.UsedFiles + currentReserved >= lockedProfile.MaxFiles))
        {
            throw new SimulationException(409, "FILE_COUNT_QUOTA_EXCEEDED", "Đã đạt giới hạn số lượng tệp (Quota).");
        }

        // Một profile chỉ có một thao tác chưa xác nhận (kể cả list/read).
        // Kiểm tra dưới profile lock để trả lỗi nghiệp vụ thay vì vi phạm unique index.
        if (await db.ProfileOperations.AnyAsync(o => o.ProfileId == profile.Id && o.CompletedAt == null, ct))
            throw new SimulationException(409, "OPERATION_IN_PROGRESS",
                "Thao tác trước đó chưa được Agent xác nhận. Vui lòng kiểm tra trạng thái trước khi thao tác tiếp.");

        var agent = await db.Agents.AsNoTracking()
            .SingleOrDefaultAsync(a => a.AssetId == assetId && a.RevokedAt == null && a.DisabledAt == null, ct)
            ?? throw new SimulationException(503, "AGENT_UNAVAILABLE", "Agent trên thiết bị chưa sẵn sàng.");

        var op = new ProfileOperation
        {
            Id = Guid.NewGuid(),
            RequestId = request.RequestId,
            ProfileId = profile.Id,
            SessionId = session.Id,
            AgentId = agent.Id,
            Kind = request.Kind,
            State = "Pending",
            RequestHash = requestHash,
            ReservedFiles = delta,
            ExpiresAt = Now.AddMinutes(5),
            CreatedAt = Now
        };
        db.ProfileOperations.Add(op);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var command = new AgentFileCommand(
            op.Id, asset.Id, user.Id, profile.Id,
            request.Kind, request.Folder, request.Name, request.NewName, request.TargetFolder,
            request.FileId, request.ContentBase64, lockedProfile.MaxFiles, op.ExpiresAt);

        var tcs = new TaskCompletionSource<AgentFileResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.RegisterWaiting(op.Id, tcs);
        dispatcher.Enqueue(agent.Id, command);

        AgentFileResult result;
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            result = await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            dispatcher.Cancel(op.Id);
            // Giữ trạng thái Unknown và giữ reservation theo chính sách Quota/Storage.
            await db.ProfileOperations.Where(o => o.Id == op.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Unknown"), CancellationToken.None);
            throw new SimulationException(504, "AGENT_TIMEOUT", "Agent không phản hồi kịp thời. Thao tác ở trạng thái chưa rõ kết quả.");
        }

        if (!result.Success)
            throw FileOperationError(result.ErrorCode);

        // Tải lại snapshot với dữ liệu cập nhật sau khi Agent hoàn tất.
        var reloadedProfile = await db.LocalProfiles.AsNoTracking().SingleAsync(p => p.Id == profile.Id, ct);
        return await BuildSnapshotAsync(session, reloadedProfile, user, asset, result.ContentBase64, ct);
    }

    private static SimulationException FileOperationError(string? errorCode) => errorCode switch
    {
        "FILE_OPERATION_UNKNOWN" => new(504, errorCode, "Kết quả trên Agent chưa rõ; Quota Reservation vẫn được giữ để đối soát."),
        "FILE_COUNT_QUOTA_EXCEEDED" => new(409, errorCode, "Quota file đã đạt giới hạn; không có file mới được tạo."),
        "INVALID_RELATIVE_PATH" => new(422, errorCode, "Đường dẫn hoặc tên file không hợp lệ."),
        _ => new(400, errorCode ?? "FILE_OPERATION_FAILED", "Thao tác trên Agent thất bại.")
    };

    public async Task<IReadOnlyList<ProfileSummaryResponse>> ListProfilesAsync(CancellationToken ct)
    {
        if (current.Role != UserRole.Admin)
            throw Forbidden("Chỉ Quản trị viên mới có quyền xem danh sách Profile.");

        return await (from p in db.LocalProfiles.AsNoTracking()
                      join u in db.AppUsers.AsNoTracking() on p.UserId equals u.Id
                      join a in db.Assets.AsNoTracking() on p.AssetId equals a.Id
                      orderby p.CreatedAt descending
                      select new ProfileSummaryResponse(
                          p.Id, u.Id, u.Username, u.FullName, a.Id, a.Name, a.AssetCode,
                          p.MaxFiles, p.UsedFiles, p.Ready, p.CreatedAt,
                          db.ProfileOperations.Where(o => o.ProfileId == p.Id && o.CompletedAt == null).Select(o => o.State).FirstOrDefault(),
                          db.ProfileOperations.Where(o => o.ProfileId == p.Id && o.CompletedAt == null).Sum(o => o.ReservedFiles), p.StorageBackend)).ToListAsync(ct);
    }

    public async Task SetProfileQuotaAsync(Guid profileId, int maxFiles, CancellationToken ct)
    {
        if (current.Role != UserRole.Admin)
            throw Forbidden("Chỉ Quản trị viên mới có quyền điều chỉnh Quota.");

        if (maxFiles < 0)
            throw new SimulationException(400, "INVALID_QUOTA", "Hạn mức tệp không được âm.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var profile = await db.LocalProfiles
            .FromSqlInterpolated($"SELECT * FROM local_profiles WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {profileId}")
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("LocalProfile", profileId);

        profile.MaxFiles = maxFiles;
        db.AppAuditEvents.Add(new AppAuditEvent
        {
            ActorId = current.UserId,
            Action = "ProfileQuotaUpdated",
            ResourceId = profile.Id,
            CreatedAt = Now
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<AgentPollResponse> PollOperationAsync(string agentCredential, CancellationToken ct)
    {
        var agent = await AuthenticateAgentAsync(agentCredential, ct);
        var command = dispatcher.Dequeue(agent.Id);
        if (command is not null)
        {
            await db.ProfileOperations.Where(o => o.Id == command.OperationId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.DispatchedAt, Now), ct);
        }
        return new AgentPollResponse(command);
    }

    public async Task<AgentAuthorizeResponse> AuthorizeOperationAsync(string agentCredential, Guid operationId, CancellationToken ct, Guid? reconciliationId = null)
    {
        var agent = await AuthenticateAgentAsync(agentCredential, ct);
        var op = await db.ProfileOperations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == operationId, ct);
        if (op is null || op.AgentId != agent.Id || op.CompletedAt != null)
            return new AgentAuthorizeResponse(false, "OPERATION_NOT_FOUND");
        if (ReadAttempt(op) != null || reconciliationId != null)
            return new AgentAuthorizeResponse(await CanReconcileAsync(op, reconciliationId, ct), "RECONCILIATION_AUTHORIZATION");

        var session = await db.DeviceSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == op.SessionId, ct);
        if (session is null || session.EndedAt != null || session.ExpiresAt <= Now)
            return new AgentAuthorizeResponse(false, "SESSION_EXPIRED");

        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == session.UserId, ct);
        if (user is null || user.Status != UserStatus.Active || user.SecurityVersion != session.SecurityVersion)
            return new AgentAuthorizeResponse(false, "USER_INVALID");

        var hasGrant = await db.DeviceAccessGrants.AnyAsync(
            g => g.AssetId == session.AssetId && g.UserId == session.UserId && g.RevokedAt == null, ct);
        if (!hasGrant)
            return new AgentAuthorizeResponse(false, "DEVICE_ACCESS_DENIED");

        return new AgentAuthorizeResponse(true);
    }

    public async Task HandleAgentResultAsync(string agentCredential, Guid operationId, AgentFileResult result, CancellationToken ct)
    {
        var agent = await AuthenticateAgentAsync(agentCredential, ct);

        var known = await db.ProfileOperations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == operationId, ct);
        if (known is null || known.AgentId != agent.Id) throw new NotFoundException("ProfileOperation", operationId);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Same lock order as enqueue/reconcile: profile before operation.
        var profile = await db.LocalProfiles
            .FromSqlInterpolated($"SELECT * FROM local_profiles WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {known.ProfileId}")
            .SingleAsync(ct);
        var op = await db.ProfileOperations
            .FromSqlInterpolated($"SELECT * FROM profile_operations WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {operationId}")
            .SingleOrDefaultAsync(ct);

        if (op is null || op.AgentId != agent.Id)
            throw new NotFoundException("ProfileOperation", operationId);

        if (result.ReconciliationId is Guid attemptId)
        {
            var attempt = ReadAttempt(op);
            if (op.CompletedAt != null || op.State != "Reconciling" || attempt?.Id != attemptId) return;
            await ApplyReconciliationAsync(op, profile, result, attempt, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            dispatcher.TryComplete(attemptId, result);
            return;
        }
        if (ReadAttempt(op) != null || op.State is "Reconciling" or "Reconciled") return;
        if (op.CompletedAt is not null)
        {
            // Idempotent completion acknowledgement
            dispatcher.TryComplete(operationId, result);
            return;
        }

        if (result.Success)
        {
            if (!IsValidInventory(result.Files))
                throw new SimulationException(422, "INVALID_AGENT_INVENTORY", "Agent trả về metadata file không hợp lệ.");

            profile.Ready = true;
            profile.UsedFiles = result.Files.Count(file => file.Folder is "Desktop" or "Documents");
            op.ReservedFiles = 0;
            op.State = "Completed";
            op.CompletedAt = Now;

            // Đồng bộ danh sách ProfileFile trong cơ sở dữ liệu
            var existingFiles = await db.ProfileFiles.Where(f => f.ProfileId == profile.Id).ToListAsync(ct);
            db.ProfileFiles.RemoveRange(existingFiles);

            foreach (var f in result.Files)
            {
                db.ProfileFiles.Add(new ProfileFile
                {
                    Id = f.FileId != Guid.Empty ? f.FileId : Guid.NewGuid(),
                    ProfileId = profile.Id,
                    Name = f.Name,
                    Folder = f.Folder,
                    Extension = f.Extension,
                    SizeBytes = f.SizeBytes
                });
            }

            db.AppAuditEvents.Add(new AppAuditEvent
            {
                ActorId = null,
                Action = "ProfileOperationCompleted",
                ResourceId = op.Id,
                CreatedAt = Now
            });
        }
        else
        {
            if (result.ErrorCode == "FILE_OPERATION_UNKNOWN")
            {
                // Giữ reservation và đánh dấu Unknown để đối soát sau
                op.State = "Unknown";
            }
            else
            {
                op.ReservedFiles = 0;
                op.State = "Failed";
                op.ErrorCode = result.ErrorCode;
                op.CompletedAt = Now;
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        dispatcher.TryComplete(operationId, result);
    }

    private async Task<Domain.Entities.Agents.RegisteredAgent> AuthenticateAgentAsync(string credential, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credential) || credential.Length != 64)
            throw new AuthenticationRequiredException("Credential Agent không hợp lệ.");

        var hash = Hash(credential);
        var agent = await db.Agents.AsNoTracking().SingleOrDefaultAsync(
            a => a.CredentialHash == hash && a.RevokedAt == null && a.DisabledAt == null, ct);

        if (agent is null)
            throw new AuthenticationRequiredException("Agent không được cấp quyền hoặc đã bị vô hiệu hóa.");

        return agent;
    }

    private static bool IsValidInventory(LocalFileDto[]? files)
    {
        if (files is null || files.Length > 10_000)
            return false;

        var ids = new HashSet<Guid>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file is null || file.FileId == Guid.Empty || file.SizeBytes < 0 ||
                file.Folder is not ("Desktop" or "Documents" or "RecycleBin") ||
                string.IsNullOrWhiteSpace(file.Name) || file.Name.Length > 512 ||
                file.Extension is null || file.Extension.Length > 32 ||
                file.Name.StartsWith('/') || file.Name.Contains('\\') || file.Name.Contains(':') ||
                file.Name.Any(char.IsControl) ||
                file.Name.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ')) ||
                file.Name.Any(character => "<>\"|?*".Contains(character)) ||
                !string.Equals(file.Extension, Path.GetExtension(file.Name).ToLowerInvariant(), StringComparison.Ordinal))
                return false;

            if (!ids.Add(file.FileId) || !paths.Add(file.Folder + "/" + file.Name))
                return false;
        }

        return true;
    }
}
