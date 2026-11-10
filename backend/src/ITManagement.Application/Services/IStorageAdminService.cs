using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Files;
namespace ITManagement.Application.Services;
public interface IStorageAdminService
{
    /// <summary>Liệt kê hạn mức và dung lượng lưu trữ của người dùng.</summary>
    Task<PagedResult<QuotaResponse>> ListAsync(QuotaQuery query, CancellationToken ct);
    /// <summary>Cập nhật hạn mức lưu trữ của người dùng.</summary>
    Task<QuotaResponse> UpdateAsync(Guid userId, long quotaBytes, CancellationToken ct);
    /// <summary>Đối soát dung lượng và đối tượng lưu trữ.</summary>
    Task<ReconcileStorageResponse> ReconcileAsync(Guid userId, bool apply, CancellationToken ct);
}
