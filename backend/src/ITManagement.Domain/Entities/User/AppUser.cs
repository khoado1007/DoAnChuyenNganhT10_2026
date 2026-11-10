using ITManagement.Domain.Common;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Entities.Storage;

namespace ITManagement.Domain.Entities.User;

/// <summary>
/// Người dùng hệ thống: Admin, Kỹ thuật viên, Nhân viên.
/// Tách riêng khỏi ASP.NET Identity để domain không phụ thuộc framework.
/// </summary>
public class AppUser : BaseEntity
{
    public int SecurityVersion { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;

    // Liên kết tới ASP.NET Identity user (IdentityUser.Id)
    public string IdentityUserId { get; set; } = string.Empty;

    // Navigation properties
    public StorageQuota? StorageQuota { get; set; }
    public ICollection<AssetAssignment> AssetAssignments { get; set; } = [];
    public ICollection<Ticket.Ticket> CreatedTickets { get; set; } = [];
    public ICollection<Ticket.Ticket> AssignedTickets { get; set; } = [];
}
