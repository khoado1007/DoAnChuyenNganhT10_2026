using ITManagement.Application.Contracts.Assets;

namespace ITManagement.Application.Services;

public interface IMachineInformationService
{
    Task<MachineInformationResponse> GetAsync(Guid assetId, CancellationToken ct);
    Task<MachineInformationResponse> GetForSessionAsync(Guid assetId, Guid sessionId, CancellationToken ct);
    Task<MachineInformationResponse> UpdateAsync(Guid assetId, UpdateMachineSpecificationRequest request, CancellationToken ct);
}
