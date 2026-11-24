using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Common.Exceptions;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;
public sealed class PasswordPolicyService(AppDbContext db, ICurrentUserService current, TimeProvider clock) : IPasswordPolicyService
{
    public const string DefaultPassword = "12345Az@";
    public static void BeginCycle(AppUser user)
    {
        user.RequiresPasswordChange = user.Role == UserRole.Employee;
        user.PasswordFirstLoginAt = null;
        user.PasswordReminderDay = 0;
        user.PasswordExpiredLock = false;
    }
    public async Task ExpireAsync(Guid? userId, CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-7);
        await db.AppUsers.Where(u => (!userId.HasValue || u.Id == userId.Value) && u.Status == UserStatus.Active
                && u.Role == UserRole.Employee && u.RequiresPasswordChange && u.PasswordFirstLoginAt <= cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked)
                .SetProperty(u => u.PasswordExpiredLock, true)
                .SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1), ct);
    }
    private PasswordPolicyResponse Read(AppUser user)
    {
        if (!user.RequiresPasswordChange || user.Role != UserRole.Employee) return new(false, null, null);
        if (user.PasswordFirstLoginAt is not DateTime start) return new(true, null, null);
        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var elapsed = (clock.GetUtcNow().UtcDateTime - start).TotalDays;
        var day = elapsed >= 6 ? 7 : elapsed >= 5 ? 6 : elapsed >= 4 ? 5 : 1;
        return new(true, start.AddDays(7), day > user.PasswordReminderDay ? day : null);
    }
    public async Task<PasswordPolicyResponse> GetAsync(CancellationToken ct)
    {
        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == current.UserId, ct);
        if (user is null || user.Status != UserStatus.Active) throw new AuthenticationRequiredException("Vui lòng đăng nhập lại.");
        return Read(user);
    }
    public async Task AcknowledgeAsync(int day, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var id = current.UserId ?? throw new AuthenticationRequiredException("Vui lòng đăng nhập lại.");
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_users WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}").SingleAsync(ct);
        var policy = Read(user);
        if (user.Status != UserStatus.Active || policy.Deadline <= clock.GetUtcNow().UtcDateTime)
            throw new AuthenticationRequiredException("Tài khoản đã hết hạn đổi mật khẩu.");
        if (policy.ReminderDay == day) { user.PasswordReminderDay = day; await db.SaveChangesAsync(ct); }
        else if (user.PasswordReminderDay != day) throw new BusinessRuleException("Thông báo đã thay đổi. Hãy tải lại.");
        await tx.CommitAsync(ct);
    }
}
