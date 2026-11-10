using ITManagement.Application.Contracts.Auth;
using ITManagement.Application.Contracts.Users;

namespace ITManagement.Application.Services;

public interface IAuthService
{
    /// <summary>Xác thực thông tin đăng nhập và tạo phiên.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    /// <summary>Lấy thông tin người dùng hiện đang đăng nhập.</summary>
    Task<UserResponse> GetCurrentUserAsync(CancellationToken ct = default);
}
