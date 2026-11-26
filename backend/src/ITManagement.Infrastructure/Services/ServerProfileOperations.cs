using ITManagement.Application.Contracts.Simulation;
using ITManagement.Domain.Entities.Simulation;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

public sealed partial class ProfileOperationService
{
    private static SimulationException InvalidFile(string message) => new(422, "INVALID_FILE_OPERATION", message);
    private static string FileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name != name.Trim() || name.EndsWith('.')
            || name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)))
            throw InvalidFile("Tên file không hợp lệ; không nhập đường dẫn.");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
            throw InvalidFile("Tên file được hệ điều hành dành riêng.");
        if (!new[] { ".txt", ".csv", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" }.Contains(Path.GetExtension(name).ToLowerInvariant())
            && !name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            throw InvalidFile("Chỉ nhận tài liệu Office, PDF, TXT, CSV hoặc desktop.ini.");
        return name;
    }
    private static string ActiveFolder(string? folder) => folder is "Desktop" or "Documents" ? folder : throw InvalidFile("Chỉ dùng Desktop hoặc Documents.");
    private static byte[] Decode(string? value)
    {
        if (value is null) return [];
        if (value.Length > ((ServerProfileStorage.MaxBytes + 2) / 3) * 4) throw new SimulationException(413, "FILE_TOO_LARGE", "File tối đa 8 MiB.");
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length > ServerProfileStorage.MaxBytes) throw new SimulationException(413, "FILE_TOO_LARGE", "File tối đa 8 MiB.");
            return bytes;
        }
        catch (FormatException) { throw InvalidFile("Nội dung Base64 không hợp lệ."); }
    }

    private async Task<ProfileSnapshot> ExecuteServerOperationAsync(Guid assetId, Guid sessionId, FileActionRequest request, CancellationToken ct)
    {
        if (current.Role != UserRole.Employee || current.UserId is not Guid userId) throw Forbidden();
        if (request.RequestId == Guid.Empty) throw InvalidFile("Thiếu mã thao tác.");
        if (!new[] { "ensure", "list", "create", "read", "write", "rename", "move", "copy", "delete", "restore" }.Contains(request.Kind))
            throw InvalidFile("Thao tác không được hỗ trợ.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Same order as session login: asset -> user -> profile. Revocation cannot race a committed file operation.
        await db.Assets.FromSqlInterpolated($"SELECT * FROM assets WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {assetId}").AsNoTracking().SingleOrDefaultAsync(ct);
        await LockQuotaOwnerAsync(userId, ct);
        var (session, profile, user, asset) = await ValidateSessionAsync(assetId, sessionId, ct);
        await db.LocalProfiles.FromSqlInterpolated($"SELECT * FROM local_profiles WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {profile.Id}").SingleAsync(ct);
        await db.Entry(profile).ReloadAsync(ct);
        var hash = RequestHash(request);
        var replay = await db.ProfileOperations.AsNoTracking().SingleOrDefaultAsync(o => o.ProfileId == profile.Id && o.RequestId == request.RequestId, ct);
        if (replay != null)
        {
            if (replay.RequestHash != hash) throw new SimulationException(409, "IDEMPOTENCY_KEY_REUSED", "Mã thao tác đã dùng cho nội dung khác.");
            if (replay.State != "Completed") throw new SimulationException(409, "OPERATION_IN_PROGRESS", "Thao tác cũ chưa hoàn tất.");
            if (request.Kind == "read") throw new SimulationException(409, "READ_RESULT_EXPIRED", "Hãy mở lại file bằng yêu cầu mới.");
            var snapshot = await BuildSnapshotAsync(session, profile, user, asset, null, ct);
            await tx.CommitAsync(ct); return snapshot;
        }
        if (await db.ProfileOperations.AnyAsync(o => o.ProfileId == profile.Id && o.CompletedAt == null, ct))
            throw new SimulationException(409, "OPERATION_IN_PROGRESS", "Có thao tác lưu trữ cũ chưa được xác nhận.");
        var files = await db.ProfileFiles.Where(f => f.ProfileId == profile.Id).ToListAsync(ct);
        if (request.Kind is "create" or "copy" or "restore" && (await AccountQuotaAsync(userId, ct)).RemainingFiles == 0)
            throw new SimulationException(409, "FILE_COUNT_QUOTA_EXCEEDED", "Đã đạt giới hạn số file.");
        var storage = new ServerProfileStorage(configuration);
        string? content = null;
        ProfileFile? file = null;
        if (request.Kind is not ("ensure" or "list" or "create"))
        {
            file = files.SingleOrDefault(f => f.Id == request.FileId) ?? throw new SimulationException(404, "FILE_NOT_FOUND", "Không tìm thấy file trong profile hiện tại.");
            if ((request.Kind == "restore") != (file.Folder == "RecycleBin")) throw InvalidFile("Trạng thái file không phù hợp thao tác.");
        }
        void Unique(string folder, string name, Guid? except = null)
        {
            if (files.Any(f => f.Id != except && f.Folder == folder && f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new SimulationException(409, "FILE_EXISTS", "Tên file đã tồn tại trong thư mục.");
        }
        switch (request.Kind)
        {
            case "ensure": case "list": break;
            case "create":
            {
                var folder = ActiveFolder(request.Folder); var name = FileName(request.Name); Unique(folder, name);
                var bytes = Decode(request.ContentBase64);
                var key = await storage.WriteAsync(assetId, userId, profile.Id, bytes, ct);
                file = new ProfileFile { Id = Guid.NewGuid(), ProfileId = profile.Id, Name = name, Folder = folder, Extension = Path.GetExtension(name).ToLowerInvariant(), SizeBytes = bytes.Length, StorageKey = key };
                db.ProfileFiles.Add(file); files.Add(file); break;
            }
            case "read":
                content = Convert.ToBase64String(await storage.ReadAsync(assetId, userId, profile.Id, file!.StorageKey, ct)); break;
            case "write":
            {
                if (file!.Extension is not (".txt" or ".csv")) throw InvalidFile("Chỉ sửa nội dung TXT/CSV trực tiếp.");
                var bytes = Decode(request.ContentBase64);
                file.StorageKey = await storage.WriteAsync(assetId, userId, profile.Id, bytes, ct); file.SizeBytes = bytes.Length; break;
            }
            case "rename":
            {
                var name = FileName(request.NewName); Unique(file!.Folder, name, file.Id);
                file.Name = name; file.Extension = Path.GetExtension(name).ToLowerInvariant(); break;
            }
            case "move": case "restore":
            {
                var folder = ActiveFolder(request.TargetFolder); Unique(folder, file!.Name, file.Id); file.Folder = folder; break;
            }
            case "copy":
            {
                var folder = ActiveFolder(request.TargetFolder); var name = FileName(request.NewName ?? file!.Name); Unique(folder, name);
                // Independent metadata may share immutable bytes; later writes create a new blob.
                await storage.ReadAsync(assetId, userId, profile.Id, file!.StorageKey, ct);
                var copy = new ProfileFile { Id = Guid.NewGuid(), ProfileId = profile.Id, Name = name, Folder = folder, Extension = Path.GetExtension(name).ToLowerInvariant(), SizeBytes = file.SizeBytes, StorageKey = file.StorageKey };
                db.ProfileFiles.Add(copy); files.Add(copy); file = copy; break;
            }
            case "delete":
                // Distinguish equal names from different original folders in the recycle bin.
                if (files.Any(f => f.Id != file!.Id && f.Folder == "RecycleBin" && f.Name.Equals(file.Name, StringComparison.OrdinalIgnoreCase)))
                    file!.Name = $"{Path.GetFileNameWithoutExtension(file.Name)}-{file.Id:N}{file.Extension}";
                file!.Folder = "RecycleBin"; break;
        }
        profile.Ready = true;
        profile.UsedFiles = files.Count(f => f.Folder is "Desktop" or "Documents");
        var op = new ProfileOperation { Id = Guid.NewGuid(), RequestId = request.RequestId, ProfileId = profile.Id, SessionId = session.Id,
            AgentId = Guid.Empty, Kind = request.Kind, FileName = request.Kind == "read" ? null : file?.Name,
            State = "Completed", RequestHash = hash, CreatedAt = Now, ExpiresAt = Now, CompletedAt = Now };
        db.ProfileOperations.Add(op);
        if (request.Kind is not ("list" or "read" or "ensure"))
            db.AppAuditEvents.Add(new AppAuditEvent { ActorId = userId, Action = AuditAction(request.Kind), ResourceId = op.Id, CreatedAt = Now });
        await db.SaveChangesAsync(ct);
        var result = await BuildSnapshotAsync(session, profile, user, asset, content, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    /// <summary>Đặt tên sự kiện nhật ký thống nhất PascalCase để Admin lọc và hiển thị nhãn tiếng Việt.</summary>
    private static string AuditAction(string kind) => kind switch
    {
        "create" => "ServerFileCreate",
        "write" => "ServerFileWrite",
        "rename" => "ServerFileRename",
        "move" => "ServerFileMove",
        "copy" => "ServerFileCopy",
        "delete" => "ServerFileDelete",
        "restore" => "ServerFileRestore",
        _ => "ServerFile" + kind
    };
}
