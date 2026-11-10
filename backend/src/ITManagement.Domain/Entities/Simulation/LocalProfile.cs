namespace ITManagement.Domain.Entities.Simulation;

public sealed class LocalProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AssetId { get; set; }
    public int MaxFiles { get; set; } = 10;
    public int UsedFiles { get; set; }
    public bool Ready { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DeviceAccessGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AssetId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}

public sealed class DeviceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Guid UserId { get; set; }
    public Guid AssetId { get; set; }
    public int SecurityVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class ProfileFile
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = "";
    public string Folder { get; set; } = "";
    public string Extension { get; set; } = "";
    public long SizeBytes { get; set; }
}

public sealed class ProfileOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid SessionId { get; set; }
    public Guid AgentId { get; set; }
    public string Kind { get; set; } = "";
    public string State { get; set; } = "Pending";
    public string CommandJson { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string? ErrorCode { get; set; }
    public int ReservedFiles { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class AppAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public Guid ResourceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
