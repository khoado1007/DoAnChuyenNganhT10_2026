using System.Collections.Concurrent;
using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Contracts.Simulation;

namespace ITManagement.Infrastructure.Services;

/// <summary>
/// Singleton điều phối hàng đợi lệnh Agent và hoàn tất bất đồng bộ bằng TaskCompletionSource trong bộ nhớ.
/// </summary>
public sealed class AgentOperationDispatcher : IAgentOperationDispatcher
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<AgentFileCommand>> _queues = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<AgentFileResult>> _waiters = new();

    public void Enqueue(Guid agentId, AgentFileCommand command)
    {
        var queue = _queues.GetOrAdd(agentId, _ => new ConcurrentQueue<AgentFileCommand>());
        queue.Enqueue(command);
    }

    public AgentFileCommand? Dequeue(Guid agentId)
    {
        if (_queues.TryGetValue(agentId, out var queue) && queue.TryDequeue(out var command))
        {
            return command;
        }
        return null;
    }

    public void RegisterWaiting(Guid operationId, TaskCompletionSource<AgentFileResult> tcs)
    {
        _waiters[operationId] = tcs;
    }

    public bool TryComplete(Guid operationId, AgentFileResult result)
    {
        if (_waiters.TryRemove(operationId, out var tcs))
        {
            return tcs.TrySetResult(result);
        }
        return false;
    }

    public void Cancel(Guid operationId)
    {
        if (_waiters.TryRemove(operationId, out var tcs))
        {
            tcs.TrySetCanceled();
        }
    }
}
