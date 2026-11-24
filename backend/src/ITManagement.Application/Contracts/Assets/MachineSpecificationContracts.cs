using System.ComponentModel.DataAnnotations;

namespace ITManagement.Application.Contracts.Assets;

public sealed record MachineSoftwareDto(
    [Required, StringLength(200)] string Name,
    [StringLength(100)] string? Version = null,
    [StringLength(100)] string? Type = null,
    [StringLength(2000)] string? Vulnerabilities = null,
    DateTime? LastUsedAt = null,
    [StringLength(1000)] string? FilePath = null,
    Guid? Id = null,
    [RegularExpression("^(App|Service)$")] string? Kind = null);

public sealed record RemoveMachineSoftwareRequest(
    [Required, StringLength(256)] string AdminUsername,
    [Required, StringLength(1000)] string AdminPassword,
    [Required, StringLength(64)] string Revision);

public sealed class BulkMachineSoftwareRequest : IValidatableObject
{
    [Required, MinLength(1), MaxLength(50)] public Guid[] AssetIds { get; init; } = [];
    [Required, MinLength(1), MaxLength(200)] public MachineSoftwareDto[] Software { get; init; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AssetIds is not null && (AssetIds.Any(id => id == Guid.Empty) || AssetIds.Distinct().Count() != AssetIds.Length))
            yield return new ValidationResult("Danh sách máy không được trùng hoặc chứa mã rỗng.", [nameof(AssetIds)]);
        if (Software is not null && Software.Any(item => item is null || item.Id.HasValue || item.Kind is not ("App" or "Service")))
            yield return new ValidationResult("Mỗi phần mềm mới cần loại App hoặc Service và không cung cấp mã có sẵn.", [nameof(Software)]);
    }
}

public sealed record BulkMachineSoftwareResult(Guid AssetId, string AssetName, int Added, int Skipped);
public sealed record BulkMachineSoftwareResponse(IReadOnlyList<BulkMachineSoftwareResult> Machines, int Added, int Skipped);

public sealed class UpdateMachineSpecificationRequest : IValidatableObject
{
    [StringLength(64)] public string? Revision { get; init; }
    [StringLength(200)] public string? OperatingSystem { get; init; }
    [StringLength(200)] public string? Processor { get; init; }
    [Range(typeof(decimal), "0", "1000000")] public decimal? MemoryGb { get; init; }
    [Range(typeof(decimal), "0", "1000000000")] public decimal? DiskTotalGb { get; init; }
    [Range(typeof(decimal), "0", "1000000000")] public decimal? DiskFreeGb { get; init; }
    public bool? DiskEncryption { get; init; }
    [StringLength(100)] public string? CollectorVersion { get; init; }
    [StringLength(45)] public string? PrivateIp { get; init; }
    [StringLength(45)] public string? PublicIp { get; init; }
    public DateTime? LastRestartedAt { get; init; }
    [Required, MaxLength(200)] public MachineSoftwareDto[] Software { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DiskFreeGb.HasValue && (!DiskTotalGb.HasValue || DiskFreeGb > DiskTotalGb))
            yield return new ValidationResult("Dung lượng trống phải nhỏ hơn hoặc bằng tổng dung lượng.", [nameof(DiskFreeGb)]);
        if (Software is not null && Software.Any(item => item is null))
            yield return new ValidationResult("Phần mềm không được để trống.", [nameof(Software)]);
        foreach (var (value, field) in new[] { (PrivateIp, nameof(PrivateIp)), (PublicIp, nameof(PublicIp)) })
            if (!string.IsNullOrWhiteSpace(value) && !System.Net.IPAddress.TryParse(value, out _))
                yield return new ValidationResult("Địa chỉ IP không hợp lệ.", [field]);
    }
}

public sealed record MachineInformationResponse(
    Guid AssetId, string AssetCode, string Name, string Status,
    string SerialNumber, string Manufacturer, string Model,
    DateTime CreatedAt, DateTime? UpdatedAt,
    string? OperatingSystem, string? Processor, decimal? MemoryGb,
    decimal? DiskTotalGb, decimal? DiskFreeGb, bool? DiskEncryption,
    string? CollectorVersion, string? PrivateIp, string? PublicIp,
    DateTime? LastRestartedAt, IReadOnlyList<MachineSoftwareDto> Software, string Revision);
