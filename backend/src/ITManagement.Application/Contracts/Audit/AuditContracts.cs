using ITManagement.Application.Common.Models;

namespace ITManagement.Application.Contracts.Audit;

public sealed record AuditEventResponse(
    Guid Id,
    Guid? ActorId,
    string? ActorName,
    string Action,
    Guid ResourceId,
    DateTime CreatedAt
);

public sealed record AuditLogQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? ActorId,
    string? Action,
    int Page = 1,
    int PageSize = 50
);

public sealed record AdminFileResponse(
    Guid Id,
    Guid ProfileId,
    Guid UserId,
    string? UserName,
    Guid AssetId,
    string? AssetName,
    string Folder,
    string Name,
    string Extension,
    long SizeBytes,
    string StorageBackend
);

public sealed record AdminFileQuery(
    Guid? UserId,
    Guid? AssetId,
    string? Folder,
    int Page = 1,
    int PageSize = 50
);
