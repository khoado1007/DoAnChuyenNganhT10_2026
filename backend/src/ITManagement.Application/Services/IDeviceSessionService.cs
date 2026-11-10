using ITManagement.Application.Contracts.Simulation;

namespace ITManagement.Application.Services;

public interface IDeviceSessionService
{
    Task<IReadOnlyList<MachineResponse>> MachinesAsync(CancellationToken ct);
    Task<IReadOnlyList<DeviceAccessResponse>> AccessAsync(Guid assetId, CancellationToken ct);
    Task SetAccessAsync(Guid assetId, Guid userId, bool grant, CancellationToken ct);
    Task<DeviceSessionResponse> StartAsync(Guid assetId, CancellationToken ct);
    Task EndAsync(Guid assetId, Guid sessionId, CancellationToken ct);
}
