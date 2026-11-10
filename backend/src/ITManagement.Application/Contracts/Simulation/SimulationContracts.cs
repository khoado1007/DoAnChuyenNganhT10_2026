using System.ComponentModel.DataAnnotations;

namespace ITManagement.Application.Contracts.Simulation;

public sealed record MachineResponse(Guid Id, string AssetCode, string Name, string Mode);
public sealed record DeviceAccessResponse(Guid UserId, string Username, string FullName);
public sealed record DeviceSessionResponse(Guid SessionId, Guid DeviceId, Guid ProfileId,
    string DomainId, string DeviceName, DateTime ExpiresAt);

public sealed record FileActionRequest(
    [Required, StringLength(24)] string Kind,
    Guid RequestId,
    string? Folder = null,
    string? Name = null,
    string? NewName = null,
    string? TargetFolder = null,
    Guid? FileId = null,
    string? ContentBase64 = null);

public sealed record LocalFileDto(Guid FileId, string Name, string Folder, long SizeBytes, string Extension);
public sealed record AgentFileResult(bool Success, string? ErrorCode, LocalFileDto[] Files, string? ContentBase64 = null);
public sealed record AgentFileCommand(Guid OperationId, Guid DeviceId, Guid UserId, Guid ProfileId,
    string Kind, string? Folder, string? Name, string? NewName, string? TargetFolder, Guid? FileId,
    string? ContentBase64, int MaxFiles, DateTime ExpiresAt, bool RecoveryOnly = false);
public sealed record QuotaSnapshot(int MaxFiles, int UsedFiles, int ReservedFiles, int RemainingFiles, bool OverQuota);
public sealed record ProfileSnapshot(Guid SessionId, Guid DeviceId, Guid ProfileId, string DomainId,
    string DeviceName, IReadOnlyList<LocalFileDto> Files, QuotaSnapshot Quota, string? ContentBase64 = null);
public sealed record SetProfileQuotaRequest([Range(0, 10000)] int MaxFiles);
public sealed record ProfileSummaryResponse(Guid ProfileId, Guid UserId, string Username, string FullName,
    Guid DeviceId, string DeviceName, string AssetCode, int MaxFiles, int UsedFiles, bool Ready, DateTime CreatedAt);
public sealed record AgentPollResponse(AgentFileCommand? Operation);
public sealed record AgentAuthorizeResponse(bool Allowed, string? Reason = null);
