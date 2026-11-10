using ITManagement.API.Responses;
using ITManagement.Application.Contracts.Assets;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ITManagement.API.Controllers;
/// <summary>Khởi tạo controller giao, hoàn trả và thu hồi tài sản.</summary>
[ApiController, Authorize(Policy = "AdminOnly"), Route("api/assets/{assetId:guid}"), RequestSizeLimit(8192)]
public sealed class AssignmentsController(IAssignmentService assignments) : ControllerBase
{
    /// <summary>Giao tài sản cho người dùng được chỉ định.</summary>
    [HttpPost("assign")]
    public async Task<IActionResult> Assign(Guid assetId, AssignAssetRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AssignmentResponse>.Ok(await assignments.AssignAsync(assetId, request, ct)));
    /// <summary>Ghi nhận việc hoàn trả tài sản.</summary>
    [HttpPost("return")]
    public async Task<IActionResult> Return(Guid assetId, CloseAssignmentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AssignmentResponse>.Ok(await assignments.CloseAsync(assetId, request, false, ct)));
    /// <summary>Thu hồi lượt giao tài sản đang hoạt động.</summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(Guid assetId, CloseAssignmentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AssignmentResponse>.Ok(await assignments.CloseAsync(assetId, request, true, ct)));
    /// <summary>Lấy lịch sử giao nhận tài sản.</summary>
    [HttpGet("assignments")]
    public async Task<IActionResult> History(Guid assetId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<AssignmentResponse>>.Ok(await assignments.HistoryAsync(assetId, ct)));
}
/// <summary>Khởi tạo controller truy vấn tài sản đang được giao cho người dùng.</summary>
[ApiController, Authorize(Roles = "Employee"), Route("api/me/assets")]
public sealed class MyAssetsController(IAssignmentService assignments) : ControllerBase
{
    /// <summary>Lấy các tài sản đang được giao cho người dùng hiện tại.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<AssignmentResponse>>.Ok(await assignments.MineAsync(ct)));
}
