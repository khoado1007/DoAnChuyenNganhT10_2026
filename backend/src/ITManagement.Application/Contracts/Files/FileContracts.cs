using System.ComponentModel.DataAnnotations;
namespace ITManagement.Application.Contracts.Files;
public sealed class FileQuery
{
    [Range(1, 100000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
public sealed record FileResponse(Guid Id, string DisplayName, long SizeBytes, DateTime UploadedAt, string VirtualFolder);
public sealed record FileDownload(Stream Content, string FileName);
public static class FileUploadLimits
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const long MaxRequestBytes = 11 * 1024 * 1024;
}
public sealed record PersonalQuotaResponse(long QuotaBytes, long UsedBytes, long AvailableBytes, long MaxFileBytes);
