using ITManagement.API.Responses;
using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Users;
using ITManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ITManagement.API.Controllers;

/// <summary>Khởi tạo controller quản lý tài khoản người dùng.</summary>
[ApiController, Authorize(Policy = "AdminOnly")]
[Route("api/users")]
[RequestSizeLimit(8192)]
public sealed class UsersController(IUserService users) : ControllerBase
{
    /// <summary>Liệt kê tài khoản người dùng theo bộ lọc.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserResponse>>>> List([FromQuery] UserQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<UserResponse>>.Ok(await users.ListAsync(query, ct)));
    /// <summary>Tạo tài khoản nhân viên mới.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Create(CreateUserRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserResponse>.Ok(await users.CreateAsync(request, ct)));
    /// <summary>Cập nhật trạng thái và thông tin vai trò của người dùng.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserResponse>.Ok(await users.UpdateAsync(id, request, ct)));
}
