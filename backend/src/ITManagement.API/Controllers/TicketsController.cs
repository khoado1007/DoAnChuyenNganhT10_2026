using ITManagement.API.Responses;
using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Tickets;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITManagement.API.Controllers;

/// <summary>Yêu cầu hỗ trợ: Employee gửi và theo dõi ticket; Admin xử lý và chuyển trạng thái.</summary>
[ApiController]
[Authorize]
[Route("api/tickets")]
[RequestSizeLimit(16384)]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    /// <summary>Danh sách ticket trong phạm vi vai trò hiện tại.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketListItemResponse>>>> List(
        [FromQuery] TicketQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<TicketListItemResponse>>.Ok(await tickets.ListAsync(query, ct)));

    /// <summary>Chi tiết ticket kèm bình luận và lịch sử trạng thái.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TicketDetailResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<TicketDetailResponse>.Ok(await tickets.GetAsync(id, ct)));

    /// <summary>Employee gửi yêu cầu hỗ trợ mới.</summary>
    [HttpPost]
    [Authorize(Roles = "Employee")]
    public async Task<ActionResult<ApiResponse<TicketDetailResponse>>> Create(
        CreateTicketRequest request, CancellationToken ct)
        => Ok(ApiResponse<TicketDetailResponse>.Ok(await tickets.CreateAsync(request, ct)));

    /// <summary>Admin hoặc Kỹ thuật viên được giao chuyển trạng thái ticket theo luồng cố định.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    public async Task<ActionResult<ApiResponse<TicketDetailResponse>>> UpdateStatus(
        Guid id, UpdateTicketStatusRequest request, CancellationToken ct)
        => Ok(ApiResponse<TicketDetailResponse>.Ok(await tickets.UpdateStatusAsync(id, request, ct)));

    /// <summary>Admin phân công ticket cho Kỹ thuật viên (hoặc hủy phân công khi để trống).</summary>
    [HttpPatch("{id:guid}/assign")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<TicketDetailResponse>>> Assign(
        Guid id, AssignTicketRequest request, CancellationToken ct)
        => Ok(ApiResponse<TicketDetailResponse>.Ok(await tickets.AssignAsync(id, request, ct)));

    /// <summary>Thêm bình luận công khai hoặc ghi chú nội bộ (Admin).</summary>
    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<ApiResponse<TicketCommentResponse>>> AddComment(
        Guid id, AddTicketCommentRequest request, CancellationToken ct)
        => Ok(ApiResponse<TicketCommentResponse>.Ok(await tickets.AddCommentAsync(id, request, ct)));
}
