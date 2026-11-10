using ITManagement.API.Responses;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITManagement.API.Controllers;

[ApiController, Route("api/assets/{id:guid}/access"), Authorize(Policy = "AdminOnly"), RequestSizeLimit(8192)]
public sealed class DeviceAccessController(IDeviceSessionService sessions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid id, CancellationToken ct) => Ok(ApiResponse<object>.Ok(await sessions.AccessAsync(id, ct)));

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Grant(Guid id, Guid userId, CancellationToken ct)
    {
        await sessions.SetAccessAsync(id, userId, true, ct);
        return Ok(ApiResponse<object>.Ok(new { granted = true }));
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Revoke(Guid id, Guid userId, CancellationToken ct)
    {
        await sessions.SetAccessAsync(id, userId, false, ct);
        return Ok(ApiResponse<object>.Ok(new { revoked = true }));
    }
}
