using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Contracts.Auth;
using ITManagement.Application.Contracts.Users;
using ITManagement.Application.Services;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Identity;
using ITManagement.Infrastructure.Persistence;
using ITManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ đăng nhập và truy vấn người dùng hiện tại.</summary>
public sealed class AuthService(UserManager<IdentityUser> userManager, IUserRepository users,
    IJwtService jwt, ICurrentUserService currentUser, AppDbContext db, IPasswordPolicyService passwords, TimeProvider clock) : IAuthService
{
    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid id || currentUser.Role != UserRole.Employee)
            throw new AuthenticationRequiredException("Vui lòng đăng nhập tài khoản nhân viên.");
        if (request.NewPassword != request.ConfirmPassword)
            throw new BusinessRuleException("Mật khẩu nhập lại không khớp.");
        if (request.NewPassword == request.CurrentPassword)
            throw new BusinessRuleException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}").SingleOrDefaultAsync(ct);
        if (user is null || user.Status != UserStatus.Active || user.Role != UserRole.Employee)
            throw new AuthenticationRequiredException("Phiên đăng nhập không còn hợp lệ.");
        var identity = await userManager.FindByIdAsync(user.IdentityUserId);
        if (identity is null) throw new AuthenticationRequiredException("Phiên đăng nhập không còn hợp lệ.");
        var result = await userManager.ChangePasswordAsync(identity, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            throw new BusinessRuleException(result.Errors.Any(e => e.Code == "PasswordMismatch")
                ? "Mật khẩu hiện tại không đúng."
                : "Mật khẩu mới không hợp lệ. Dùng ít nhất 8 ký tự, có chữ thường và chữ số.");
        user.RequiresPasswordChange = false;
        user.PasswordFirstLoginAt = null;
        user.PasswordReminderDay = 0;
        user.PasswordExpiredLock = false;
        user.SecurityVersion++;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

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

        var known = await users.GetByIdentityIdAsync(identity.Id, ct);
        if (known is null) throw InvalidCredentials();
        await passwords.ExpireAsync(known.Id, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var id = known.Id;
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}").SingleAsync(ct);
        await db.Entry(user).ReloadAsync(ct);
        await db.Entry(identity).ReloadAsync(ct);
        if (!await userManager.CheckPasswordAsync(identity, request.Password)) throw InvalidCredentials();
        if (user is null || user.Status != UserStatus.Active) throw InvalidCredentials();

        if (user.Role == UserRole.Employee && user.RequiresPasswordChange && user.PasswordFirstLoginAt is null)
            user.PasswordFirstLoginAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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
