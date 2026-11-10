using ITManagement.API.Responses;
using ITManagement.Application.Contracts.Dashboard;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITManagement.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "AdminOnly")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<AdminOverviewResponse>>> Get(CancellationToken ct)
        => Ok(ApiResponse<AdminOverviewResponse>.Ok(await dashboard.GetOverviewAsync(ct)));
}
