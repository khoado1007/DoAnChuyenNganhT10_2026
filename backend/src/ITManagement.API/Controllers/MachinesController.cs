using ITManagement.API.Responses;
using ITManagement.Application.Contracts.Simulation;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITManagement.API.Controllers;

[ApiController, Route("api/machines"), RequestSizeLimit(8192)]
public sealed class MachinesController(IDeviceSessionService sessions, IProfileOperationService profiles) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<object>.Ok(await sessions.MachinesAsync(ct)));

    [HttpPost("{id:guid}/sessions"), Authorize(Roles = "Employee")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct) => Ok(ApiResponse<object>.Ok(await sessions.StartAsync(id, ct)));

    [HttpPost("{id:guid}/logout"), Authorize(Roles = "Employee")]
    public async Task<IActionResult> Logout(Guid id, [FromHeader(Name = "X-Device-Session")] Guid sessionId, CancellationToken ct)
    {
        await sessions.EndAsync(id, sessionId, ct);
        return Ok(ApiResponse<object>.Ok(new { ended = true }));
    }

    [HttpGet("{id:guid}/profile"), Authorize(Roles = "Employee")]
    public async Task<IActionResult> GetProfile(Guid id, [FromHeader(Name = "X-Device-Session")] Guid sessionId, CancellationToken ct)
        => Ok(ApiResponse<ProfileSnapshot>.Ok(await profiles.GetProfileSnapshotAsync(id, sessionId, ct)));

    [HttpPost("{id:guid}/files/operations"), Authorize(Roles = "Employee"), RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Operate(Guid id, [FromHeader(Name = "X-Device-Session")] Guid sessionId, [FromBody] FileActionRequest request, CancellationToken ct)
        => Ok(ApiResponse<ProfileSnapshot>.Ok(await profiles.ExecuteOperationAsync(id, sessionId, request, ct)));
}
