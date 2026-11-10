using ITManagement.Domain.Common;

namespace ITManagement.Domain.Entities.Ticket;

/// <summary>
/// Comment / ghi chú trong quá trình xử lý ticket.
/// </summary>
public class TicketComment : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public string AuthorUsername { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false; // True = chỉ kỹ thuật viên/admin thấy
}
