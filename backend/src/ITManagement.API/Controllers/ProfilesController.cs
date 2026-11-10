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

    [HttpPut("{id:guid}/quota")]
    public async Task<ActionResult<ApiResponse<object>>> SetQuota(Guid id, [FromBody] SetProfileQuotaRequest request, CancellationToken ct)
    {
        await profiles.SetProfileQuotaAsync(id, request.MaxFiles, ct);
        return Ok(ApiResponse<object>.Ok(new { updated = true, maxFiles = request.MaxFiles }));
    }
}
