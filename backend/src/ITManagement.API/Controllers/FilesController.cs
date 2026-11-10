using ITManagement.API.Responses;
using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Files;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ITManagement.API.Controllers;
/// <summary>Khởi tạo controller tải lên, liệt kê và quản lý tệp cá nhân.</summary>
[ApiController, Authorize(Roles = "Employee"), Route("api/files")]
public sealed class FilesController(IFileService files) : ControllerBase
{
    /// <summary>Lấy thông tin hạn mức và dung lượng lưu trữ còn lại.</summary>
    [HttpGet("/api/me/storage-quota")]
    public async Task<ActionResult<ApiResponse<PersonalQuotaResponse>>> Quota(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<PersonalQuotaResponse>.Ok(await files.GetQuotaAsync(ct)));
    }
    /// <summary>Tải tệp lên vùng lưu trữ riêng của người dùng.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileUploadLimits.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileUploadLimits.MaxRequestBytes)]
    [ProducesResponseType(typeof(ApiResponse<FileResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (Request.Form.Files.Count != 1)
            return BadRequest(ApiResponse.Fail("Chỉ tải lên một file mỗi yêu cầu."));
        await using var content = file.OpenReadStream();
        var result = await files.UploadAsync(file.FileName, content, ct);
        return CreatedAtAction(nameof(Download), new { id = result.Id }, ApiResponse<FileResponse>.Ok(result));
    }
    /// <summary>Liệt kê các tệp thuộc người dùng hiện tại.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<FileResponse>>>> List([FromQuery] FileQuery query, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<FileResponse>>.Ok(await files.ListAsync(query, ct)));
    /// <summary>Tải nội dung tệp thuộc người dùng hiện tại.</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await files.DownloadAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, "application/octet-stream", file.FileName);
    }
    /// <summary>Xóa tệp thuộc người dùng hiện tại.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await files.DeleteAsync(id, ct); return NoContent();
    }
}
