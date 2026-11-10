using ITManagement.Application.Contracts.Simulation;

namespace ITManagement.Application.Services;

/// <summary>
/// Quản lý snapshot profile, hạn mức tệp (Quota Reservation) và điều phối lệnh file xuống Agent.
/// </summary>
public interface IProfileOperationService
{
    Task<ProfileSnapshot> GetProfileSnapshotAsync(Guid assetId, Guid sessionId, CancellationToken ct);
    Task<ProfileSnapshot> ExecuteOperationAsync(Guid assetId, Guid sessionId, FileActionRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProfileSummaryResponse>> ListProfilesAsync(CancellationToken ct);
    Task SetProfileQuotaAsync(Guid profileId, int maxFiles, CancellationToken ct);
    Task<AgentPollResponse> PollOperationAsync(string agentCredential, CancellationToken ct);
    Task<AgentAuthorizeResponse> AuthorizeOperationAsync(string agentCredential, Guid operationId, CancellationToken ct);
    Task HandleAgentResultAsync(string agentCredential, Guid operationId, AgentFileResult result, CancellationToken ct);
}
