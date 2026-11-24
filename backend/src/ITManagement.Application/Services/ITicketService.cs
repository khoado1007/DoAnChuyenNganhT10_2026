using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Tickets;

namespace ITManagement.Application.Services;

/// <summary>Nghiệp vụ ticket cơ bản: Employee gửi, Admin xử lý, bình luận và lịch sử trạng thái.</summary>
public interface ITicketService
{
    /// <summary>Danh sách ticket trong phạm vi vai trò hiện tại, có lọc và phân trang.</summary>
    Task<PagedResult<TicketListItemResponse>> ListAsync(TicketQuery query, CancellationToken ct);

    /// <summary>Chi tiết ticket; chỉ người tạo, người được giao hoặc Admin được xem.</summary>
    Task<TicketDetailResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Tạo ticket mới cho Employee hiện tại.</summary>
    Task<TicketDetailResponse> CreateAsync(CreateTicketRequest request, CancellationToken ct);

    /// <summary>Chuyển trạng thái ticket theo luồng Open → InProgress → Resolved → Closed (Admin hoặc Kỹ thuật viên được giao).</summary>
    Task<TicketDetailResponse> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct);

    /// <summary>Admin phân công ticket cho Kỹ thuật viên (hoặc hủy phân công khi null).</summary>
    Task<TicketDetailResponse> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken ct);

    /// <summary>Thêm bình luận vào ticket đang được phép xem.</summary>
    Task<TicketCommentResponse> AddCommentAsync(Guid id, AddTicketCommentRequest request, CancellationToken ct);
}
