using ITManagement.Domain.Common;
using ITManagement.Domain.Enums;

namespace ITManagement.Domain.Entities.Ticket;

/// <summary>
/// Yêu cầu hỗ trợ IT (ITSM Ticket).
/// </summary>
public class Ticket : BaseEntity
{
    public string TicketNumber { get; set; } = string.Empty;  // VD: TKT-2024-001
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketCategory Category { get; set; }

    // Người tạo ticket
    public Guid CreatedByUserId { get; set; }
    public new User.AppUser CreatedBy { get; set; } = null!;

    // Kỹ thuật viên được phân công
    public Guid? AssignedToUserId { get; set; }
    public User.AppUser? AssignedTo { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }

    // Navigation
    public ICollection<TicketHistory> History { get; set; } = [];
    public ICollection<TicketComment> Comments { get; set; } = [];
}
