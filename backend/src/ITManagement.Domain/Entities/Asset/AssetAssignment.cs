using ITManagement.Domain.Common;
using ITManagement.Domain.Enums;

namespace ITManagement.Domain.Entities.Asset;

/// <summary>
/// Lịch sử cấp phát / thu hồi thiết bị cho nhân viên.
/// Mỗi bản ghi là một khoảng thời gian thiết bị được giao cho một nhân viên.
/// </summary>
public class AssetAssignment : BaseEntity
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public Guid UserId { get; set; }
    public User.AppUser User { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReturnedAt { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;
    public string AssignedBy { get; set; } = string.Empty;  // Username của admin
    public string? ClosedBy { get; set; }
    public string? CloseNotes { get; set; }
    public string Notes { get; set; } = string.Empty;
}
