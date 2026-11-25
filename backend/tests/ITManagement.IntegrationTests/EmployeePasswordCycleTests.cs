using ITManagement.Domain.Entities.User;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using Xunit;

namespace ITManagement.IntegrationTests;

public sealed class EmployeePasswordCycleTests
{
    private static readonly DateTime FirstLogin = new(2026, 11, 25, 17, 45, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(4, 1, 5)]
    [InlineData(5, 5, 6)]
    [InlineData(6, 6, 7)]
    public void Reminders_follow_elapsed_days_and_can_be_acknowledged_once(int elapsedDays, int previousDay, int dueDay)
    {
        var user = new AppUser { Role = UserRole.Employee };
        user.BeginPasswordChangeCycle();
        user.RecordPasswordLogin(FirstLogin);
        var boundary = FirstLogin.AddDays(elapsedDays);
        user.RecordPasswordLogin(boundary); // Lần đăng nhập sau không dời hạn đổi mật khẩu.
        Assert.True(user.TryAcknowledgePasswordReminder(previousDay, boundary.AddTicks(-1)));
        Assert.Null(user.GetPasswordChangeNotice(boundary.AddTicks(-1)).ReminderDay);
        Assert.Equal(dueDay, user.GetPasswordChangeNotice(boundary).ReminderDay);
        Assert.Throws<BusinessRuleException>(() => user.TryAcknowledgePasswordReminder(99, boundary));
        Assert.True(user.TryAcknowledgePasswordReminder(dueDay, boundary));
        Assert.True(user.TryAcknowledgePasswordReminder(dueDay, boundary));
        Assert.Null(user.GetPasswordChangeNotice(boundary).ReminderDay);
    }

    [Fact]
    public void Expiry_starts_at_first_login_and_includes_the_exact_deadline()
    {
        var deadline = FirstLogin.AddDays(7);
        var overdue = AppUser.WithExpiredPassword(deadline).Compile();
        var user = new AppUser { Role = UserRole.Employee };
        user.BeginPasswordChangeCycle();
        Assert.Null(user.GetPasswordChangeNotice(FirstLogin).Deadline);
        Assert.False(overdue(user));
        user.RecordPasswordLogin(FirstLogin);
        Assert.Equal(deadline, user.GetPasswordChangeNotice(FirstLogin).Deadline);
        Assert.False(AppUser.WithExpiredPassword(deadline.AddTicks(-1)).Compile()(user));
        Assert.True(overdue(user));
        Assert.False(user.TryAcknowledgePasswordReminder(7, deadline));
        user.CompletePasswordChange();
        Assert.False(overdue(user));
        Assert.False(user.GetPasswordChangeNotice(deadline).Required);
    }

    [Fact]
    public void Reset_preserves_expiry_lock_until_admin_reactivates_user()
    {
        var user = new AppUser { Role = UserRole.Employee, Status = UserStatus.Locked, PasswordExpiredLock = true };
        user.BeginPasswordChangeCycle();
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.True(user.PasswordExpiredLock);
        user.SetAccountStatus(UserStatus.Active);
        Assert.False(user.PasswordExpiredLock);
        Assert.True(user.RequiresPasswordChange);
        Assert.Null(user.PasswordFirstLoginAt);
        user.RecordPasswordLogin(FirstLogin);
        Assert.Equal(1, user.GetPasswordChangeNotice(FirstLogin).ReminderDay);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Technician)]
    public void Other_roles_are_exempt_even_with_old_cycle_data(UserRole role)
    {
        var user = new AppUser { Role = role, RequiresPasswordChange = true, PasswordFirstLoginAt = FirstLogin };
        var now = FirstLogin.AddDays(8);
        Assert.False(user.GetPasswordChangeNotice(now).Required);
        Assert.False(AppUser.WithExpiredPassword(now).Compile()(user));
        user.BeginPasswordChangeCycle();
        user.RecordPasswordLogin(now);
        Assert.Null(user.PasswordFirstLoginAt);
    }
}
