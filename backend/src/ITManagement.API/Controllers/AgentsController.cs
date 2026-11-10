using ITManagement.API.Responses;
using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Contracts.Agents;
using ITManagement.Application.Contracts.Simulation;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ITManagement.API.Controllers;

/// <summary>Khởi tạo controller quản lý đăng ký, heartbeat và trạng thái agent.</summary>
[ApiController]
[Route("api/agents")]
[RequestSizeLimit(8192)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AgentsController(IAgentService agents, IProfileOperationService profileOperations) : ControllerBase
{
    private string GetAgentCredential()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Agent ", StringComparison.OrdinalIgnoreCase))
            throw new AuthenticationRequiredException("Cần credential Agent.");
        return header[6..];
    }

    /// <summary>Cấp phát token đăng ký cho agent theo yêu cầu của quản trị viên.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("enrollment-tokens")]
    public async Task<ActionResult<ApiResponse<EnrollmentIssued>>> Issue(IssueEnrollmentRequest request, CancellationToken ct)
        => Ok(ApiResponse<EnrollmentIssued>.Ok(await agents.IssueAsync(request, ct)));

    /// <summary>Cấp phát token khôi phục để đăng ký lại agent.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("recovery-tokens")]
    public async Task<ActionResult<ApiResponse<EnrollmentIssued>>> IssueRecovery(IssueRecoveryRequest request, CancellationToken ct)
        => Ok(ApiResponse<EnrollmentIssued>.Ok(await agents.IssueRecoveryAsync(request, ct)));

    /// <summary>Đăng ký agent bằng token và trả về thông tin xác thực ban đầu.</summary>
    [AllowAnonymous, EnableRateLimiting("agent-enrollment")]
    [HttpPost("enroll")]
    public async Task<ActionResult<ApiResponse<AgentEnrolled>>> Enroll(EnrollRequest request, CancellationToken ct)
        => Ok(ApiResponse<AgentEnrolled>.Ok(await agents.EnrollAsync(request, ct)));

    /// <summary>Nhận heartbeat từ agent sau khi xác thực credential.</summary>
    [AllowAnonymous, EnableRateLimiting("agent-heartbeat")]
    [HttpPost("heartbeat")]
    public async Task<ActionResult<ApiResponse<HeartbeatAccepted>>> Heartbeat(HeartbeatRequest request, CancellationToken ct)
        => Ok(ApiResponse<HeartbeatAccepted>.Ok(await agents.HeartbeatAsync(GetAgentCredential(), request, ct)));

    /// <summary>Liệt kê các agent đã đăng ký.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentResponse>>>> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<AgentResponse>>.Ok(await agents.ListAsync(ct)));

    /// <summary>Vô hiệu hóa agent theo mã định danh.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/disable")]
    public async Task<ActionResult<ApiResponse<bool>>> Disable(Guid id, CancellationToken ct)
    {
        await agents.DisableAsync(id, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>Agent thăm dò (poll) lệnh xử lý profile đang chờ.</summary>
    [AllowAnonymous]
    [HttpPost("operations/poll")]
    public async Task<ActionResult<ApiResponse<AgentPollResponse>>> Poll(CancellationToken ct)
        => Ok(ApiResponse<AgentPollResponse>.Ok(await profileOperations.PollOperationAsync(GetAgentCredential(), ct)));

    /// <summary>Agent xác thực quyền thực thi lệnh trước khi thao tác đĩa.</summary>
    [AllowAnonymous]
    [HttpPost("operations/{id:guid}/authorize")]
    public async Task<ActionResult<ApiResponse<AgentAuthorizeResponse>>> Authorize(Guid id, CancellationToken ct)
        => Ok(ApiResponse<AgentAuthorizeResponse>.Ok(await profileOperations.AuthorizeOperationAsync(GetAgentCredential(), id, ct)));

    /// <summary>Agent báo cáo kết quả thực thi lệnh cục bộ.</summary>
    [AllowAnonymous]
    [HttpPost("operations/{id:guid}/result"), RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> SubmitResult(Guid id, [FromBody] AgentFileResult result, CancellationToken ct)
    {
        await profileOperations.HandleAgentResultAsync(GetAgentCredential(), id, result, ct);
        return Ok(ApiResponse<object>.Ok(new { accepted = true }));
    }
}
