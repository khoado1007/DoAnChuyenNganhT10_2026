namespace ITManagement.Domain.Entities.Agents;

public sealed class RegisteredAgent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Guid InstanceId { get; set; }
    public string Mode { get; set; } = "real";
    public string CredentialHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? DisabledAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public long LastSequence { get; set; }
    public string Hostname { get; set; } = "";
    public string Os { get; set; } = "";
    public string Cpu { get; set; } = "";
    public long RamBytes { get; set; }
    public string AgentVersion { get; set; } = "";
}

public sealed class EnrollmentToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public string Mode { get; set; } = "real";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
