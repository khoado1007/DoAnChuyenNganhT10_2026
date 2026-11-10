using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Contracts.Auth;
using ITManagement.Application.Contracts.Users;
using ITManagement.Application.Services;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Identity;

namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ đăng nhập và truy vấn người dùng hiện tại.</summary>
public sealed class AuthService(UserManager<IdentityUser> userManager, IUserRepository users,
    IJwtService jwt, ICurrentUserService currentUser) : IAuthService
{
    /// <summary>Xác thực thông tin đăng nhập và phát hành token phiên.</summary>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var login = request.Username.Trim();
        var separator = login.IndexOf('\\');
        if (separator >= 0)
        {
            // Domain ID là alias của Identity hiện có, không tạo kho mật khẩu thứ hai.
            if (!login[..separator].Equals("COMPANY", StringComparison.OrdinalIgnoreCase))
                throw InvalidCredentials();
            login = login[(separator + 1)..];
            if (login.Length == 0 || login.Contains('\\')) throw InvalidCredentials();
        }
        var identity = await userManager.FindByNameAsync(login);
        if (identity is null || !await userManager.CheckPasswordAsync(identity, request.Password)
            || await userManager.IsLockedOutAsync(identity))
            throw InvalidCredentials();

        var user = await users.GetByIdentityIdAsync(identity.Id, ct);
        if (user is null || user.Status != UserStatus.Active) throw InvalidCredentials();

        var token = jwt.GenerateAccessToken(user.Id, user.Username, user.Role.ToString(), user.Email, user.SecurityVersion);
        return new AuthResponse(token, UserResponse.From(user));
    }

    /// <summary>Lấy thông tin người dùng gắn với phiên hiện tại.</summary>
    public async Task<UserResponse> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid id)
            throw new AuthenticationRequiredException("Vui lòng đăng nhập.");
        var user = await users.GetByIdAsync(id, ct);
        if (user is null || user.Status != UserStatus.Active)
            throw new AuthenticationRequiredException("Phiên đăng nhập không còn hợp lệ.");
        return UserResponse.From(user);
    }

    // Tạo lỗi xác thực thống nhất cho thông tin đăng nhập không hợp lệ.
    private static AuthenticationRequiredException InvalidCredentials()
        => new("Tên đăng nhập hoặc mật khẩu không hợp lệ.");
}
