using ITManagement.API.Responses;
using ITManagement.Application.Contracts.Simulation;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITManagement.API.Controllers;

[ApiController]
[Route("api/profiles")]
[Authorize(Policy = "AdminOnly")]
[RequestSizeLimit(8192)]
public sealed class ProfilesController(IProfileOperationService profiles) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProfileSummaryResponse>>>> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProfileSummaryResponse>>.Ok(await profiles.ListProfilesAsync(ct)));

    [HttpPost("{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct)
    {
        await profiles.ReconcileAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { reconciled = true }));
    }

    [HttpGet("{id:guid}/reconciliation-history")]
    public async Task<IActionResult> ReconciliationHistory(Guid id, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ProfileReconciliationEvent>>.Ok(await profiles.ReconciliationHistoryAsync(id, ct)));

    [HttpPut("{id:guid}/quota")]
    public async Task<ActionResult<ApiResponse<object>>> SetQuota(Guid id, [FromBody] SetProfileQuotaRequest request, CancellationToken ct)
    {
        await profiles.SetProfileQuotaAsync(id, request.MaxFiles, ct);
        return Ok(ApiResponse<object>.Ok(new { updated = true, maxFiles = request.MaxFiles }));
    }
}
