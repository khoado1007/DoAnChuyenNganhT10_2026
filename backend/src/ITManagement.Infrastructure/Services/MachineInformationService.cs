using System.Text.Json;
using ITManagement.Application.Contracts.Assets;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

public sealed class MachineInformationService(AppDbContext db, IProfileOperationService profiles, TimeProvider clock)
    : IMachineInformationService
{
    // SQL datetime2 does not retain Kind. Values in these columns are UTC, including values read back after save.
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value is DateTime date ? Utc(date) : null;
    public async Task<MachineInformationResponse> GetForSessionAsync(Guid assetId, Guid sessionId, CancellationToken ct)
    {
        // Reuse the same current-user, active account, machine and session checks as the file workspace.
        await profiles.GetProfileSnapshotAsync(assetId, sessionId, ct);
        return await GetAsync(assetId, ct);
    }

    public async Task<MachineInformationResponse> GetAsync(Guid assetId, CancellationToken ct)
    {
        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId, ct)
            ?? throw new NotFoundException("Asset", assetId);
        var spec = await db.MachineSpecifications.AsNoTracking().SingleOrDefaultAsync(x => x.AssetId == assetId, ct);
        return new MachineInformationResponse(asset.Id, asset.AssetCode, asset.Name, "simulated",
            asset.SerialNumber, asset.Manufacturer, asset.Model, Utc(asset.CreatedAt), Utc(spec?.UpdatedAt ?? asset.UpdatedAt),
            spec?.OperatingSystem, spec?.Processor, spec?.MemoryGb, spec?.DiskTotalGb, spec?.DiskFreeGb,
            spec?.DiskEncryption, spec?.CollectorVersion, spec?.PrivateIp, spec?.PublicIp, Utc(spec?.LastRestartedAt),
            spec is null ? [] : JsonSerializer.Deserialize<MachineSoftwareDto[]>(spec.SoftwareJson) ?? []);
    }

    public async Task<MachineInformationResponse> UpdateAsync(Guid assetId, UpdateMachineSpecificationRequest request, CancellationToken ct)
    {
        if (!await db.Assets.AnyAsync(x => x.Id == assetId, ct)) throw new NotFoundException("Asset", assetId);
        var spec = await db.MachineSpecifications.SingleOrDefaultAsync(x => x.AssetId == assetId, ct);
        if (spec is null)
        {
            spec = new MachineSpecification { AssetId = assetId };
            db.MachineSpecifications.Add(spec);
        }
        spec.OperatingSystem = request.OperatingSystem;
        spec.Processor = request.Processor;
        spec.MemoryGb = request.MemoryGb;
        spec.DiskTotalGb = request.DiskTotalGb;
        spec.DiskFreeGb = request.DiskFreeGb;
        spec.DiskEncryption = request.DiskEncryption;
        spec.CollectorVersion = request.CollectorVersion;
        spec.PrivateIp = request.PrivateIp;
        spec.PublicIp = request.PublicIp;
        spec.LastRestartedAt = Utc(request.LastRestartedAt);
        spec.SoftwareJson = JsonSerializer.Serialize(request.Software.Select(item => item with { LastUsedAt = Utc(item.LastUsedAt) }));
        spec.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return await GetAsync(assetId, ct);
    }
}
