using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Enums;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;
public sealed class PasswordPolicyService(AppDbContext db, ICurrentUserService current, TimeProvider clock) : IPasswordPolicyService
{
    public async Task ExpireAsync(Guid? userId, CancellationToken ct)
    {
        await db.AppUsers.Where(AppUser.WithExpiredPassword(clock.GetUtcNow().UtcDateTime))
            .Where(u => !userId.HasValue || u.Id == userId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked)
                .SetProperty(u => u.PasswordExpiredLock, true)
                .SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1), ct);
    }
    public async Task<PasswordPolicyResponse> GetAsync(CancellationToken ct)
    {
        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == current.UserId, ct);
        if (user is null || user.Status != UserStatus.Active) throw new AuthenticationRequiredException("Vui lòng đăng nhập lại.");
        var notice = user.GetPasswordChangeNotice(clock.GetUtcNow().UtcDateTime);
        return new(notice.Required, notice.Deadline, notice.ReminderDay);
    }
    public async Task AcknowledgeAsync(int day, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var id = current.UserId ?? throw new AuthenticationRequiredException("Vui lòng đăng nhập lại.");
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}").SingleAsync(ct);
        if (!user.TryAcknowledgePasswordReminder(day, clock.GetUtcNow().UtcDateTime))
            throw new AuthenticationRequiredException("Tài khoản đã hết hạn đổi mật khẩu.");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
