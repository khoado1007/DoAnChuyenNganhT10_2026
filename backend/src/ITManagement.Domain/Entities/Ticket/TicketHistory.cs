using ITManagement.Domain.Common;
using ITManagement.Domain.Enums;

namespace ITManagement.Domain.Entities.Ticket;

/// <summary>
/// Lịch sử thay đổi trạng thái của một ticket.
/// </summary>
public class TicketHistory : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public TicketStatus FromStatus { get; set; }
    public TicketStatus ToStatus { get; set; }
    public string ChangedByUsername { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
