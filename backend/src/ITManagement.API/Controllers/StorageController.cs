using ITManagement.API.Responses;
using ITManagement.Application.Contracts.Files;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ITManagement.API.Controllers;
/// <summary>Khởi tạo controller quản trị hạn mức và đối soát lưu trữ.</summary>
[ApiController, Authorize(Policy = "AdminOnly"), Route("api/storage/quotas"), RequestSizeLimit(8192)]
public sealed class StorageController(IStorageAdminService storage) : ControllerBase
{
    /// <summary>Liệt kê hạn mức lưu trữ của người dùng.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QuotaQuery query, CancellationToken ct)
        => Ok(ApiResponse<ITManagement.Application.Common.Models.PagedResult<QuotaResponse>>.Ok(await storage.ListAsync(query, ct)));
    /// <summary>Cập nhật hạn mức lưu trữ cho người dùng.</summary>
    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Update(Guid userId, UpdateQuotaRequest request, CancellationToken ct)
        => Ok(ApiResponse<QuotaResponse>.Ok(await storage.UpdateAsync(userId, request.QuotaBytes!.Value, ct)));
    /// <summary>Đối soát metadata, dung lượng sử dụng và các đối tượng lưu trữ.</summary>
    [HttpPost("{userId:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid userId, ReconcileStorageRequest request, CancellationToken ct)
        => Ok(ApiResponse<ReconcileStorageResponse>.Ok(await storage.ReconcileAsync(userId, request.Apply, ct)));
}
