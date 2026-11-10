using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Files;
namespace ITManagement.Application.Services;
public interface IFileService
{
    /// <summary>Liệt kê tệp thuộc người dùng hiện tại.</summary>
    Task<PagedResult<FileResponse>> ListAsync(FileQuery query, CancellationToken ct);
    /// <summary>Lấy hạn mức và dung lượng còn lại của người dùng.</summary>
    Task<PersonalQuotaResponse> GetQuotaAsync(CancellationToken ct);
    /// <summary>Mở nội dung tệp thuộc người dùng hiện tại.</summary>
    Task<FileDownload> DownloadAsync(Guid id, CancellationToken ct);
    /// <summary>Tải tệp lên vùng lưu trữ riêng của người dùng.</summary>
    Task<FileResponse> UploadAsync(string fileName, Stream content, CancellationToken ct);
    /// <summary>Xóa tệp thuộc người dùng hiện tại.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
