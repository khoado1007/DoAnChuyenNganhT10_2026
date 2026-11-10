using System.ComponentModel.DataAnnotations;
using ITManagement.Domain.Enums;
namespace ITManagement.Application.Contracts.Assets;
public sealed record AssignAssetRequest(Guid UserId, [StringLength(1000)] string? Notes);
public sealed record CloseAssignmentRequest(Guid AssignmentId, [StringLength(1000)] string? Notes);
public sealed record AssignmentResponse(Guid Id, Guid AssetId, string AssetCode, string AssetName,
    Guid UserId, string Username, DateTime AssignedAt, DateTime? ReturnedAt, AssignmentStatus Status,
    string AssignedBy, string Notes, string? ClosedBy, string? CloseNotes);
