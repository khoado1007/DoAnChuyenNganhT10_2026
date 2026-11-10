using ITManagement.Application.Contracts.Dashboard;

namespace ITManagement.Application.Services;

public interface IDashboardService
{
    Task<AdminOverviewResponse> GetOverviewAsync(CancellationToken ct);
}
