using System.ComponentModel.DataAnnotations;
namespace ITManagement.Application.Contracts.Files;
public sealed class QuotaQuery
{
    [Range(1, 100000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
public sealed class UpdateQuotaRequest
{
    public const long MaxQuotaBytes = 1L << 40;
    [Required, Range(typeof(long), "0", "1099511627776")] public long? QuotaBytes { get; init; }
}
public sealed class ReconcileStorageRequest { public bool Apply { get; init; } }
public sealed record QuotaResponse(Guid UserId, string Username, long QuotaBytes, long UsedBytes, long AvailableBytes);
public sealed record ReconcileStorageResponse(bool Applied, long RecordedUsedBytes, long CalculatedUsedBytes,
    int OrphanFiles, int TombstoneFiles, int MissingLiveFiles, int InvalidMetadata, int SkippedEntries,
    bool CleanupBlocked, int DeletedFiles, int CleanupFailures);
