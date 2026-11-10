using ITManagement.Application.Contracts.Tickets;
using ITManagement.Domain.Enums;

namespace ITManagement.Application.Contracts.Dashboard;

public sealed record AssetStatusCount(AssetStatus Status, int Count);
public sealed record StorageQuotaResponse(long QuotaBytes, long UsedBytes, long AvailableBytes);
public sealed record DashboardResponse(int TotalAssets, int TotalUsers, int OpenTickets,
    int ResolvedThisMonth, IReadOnlyList<AssetStatusCount> AssetsByStatus,
    IReadOnlyList<TicketResponse> RecentTickets);
