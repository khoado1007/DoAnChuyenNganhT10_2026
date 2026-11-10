using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Users;
namespace ITManagement.Application.Services;

public interface IUserService
{
    /// <summary>Liệt kê tài khoản theo bộ lọc và phân trang.</summary>
    Task<PagedResult<UserResponse>> ListAsync(UserQuery query, CancellationToken ct);
    /// <summary>Tạo tài khoản nhân viên mới.</summary>
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct);
    /// <summary>Cập nhật thông tin, vai trò hoặc trạng thái người dùng.</summary>
    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);
}
