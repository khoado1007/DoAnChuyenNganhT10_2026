namespace ITManagement.Application.Services;

public sealed record PasswordPolicyResponse(bool Required, DateTime? Deadline, int? ReminderDay);
public sealed record PasswordReminderRequest(int Day);
public interface IPasswordPolicyService
{
    Task ExpireAsync(Guid? userId, CancellationToken ct);
    Task<PasswordPolicyResponse> GetAsync(CancellationToken ct);
    Task AcknowledgeAsync(int day, CancellationToken ct);
}
