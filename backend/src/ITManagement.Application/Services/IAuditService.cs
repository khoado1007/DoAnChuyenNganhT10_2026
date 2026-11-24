using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Audit;

namespace ITManagement.Application.Services;

public interface IAuditService
{
    /// <summary>Nhật ký thao tác hệ thống, chỉ Admin. Không trả token hay nội dung file.</summary>
    Task<PagedResult<AuditEventResponse>> ListAuditEventsAsync(AuditLogQuery query, CancellationToken ct);

    /// <summary>Metadata file của người dùng theo máy, chỉ Admin. Không đọc nội dung.</summary>
    Task<PagedResult<AdminFileResponse>> ListFilesAdminAsync(AdminFileQuery query, CancellationToken ct);
}
