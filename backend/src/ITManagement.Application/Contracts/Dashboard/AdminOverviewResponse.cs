namespace ITManagement.Application.Contracts.Dashboard;

public sealed record AdminOverviewResponse(
    int Users, int Assets, int AssignedAssets, int Agents, int Profiles);
