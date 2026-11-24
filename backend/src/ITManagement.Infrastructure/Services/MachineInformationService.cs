using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ITManagement.Application.Contracts.Assets;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Exceptions;
using ITManagement.Domain.Enums;
using ITManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

public sealed class MachineInformationService(AppDbContext db, IProfileOperationService profiles, TimeProvider clock,
    UserManager<IdentityUser> userManager)
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
        var info = await GetAsync(assetId, ct);
        return ForEmployee(info);
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
            ReadSoftware(assetId, spec), Revision(spec));
    }

    public async Task<MachineInformationResponse> UpdateAsync(Guid assetId, UpdateMachineSpecificationRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAssetAsync(assetId, ct);
        var spec = await db.MachineSpecifications.SingleOrDefaultAsync(x => x.AssetId == assetId, ct);
        CheckRevision(spec, request.Revision, allowNew: true);
        var existingIds = ReadSoftware(assetId, spec).Select(item => item.Id!.Value).ToHashSet();
        var suppliedIds = request.Software.Where(item => item.Id.HasValue).Select(item => item.Id!.Value).ToArray();
        if (suppliedIds.Any(id => id == Guid.Empty || !existingIds.Contains(id)) || suppliedIds.Distinct().Count() != suppliedIds.Length)
            throw new BusinessRuleException("Mã phần mềm không hợp lệ hoặc bị trùng. Vui lòng tải lại cấu hình.");
        var software = request.Software.Select(item => item with
        {
            Id = item.Id ?? Guid.NewGuid(), Name = item.Name.Trim(), LastUsedAt = Utc(item.LastUsedAt)
        }).ToList();
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
        spec.SoftwareJson = JsonSerializer.Serialize(software);
        spec.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(assetId, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<MachineInformationResponse> RemoveSoftwareAsync(Guid assetId, Guid sessionId, Guid softwareId,
        RemoveMachineSoftwareRequest request, CancellationToken ct)
    {
        // Check the caller's live workspace before evaluating administrator credentials.
        await profiles.GetProfileSnapshotAsync(assetId, sessionId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAssetAsync(assetId, ct);
        await profiles.GetProfileSnapshotAsync(assetId, sessionId, ct);
        await ValidateAdministratorAsync(request.AdminUsername, request.AdminPassword, ct);
        var spec = await db.MachineSpecifications.SingleOrDefaultAsync(x => x.AssetId == assetId, ct);
        CheckRevision(spec, request.Revision);
        var software = ReadSoftware(assetId, spec);
        if (software.RemoveAll(item => item.Id == softwareId) != 1)
            throw new NotFoundException("Software", softwareId);
        spec!.SoftwareJson = JsonSerializer.Serialize(software);
        spec.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        var response = ForEmployee(await GetAsync(assetId, ct));
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<BulkMachineSoftwareResponse> AddSoftwareBulkAsync(BulkMachineSoftwareRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var assets = new List<Asset>();
        // All mutation paths serialize on the asset row; ordering prevents cross-machine deadlocks.
        foreach (var assetId in request.AssetIds.Order()) assets.Add(await LockAssetAsync(assetId, ct));
        var results = new List<BulkMachineSoftwareResult>();
        foreach (var asset in assets)
        {
            var spec = await db.MachineSpecifications.SingleOrDefaultAsync(x => x.AssetId == asset.Id, ct);
            var software = ReadSoftware(asset.Id, spec);
            var added = 0; var skipped = 0;
            foreach (var input in request.Software)
            {
                var name = input.Name.Trim();
                if (software.Any(item => item.Kind == input.Kind && string.Equals(item.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                { skipped++; continue; }
                software.Add(input with { Id = Guid.NewGuid(), Name = name, LastUsedAt = Utc(input.LastUsedAt) });
                added++;
            }
            if (software.Count > 200)
                throw new BusinessRuleException($"Máy {asset.Name} vượt giới hạn 200 phần mềm. Chưa có máy nào được cập nhật.");
            if (added > 0)
            {
                if (spec is null) { spec = new MachineSpecification { AssetId = asset.Id }; db.MachineSpecifications.Add(spec); }
                spec.SoftwareJson = JsonSerializer.Serialize(software);
                spec.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            }
            results.Add(new BulkMachineSoftwareResult(asset.Id, asset.Name, added, skipped));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new BulkMachineSoftwareResponse(results, results.Sum(item => item.Added), results.Sum(item => item.Skipped));
    }

    private async Task<Asset> LockAssetAsync(Guid assetId, CancellationToken ct)
        => await db.Assets.FromSqlInterpolated($"SELECT * FROM assets WITH (UPDLOCK, ROWLOCK) WHERE Id = {assetId}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Asset", assetId);

    private async Task ValidateAdministratorAsync(string username, string password, CancellationToken ct)
    {
        var login = username.Trim();
        if (login.StartsWith("COMPANY\\", StringComparison.OrdinalIgnoreCase)) login = login[8..];
        var identity = await userManager.FindByNameAsync(login);
        if (identity is null) throw InvalidApproval();
        var administrator = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE IdentityUserId = {identity.Id}")
            .SingleOrDefaultAsync(ct);
        if (administrator is null || administrator.Status != UserStatus.Active || administrator.Role != UserRole.Admin)
            throw InvalidApproval();
        await db.Entry(identity).ReloadAsync(ct);
        if (await userManager.IsLockedOutAsync(identity) || !await userManager.CheckPasswordAsync(identity, password))
            throw InvalidApproval();
    }

    private static UnauthorizedException InvalidApproval() => new("Không thể xác nhận tài khoản quản trị. Vui lòng kiểm tra thông tin và thử lại.");
    private static MachineInformationResponse ForEmployee(MachineInformationResponse response)
        => response with { Software = response.Software.Select(item => item with { FilePath = null }).ToList() };

    private static string Revision(MachineSpecification? spec)
        => spec is null ? "0" : Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(spec)));

    private static void CheckRevision(MachineSpecification? spec, string? revision, bool allowNew = false)
    {
        if (allowNew && spec is null && revision is null) return;
        if (!string.Equals(revision, Revision(spec), StringComparison.Ordinal))
            throw new SimulationException(409, "SPECIFICATION_CHANGED", "Cấu hình máy đã thay đổi. Vui lòng tải lại trước khi thực hiện.");
    }

    private static List<MachineSoftwareDto> ReadSoftware(Guid assetId, MachineSpecification? spec)
    {
        if (spec is null) return [];
        var values = JsonSerializer.Deserialize<MachineSoftwareDto[]>(spec.SoftwareJson) ?? [];
        // Legacy rows lack IDs. Derive them without writing during GET; the next authorized mutation
        // persists these IDs before any reorder/removal, so surviving rows keep their identity.
        return values.Select((item, index) => item with
        {
            Id = item.Id is Guid id && id != Guid.Empty ? id : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{assetId:N}:legacy-software:{index}"))[..16]),
            LastUsedAt = Utc(item.LastUsedAt)
        }).ToList();
    }
}
