using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    /// <summary>Khởi tạo repository người dùng với ngữ cảnh dữ liệu.</summary>
    public UserRepository(AppDbContext db) => _db = db;

    /// <summary>Tìm người dùng theo mã định danh miền.</summary>
    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.AppUsers.Include(u => u.StorageQuota).FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <summary>Tìm người dùng theo tên đăng nhập.</summary>
    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => _db.AppUsers.FirstOrDefaultAsync(u => u.Username == username, ct);

    /// <summary>Tìm người dùng theo mã tài khoản Identity.</summary>
    public Task<AppUser?> GetByIdentityIdAsync(string identityUserId, CancellationToken ct = default)
        => _db.AppUsers.FirstOrDefaultAsync(u => u.IdentityUserId == identityUserId, ct);

    /// <summary>Liệt kê người dùng theo vai trò.</summary>
    public async Task<IEnumerable<AppUser>> GetByRoleAsync(UserRole role, CancellationToken ct = default)
        => await _db.AppUsers.Where(u => u.Role == role).ToListAsync(ct);

    /// <summary>Truy vấn danh sách người dùng theo bộ lọc và phân trang.</summary>
    public async Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken ct = default)
        => await _db.AppUsers.ToListAsync(ct);

    /// <summary>Thêm người dùng vào ngữ cảnh dữ liệu.</summary>
    public async Task AddAsync(AppUser user, CancellationToken ct = default)
        => await _db.AppUsers.AddAsync(user, ct);

    /// <summary>Đánh dấu người dùng cần cập nhật.</summary>
    public Task UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        _db.AppUsers.Update(user);
        return Task.CompletedTask;
    }
}
