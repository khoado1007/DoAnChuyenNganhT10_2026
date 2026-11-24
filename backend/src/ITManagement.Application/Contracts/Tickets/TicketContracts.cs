using System.ComponentModel.DataAnnotations;
using ITManagement.Domain.Enums;

namespace ITManagement.Application.Contracts.Tickets;

/// <summary>Dữ liệu tạo yêu cầu hỗ trợ; chỉ Employee được gửi.</summary>
public sealed record CreateTicketRequest(
    [Required, StringLength(500, MinimumLength = 5)] string Title,
    [Required, StringLength(4000, MinimumLength = 10)] string Description,
    [EnumDataType(typeof(TicketCategory))] TicketCategory Category,
    [EnumDataType(typeof(TicketPriority))] TicketPriority Priority);

/// <summary>Bộ lọc danh sách ticket; phạm vi dữ liệu do service giới hạn theo vai trò.</summary>
public sealed record TicketQuery(
    [Range(1, 100000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(100)] string? Search = null,
    [EnumDataType(typeof(TicketStatus))] TicketStatus? Status = null,
    [EnumDataType(typeof(TicketCategory))] TicketCategory? Category = null,
    [EnumDataType(typeof(TicketPriority))] TicketPriority? Priority = null);

/// <summary>Một dòng trong danh sách ticket.</summary>
public sealed record TicketListItemResponse(
    Guid Id,
    string TicketNumber,
    string Title,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    string CreatedByName,
    string? AssignedToName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Hợp đồng ticket rút gọn còn dùng cho dashboard; danh sách mới dùng <see cref="TicketListItemResponse"/>.</summary>
public sealed record TicketResponse(Guid Id, string TicketNumber, string Title, string Description,
    TicketCategory Category, TicketPriority Priority, TicketStatus Status,
    Guid CreatedByUserId, string CreatedByName, Guid? AssignedToUserId, string? AssignedToName,
    DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>Bình luận/ghi chú trên ticket; ghi chú nội bộ chỉ Admin và Technician thấy.</summary>
public sealed record TicketCommentResponse(
    Guid Id,
    string AuthorUsername,
    string Content,
    bool IsInternal,
    DateTime CreatedAt);

/// <summary>Một lần chuyển trạng thái ticket.</summary>
public sealed record TicketHistoryResponse(
    TicketStatus FromStatus,
    TicketStatus ToStatus,
    string ChangedByUsername,
    string? Note,
    DateTime ChangedAt);

/// <summary>Chi tiết ticket kèm bình luận và lịch sử trạng thái.</summary>
public sealed record TicketDetailResponse(
    Guid Id,
    string TicketNumber,
    string Title,
    string Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    Guid CreatedByUserId,
    string CreatedByName,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt,
    string? Resolution,
    IReadOnlyList<TicketCommentResponse> Comments,
    IReadOnlyList<TicketHistoryResponse> History);

/// <summary>Phân công ticket cho Kỹ thuật viên; chỉ Admin được thực hiện.</summary>
public sealed record AssignTicketRequest(
    Guid? AssignedToUserId,
    [StringLength(500)] string? Note = null);

/// <summary>Cập nhật trạng thái ticket; Admin bắt buộc nhập kết quả khi Resolved.</summary>
public sealed record UpdateTicketStatusRequest(
    [EnumDataType(typeof(TicketStatus))] TicketStatus Status,
    [StringLength(4000)] string? Resolution = null,
    [StringLength(500)] string? Note = null);

/// <summary>Thêm bình luận vào ticket; Employee không được đánh dấu nội bộ.</summary>
public sealed record AddTicketCommentRequest(
    [Required, StringLength(2000, MinimumLength = 1)] string Content,
    bool IsInternal = false);
