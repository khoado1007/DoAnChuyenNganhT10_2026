using ITManagement.Application.Contracts.Simulation;

namespace ITManagement.Application.Common.Interfaces;

/// <summary>
/// Điều phối hàng đợi lệnh giữa HTTP callers và Agent polling channel trong bộ nhớ.
/// </summary>
public interface IAgentOperationDispatcher
{
    void Enqueue(Guid agentId, AgentFileCommand command);
    AgentFileCommand? Dequeue(Guid agentId);
    void RegisterWaiting(Guid operationId, TaskCompletionSource<AgentFileResult> tcs);
    bool TryComplete(Guid operationId, AgentFileResult result);
    void Cancel(Guid operationId);
}
