using System.ComponentModel.DataAnnotations;

namespace ITManagement.Application.Contracts.Agents;

public sealed record IssueEnrollmentRequest(
    Guid AssetId,
    [Required, RegularExpression("^(real|simulated)$")] string Mode,
    bool ReplaceExisting = false,
    [StringLength(200)] string? Reason = null);
public sealed record IssueRecoveryRequest(
    Guid AssetId,
    [Required, RegularExpression("^(real|simulated)$")] string Mode,
    [StringLength(200)] string? Reason = null);
public sealed record EnrollRequest(
    [Required, StringLength(64, MinimumLength = 64)] string Token,
    Guid InstanceId,
    [Required, RegularExpression("^(real|simulated)$")] string Mode,
    [Range(1, 1)] int ProtocolVersion);
public sealed record HeartbeatRequest(
    [Range(1, long.MaxValue)] long Sequence,
    [Range(1, 1)] int ProtocolVersion,
    [Required, StringLength(200)] string Hostname,
    [Required, StringLength(200)] string Os,
    [Required, StringLength(200)] string Cpu,
    [Range(1, long.MaxValue)] long RamBytes,
    [Required, StringLength(40)] string AgentVersion);
public sealed record EnrollmentIssued(string Token, DateTime ExpiresAt);
public sealed record AgentEnrolled(Guid AgentId, string Credential, int HeartbeatIntervalSeconds = 30);
public sealed record HeartbeatAccepted(DateTime? LastSeenAt, long LastSequence, int HeartbeatIntervalSeconds = 30);
public sealed record AgentResponse(Guid Id, Guid AssetId, Guid InstanceId, string AssetCode, string AssetStatus,
    string Mode, string Policy, string Connectivity, DateTime? LastSeenAt, string Hostname,
    string Os, string Cpu, long RamBytes, string AgentVersion,
    DateTime? RevokedAt = null, string? RevokedReason = null);
