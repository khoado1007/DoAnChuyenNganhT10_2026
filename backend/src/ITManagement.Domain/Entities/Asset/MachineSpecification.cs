namespace ITManagement.Domain.Entities.Asset;

/// <summary>Administrator maintained simulated machine inventory; never probes the host.</summary>
public sealed class MachineSpecification
{
    public Guid AssetId { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Processor { get; set; }
    public decimal? MemoryGb { get; set; }
    public decimal? DiskTotalGb { get; set; }
    public decimal? DiskFreeGb { get; set; }
    public bool? DiskEncryption { get; set; }
    public string? CollectorVersion { get; set; }
    public string? PrivateIp { get; set; }
    public string? PublicIp { get; set; }
    public DateTime? LastRestartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string SoftwareJson { get; set; } = "[]";
}
