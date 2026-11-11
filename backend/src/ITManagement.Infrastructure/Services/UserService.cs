using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Users;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Entities.Storage;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ITManagement.Infrastructure.Services;

/// <summary>Khởi tạo dịch vụ tạo, liệt kê và cập nhật tài khoản.</summary>
public sealed class UserService(AppDbContext db, UserManager<IdentityUser> identities) : IUserService
{
    // Kiểm tra vai trò được phép cấp cho tài khoản.
    private static void CheckRole(UserRole role)
    {
        if (role is not (UserRole.Employee or UserRole.Technician))
            throw new BusinessRuleException("Chỉ tạo hoặc chỉnh sửa vai trò Nhân viên/Kỹ thuật viên tại đây.");
    }
    // Báo lỗi khi thao tác Identity không thành công.
    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new BusinessRuleException("Thông tin tài khoản không hợp lệ hoặc đã được sử dụng. Kiểm tra tên đăng nhập, email và mật khẩu (ít nhất 8 ký tự, có chữ số).");
    }
    // Tạo lỗi nghiệp vụ khi tên đăng nhập hoặc email đã được sử dụng.
    private static BusinessRuleException Duplicate() => new("Tên đăng nhập hoặc email đã được sử dụng.");

    /// <summary>Liệt kê tài khoản theo bộ lọc và phân trang.</summary>
    public async Task<PagedResult<UserResponse>> ListAsync(UserQuery query, CancellationToken ct)
    {
        var rows = db.AppUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(u => u.Username.ToLower().Contains(search) || u.FullName.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
        }
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderBy(u => u.Username).ThenBy(u => u.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return PagedResult<UserResponse>.Create(page.Select(UserResponse.From), query.Page, query.PageSize, count);
    }

    /// <summary>Tạo tài khoản cùng thông tin nhân viên và hạn mức ban đầu.</summary>
    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        CheckRole(request.Role);
        ct.ThrowIfCancellationRequested();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var identity = new IdentityUser { UserName = request.Username.Trim(), Email = request.Email.Trim().ToLowerInvariant(), EmailConfirmed = true };
            EnsureSuccess(await identities.CreateAsync(identity, request.Password));
            EnsureSuccess(await identities.AddToRoleAsync(identity, request.Role.ToString()));
            var user = new AppUser
            {
                IdentityUserId = identity.Id, Username = identity.UserName!, Email = identity.Email!,
                FullName = request.FullName.Trim(), Department = request.Department?.Trim() ?? "",
                Role = request.Role, Status = UserStatus.Active
            };
            user.StorageQuota = new StorageQuota { UserId = user.Id };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return UserResponse.From(user);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        { throw Duplicate(); }
    }

    /// <summary>Cập nhật vai trò, trạng thái và thông tin nhân viên.</summary>
    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        CheckRole(request.Role);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}").SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", id);
        if (user.Role == UserRole.Admin)
            throw new BusinessRuleException("Không chỉnh sửa tài khoản quản trị bootstrap trong chức năng này.");
        var identity = await identities.FindByIdAsync(user.IdentityUserId) ?? throw new NotFoundException("Identity", id);
        try
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (identity.Email != email) EnsureSuccess(await identities.SetEmailAsync(identity, email));
            if (user.Role != request.Role)
            {
                EnsureSuccess(await identities.RemoveFromRolesAsync(identity, await identities.GetRolesAsync(identity)));
                EnsureSuccess(await identities.AddToRoleAsync(identity, request.Role.ToString()));
            }
            if (user.Status != request.Status || user.Role != request.Role) user.SecurityVersion++;
            user.Email = email;
            user.FullName = request.FullName.Trim();
            user.Department = request.Department?.Trim() ?? "";
            user.Role = request.Role;
            user.Status = request.Status;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return UserResponse.From(user);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        { throw Duplicate(); }
    }
}
