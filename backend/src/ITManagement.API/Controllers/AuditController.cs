using ITManagement.API.Responses;
using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Audit;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITManagement.API.Controllers;

/// <summary>Nhật ký thao tác hệ thống và metadata file — chỉ Admin, chỉ đọc.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AuditController(IAuditService audit) : ControllerBase
{
    /// <summary>Nhật ký thao tác: lọc theo thời gian, actor, loại thao tác; phân trang.</summary>
    [HttpGet("audit-log")]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditEventResponse>>>> GetAuditLog(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? actorId,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(ApiResponse.Fail("Thời điểm bắt đầu phải trước hoặc bằng thời điểm kết thúc."));

        var result = await audit.ListAuditEventsAsync(
            new AuditLogQuery(from, to, actorId, action, page, pageSize), ct);
        return Ok(ApiResponse<PagedResult<AuditEventResponse>>.Ok(result));
    }

    /// <summary>Metadata file: tên, thư mục, dung lượng, tài khoản, máy, kho lưu trữ. Không đọc nội dung.</summary>
    [HttpGet("files")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminFileResponse>>>> GetFiles(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? assetId,
        [FromQuery] string? folder,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await audit.ListFilesAdminAsync(
            new AdminFileQuery(userId, assetId, folder, page, pageSize), ct);
        return Ok(ApiResponse<PagedResult<AdminFileResponse>>.Ok(result));
    }
}
