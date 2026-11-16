using ITManagement.API.Responses;
using ITManagement.Application.Contracts.Assets;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITManagement.API.Controllers;

[ApiController]
public sealed class MachineInformationController(IMachineInformationService information) : ControllerBase
{
    [HttpGet("api/machines/{id:guid}/information"), Authorize(Roles = "Employee")]
    public async Task<ActionResult<ApiResponse<MachineInformationResponse>>> GetForSession(
        Guid id, [FromHeader(Name = "X-Device-Session")] Guid sessionId, CancellationToken ct)
        => Ok(ApiResponse<MachineInformationResponse>.Ok(await information.GetForSessionAsync(id, sessionId, ct)));

    [HttpGet("api/assets/{id:guid}/specification"), Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<MachineInformationResponse>>> Get(Guid id, CancellationToken ct)
        => Ok(ApiResponse<MachineInformationResponse>.Ok(await information.GetAsync(id, ct)));

    [HttpPut("api/assets/{id:guid}/specification"), Authorize(Policy = "AdminOnly"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<MachineInformationResponse>>> Update(
        Guid id, UpdateMachineSpecificationRequest request, CancellationToken ct)
        => Ok(ApiResponse<MachineInformationResponse>.Ok(await information.UpdateAsync(id, request, ct)));
}
