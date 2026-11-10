using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Enums;

namespace ITManagement.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    /// <summary>Tìm người dùng theo mã định danh miền.</summary>
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Tìm người dùng theo tên đăng nhập.</summary>
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
    /// <summary>Tìm người dùng theo mã tài khoản Identity.</summary>
    Task<AppUser?> GetByIdentityIdAsync(string identityUserId, CancellationToken ct = default);
    /// <summary>Liệt kê người dùng theo vai trò.</summary>
    Task<IEnumerable<AppUser>> GetByRoleAsync(UserRole role, CancellationToken ct = default);
    /// <summary>Truy vấn danh sách người dùng theo điều kiện lọc.</summary>
    Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Thêm người dùng mới.</summary>
    Task AddAsync(AppUser user, CancellationToken ct = default);
    /// <summary>Cập nhật người dùng hiện có.</summary>
    Task UpdateAsync(AppUser user, CancellationToken ct = default);
}
