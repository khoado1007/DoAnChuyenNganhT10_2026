using System.Linq.Expressions;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;

namespace ITManagement.Domain.Entities.User;

public readonly record struct PasswordChangeNotice(bool Required, DateTime? Deadline, int? ReminderDay);

public partial class AppUser
{
    private const int PasswordChangePeriodDays = 7;

    public void BeginPasswordChangeCycle()
    {
        RequiresPasswordChange = Role == UserRole.Employee;
        PasswordFirstLoginAt = null;
        PasswordReminderDay = 0;
        // Cấp lại mật khẩu cho tài khoản bị khóa vẫn giữ nguyên lý do khóa.
    }

    public void SetAccountStatus(UserStatus status)
    {
        if (PasswordExpiredLock && status == UserStatus.Active)
        {
            BeginPasswordChangeCycle();
            PasswordExpiredLock = false;
        }
        Status = status;
    }

    public void RecordPasswordLogin(DateTime utcNow)
    {
        if (Role == UserRole.Employee && RequiresPasswordChange)
            PasswordFirstLoginAt ??= utcNow;
    }

    public void CompletePasswordChange()
    {
        RequiresPasswordChange = false;
        PasswordFirstLoginAt = null;
        PasswordReminderDay = 0;
        PasswordExpiredLock = false;
    }

    public PasswordChangeNotice GetPasswordChangeNotice(DateTime utcNow)
    {
        if (Role != UserRole.Employee || !RequiresPasswordChange) return new(false, null, null);
        if (PasswordFirstLoginAt is not DateTime firstLogin) return new(true, null, null);

        // SQL Server trả datetime2 không kèm múi giờ. Thời điểm đăng nhập luôn được ghi theo UTC.
        var start = DateTime.SpecifyKind(firstLogin, DateTimeKind.Utc);
        var reminderDay = (utcNow - start).Days switch
        {
            >= 6 => 7,
            5 => 6,
            4 => 5,
            _ => 1
        };
        return new(true, start.AddDays(PasswordChangePeriodDays),
            reminderDay > PasswordReminderDay ? reminderDay : null);
    }

    public bool TryAcknowledgePasswordReminder(int day, DateTime utcNow)
    {
        var notice = GetPasswordChangeNotice(utcNow);
        if (Status != UserStatus.Active || notice.Deadline <= utcNow) return false;
        if (day == PasswordReminderDay) return true;
        if (day != notice.ReminderDay)
            throw new BusinessRuleException("Thông báo đã thay đổi. Hãy tải lại.");
        PasswordReminderDay = day;
        return true;
    }

    public static Expression<Func<AppUser, bool>> WithExpiredPassword(DateTime utcNow)
    {
        var cutoff = utcNow.AddDays(-PasswordChangePeriodDays);
        return user => user.Role == UserRole.Employee && user.Status == UserStatus.Active
            && user.RequiresPasswordChange && user.PasswordFirstLoginAt <= cutoff;
    }
}
