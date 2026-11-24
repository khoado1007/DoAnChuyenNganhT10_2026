using ITManagement.API.Responses;
using ITManagement.Application.Contracts.Assets;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    [HttpPost("api/machines/{id:guid}/software/{softwareId:guid}/remove"), Authorize(Roles = "Employee")]
    [EnableRateLimiting("software-approval"), RequestSizeLimit(16 * 1024)]
    public async Task<ActionResult<ApiResponse<MachineInformationResponse>>> RemoveSoftware(
        Guid id, Guid softwareId, [FromHeader(Name = "X-Device-Session")] Guid sessionId,
        RemoveMachineSoftwareRequest request, CancellationToken ct)
        => Ok(ApiResponse<MachineInformationResponse>.Ok(await information.RemoveSoftwareAsync(id, sessionId, softwareId, request, ct)));

    [HttpPost("api/assets/software/bulk"), Authorize(Policy = "AdminOnly"), RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<BulkMachineSoftwareResponse>>> AddSoftwareBulk(
        BulkMachineSoftwareRequest request, CancellationToken ct)
        => Ok(ApiResponse<BulkMachineSoftwareResponse>.Ok(await information.AddSoftwareBulkAsync(request, ct)));
}
